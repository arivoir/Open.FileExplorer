using Open.Facebook;
using Open.FileSystem;
using Open.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;
using Path = Open.FileSystem.Path;

namespace Open.FileExplorer
{
    public class FacebookFileSystem : AuthenticatedFileSystem, ISocialExtension//, ISearchExtension
    {
        #region ** fields

        private static string ClientId { get; set; }
        private static string ClientSecret { get; set; }
        private static string RedirectUri { get; set; }

        private User _user = null;
        private const int MAX_SIZE = 720;
        private static string _userFields = "id,name,permissions";
        private string _albumFields = "id,description,name,link,can_upload,count,privacy,type";
        private string _photoFields = "id,name,picture,link,source,width,height,can_delete";
        private string _photosOfMeFields = "id,name,from,picture,link,source,width,height,can_delete";
        private string _videoFields = "id,description,picture,source,permalink_url,created_time,updated_time";
        private string _commentFields = "id,message,from,created_time";
        private string _likeFields = "id,name";

        public static string PUBLIC_PROFILE_PERMISSION = "public_profile";
        public static string USER_PHOTOS_PERMISSION = "user_photos";
        public static string USER_VIDEOS_PERMISSION = "user_videos";
        public static string PUBLISH_PERMISSIONS = "publish_actions";
        public const string PhotosOfYou = "photosOfYou";
        public const string YourPhotos = "yourPhotos";
        public const string Albums = "albums";
        public const string Videos = "videos";

        private static FacebookAlbum PhotosOfYouDirectory = new FacebookAlbum(PhotosOfYou, FacebookResources.PhotosOfYouLabel);
        private static FacebookAlbum YourPhotosDirectory = new FacebookAlbum(YourPhotos, FacebookResources.YourPhotosLabel);
        private static FacebookAlbum AlbumsDirectory = new FacebookAlbum(Albums, FacebookResources.AlbumsLabel);
        private static FacebookAlbum VideosDirectory = new FacebookAlbum(Videos, FacebookResources.VideosLabel);

        #endregion

        #region ** initialization

        static FacebookFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["FacebookKey"];
            ClientSecret = ConfigurationManager.AppSettings["FacebookSecret"];
            RedirectUri = ConfigurationManager.AppSettings["FacebookRedirectUri"];
        }

        #endregion

        #region ** authentication

        public override string[] GetScopes(string dirId)
        {
            var permissions = new List<string>();
            if (dirId == PhotosOfYou || dirId == YourPhotos || dirId == Albums)
            {
                permissions.Add(USER_PHOTOS_PERMISSION);
            }
            else if (dirId == Videos)
            {
                permissions.Add(USER_VIDEOS_PERMISSION);
            }
            return permissions.ToArray();
        }

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            _user = ticket.Tag as User;
            return ticket;
        }

        protected override async Task InvalidateAccessAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            await base.InvalidateAccessAsyncOverride(dirId, cancellationToken);
            _user = null;
        }

        protected override Task RefreshAsyncOverride(string dirId = null)
        {
            return Task.FromResult(true);
        }

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            string callbackUrl = RedirectUri;
            scopes = scopes == null ? new string[] { PUBLIC_PROFILE_PERMISSION } : scopes;
            string scopes2 = string.Join(", ", scopes);
            var loginUrl = new Uri(FacebookClient.GetRequestUrl(FacebookFileSystem.ClientId, scopes2, callbackUrl: callbackUrl, display: "popup", auth_type: (requestingDeniedScope ? "rerequest" : null)));

            var result = await authenticationBroker.WebAuthenticationBrokerAsync(loginUrl, !string.IsNullOrWhiteSpace(callbackUrl) ? new Uri(callbackUrl) : null);

            var fragments = UriEx.ProcessFragments(result.Fragment);
            string code;
            if (fragments.TryGetValue("access_token", out code))
            {
                var user = await GetUserAsync(code, CancellationToken.None);
                return new AuthenticatonTicket
                {
                    AuthToken = code,
                    Tag = user,
                    GrantedScopes = user.Permissions.Data.Where(p => p.Status == "granted").Select(p => p.Value).ToArray(),
                    DeclinedScopes = user.Permissions.Data.Where(p => p.Status == "declined").Select(p => p.Value).ToArray()
                };
            }
            else
            {
                var errors = UriEx.ProcessFragments(result.Query);
                string errorReason;
                if (errors.TryGetValue("error_reason", out errorReason) && errorReason == "user_denied")
                {
                    throw new OperationCanceledException();
                }
                throw new Exception("access_token not found.");
            }
        }

        public async override Task<AuthenticatonTicket> RefreshTokenAsync(string connectionString, CancellationToken cancellationToken)
        {
            try
            {
                var user = await GetUserAsync(connectionString, cancellationToken);
                return new AuthenticatonTicket
                {
                    AuthToken = connectionString,
                    Tag = user,
                    GrantedScopes = user.Permissions.Data.Where(p => p.Status == "granted").Select(p => p.Value).ToArray(),
                    DeclinedScopes = user.Permissions.Data.Where(p => p.Status == "declined").Select(p => p.Value).ToArray(),
                };
            }
            catch (Exception exc)
            {
                throw ProcessException(exc);
            }
        }

        private static Task<User> GetUserAsync(string accessToken, CancellationToken cancellationToken)
        {
            var client = new FacebookClient(accessToken);
            return client.GetUserInfoAsync(_userFields, cancellationToken);
        }

        #endregion

        #region ** object model

        protected override bool ShowCountInDirectories
        {
            get
            {
                return true;
            }
        }

        protected override DirPathMode DirPathMode
        {
            get
            {
                return DirPathMode.DirIdAsId;
            }
        }

        protected override UniqueFileNameMode UniqueFileNameMode
        {
            get
            {
                return UniqueFileNameMode.DirName_FileId_Extension;
            }
        }
        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            var dirId = directory.Id;
            return dirId == PhotosOfYou || dirId == YourPhotos || dirId == Albums || dirId == Videos ? "" : Albums;
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            if (file is FacebookPhoto)
                return (file as FacebookPhoto).AlbumId;
            else
                return Videos;
        }

        #endregion

        #region ** get info

        protected override bool CacheDirectoriesMetadata(string dirId)
        {
            return dirId == Albums;
        }

        protected override bool CacheFilesMetadata(string dirId)
        {
            return !string.IsNullOrWhiteSpace(dirId) && dirId != Albums;
        }

        protected override Task<ICollectionView<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            ICollectionView<FileSystemDirectory> result;
            if (string.IsNullOrWhiteSpace(dirId))
            {
                var directories = new List<FileSystemDirectory>();
                directories.Add(YourPhotosDirectory);
                directories.Add(AlbumsDirectory);
                directories.Add(VideosDirectory);
                directories.Add(PhotosOfYouDirectory);
                result = new WrapCollectionView<FileSystemDirectory>(directories);
            }
            else if (dirId == Albums)
            {
                result = new CursorList<FileSystemDirectory>(
                    async (pageSize, after, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION }, true, ct));
                            var r = await client.GetAlbumsAsync(_albumFields, null, after: after);
                            var albums = new List<FileSystemDirectory>();
                            if (r.Data != null)
                            {
                                foreach (var albumData in r.Data)
                                {
                                    var album = new FacebookAlbum(albumData);
                                    albums.Add(album);
                                }
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(GetAfter(r), albums);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else
            {
                result = new EmptyCollectionView<FileSystemDirectory>();
            }
            return Task.FromResult(result);
        }

        protected override Task<ICollectionView<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            ICollectionView<FileSystemFile> result;
            if (dirId == PhotosOfYou)
            {
                result = new CursorList<FileSystemFile>(
                    async (pageSize, after, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION }, true, ct));
                            var r = await client.GetPhotosAsync(_photosOfMeFields, after: after);
                            var photos = new List<FileSystemFile>();
                            foreach (var photo in r.Data)
                            {
                                var p = new FacebookPhoto(photo, photo.From, PhotosOfYou);
                                photos.Add(p);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(GetAfter(r), photos);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else if (dirId == YourPhotos)
            {
                result = new CursorList<FileSystemFile>(
                    async (pageSize, after, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION }, true, ct));
                            var r = await client.GetPhotosAsync(_photoFields, after: after, type: "uploaded");
                            var photos = new List<FileSystemFile>();
                            foreach (var photo in r.Data)
                            {
                                var p = new FacebookPhoto(photo, _user, YourPhotos);
                                photos.Add(p);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(GetAfter(r), photos);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else if (dirId == Videos)
            {
                result = new CursorList<FileSystemFile>(
                    async (pageSize, after, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_VIDEOS_PERMISSION }, true, ct));
                            var r = await client.GetVideosAsync(_videoFields, after: after);
                            var photos = new List<FileSystemFile>();
                            foreach (var video in r.Data)
                            {
                                var p = new FacebookVideo(video, _user, Videos);
                                photos.Add(p);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(GetAfter(r), photos);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else if (dirId == Albums)
            {
                result = new EmptyCollectionView<FileSystemFile>();
            }
            else if (!string.IsNullOrWhiteSpace(dirId))
            {
                result = new CursorList<FileSystemFile>(
                    async (pageSize, after, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION }, true, ct));
                            var r = await client.GetAlbumPhotosAsync(dirId, _photoFields, limit: pageSize, after: after);
                            var photos = new List<FileSystemFile>();
                            foreach (var photo in r.Data)
                            {
                                var p = new FacebookPhoto(photo, _user, dirId);
                                photos.Add(p);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(GetAfter(r), photos);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else
            {
                result = new EmptyCollectionView<FileSystemFile>();
            }
            return Task.FromResult(result);
        }

        protected override async Task<FileSystemDirectory> GetDirectoryAsyncOverride(string dirId, bool full, CancellationToken cancellationToken)
        {
            if (dirId == PhotosOfYou)
                return PhotosOfYouDirectory;
            if (dirId == YourPhotos)
                return YourPhotosDirectory;
            if (dirId == Albums)
                return AlbumsDirectory;
            if (dirId == Videos)
                return VideosDirectory;
            if (!string.IsNullOrEmpty(dirId))
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(dirId));
                var client = new FacebookClient(await GetAccessTokenAsync());
                var album = new FacebookAlbum(await client.GetAlbumAsync(dirId, _albumFields));
                return album;
            }
            else
            {
                return await base.GetDirectoryAsyncOverride(dirId, full, cancellationToken);
            }
        }

        private static string GetAfter(AlbumList result)
        {
            return result.Paging != null ? result.Paging.Cursors.After : null;
        }

        private static string GetAfter(PhotoList result)
        {
            return result.Paging != null ? result.Paging.Cursors.After : null;
        }

        private static string GetAfter(VideoList result)
        {
            return result.Paging != null ? result.Paging.Cursors.After : null;
        }

        private static string GetUntil(PhotoList result)
        {
            string nextUntil = null;
            if (result.Paging != null)
            {
                var query = new UriBuilder(result.Paging.Next).Query;
                UriEx.ProcessFragments(query).TryGetValue("until", out nextUntil);
            }
            return nextUntil;
        }

        #endregion

        #region ** create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(dirId == Albums);
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION, PUBLISH_PERMISSIONS }, true, cancellationToken));
            var album = await client.CreateAlbumAsync(item.Name, privacy: GetPrivacy((item as FacebookAlbum).Permission), fields: _albumFields);
            album = await client.GetAlbumAsync(album.Id, _albumFields);
            return new FacebookAlbum(album);
        }

        private static string GetPrivacy(FacebookPermission permission)
        {
            string value;
            switch (permission)
            {
                case FacebookPermission.Public:
                    value = "EVERYONE";
                    break;
                case FacebookPermission.Friends:
                    value = "ALL_FRIENDS";
                    break;
                case FacebookPermission.OnlyMe:
                default:
                    value = "SELF";
                    break;
            }
            return value;
        }

        #endregion

        #region ** upload

        protected override string[] GetAcceptedFileTypesOverride(string dirId, bool includeSubDirectories)
        {
            return new string[] { "image/jpeg", "image/png", "image/gif", "image/tiff" };
        }

        protected override async Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            if (dirId == YourPhotos)
                return true;
            if (await GetDirectoryParentIdAsync(dirId, cancellationToken) == Albums)
            {
                var album = await GetDirectoryAsync(dirId, false, cancellationToken) as FacebookAlbum;
                return album.Type == "normal";
            }
            return false;
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile item, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var permissions = new List<string>();
            if (dirId == Videos)
                permissions.Add(USER_VIDEOS_PERMISSION);
            else
                permissions.Add(USER_PHOTOS_PERMISSION);
            permissions.Add(PUBLISH_PERMISSIONS);
            var albumId = dirId == YourPhotos ? "me" : dirId;
            var client = new FacebookClient(await GetAccessTokenAsync(permissions, true, cancellationToken));
            var uploadedPhoto = await client.UploadPhotoAsync(albumId, item.Name, fileStream, progress, cancellationToken);
            var photo = await client.GetPhotoAsync(uploadedPhoto.Id, _photoFields);
            return new FacebookPhoto(photo, _user, dirId);
        }

        #endregion

        #region ** download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var photo = await GetFileAsync(fileId, false, cancellationToken) as FacebookFile;
            var uri = photo.Content;
            var client = new FacebookClient(await GetAccessTokenAsync());
            return await client.DownloadPhotoAsync(uri, cancellationToken);
        }

        #endregion

        #region ** delete

        protected override async Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            var parentId = await GetFileParentIdAsync(fileId, cancellationToken);
            return parentId != Videos;
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new FacebookClient(await GetAccessTokenAsync());
            await client.DeleteResourceByIdAsync(fileId);
            return null;
        }

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new FacebookClient(await GetAccessTokenAsync());
            await client.DeleteResourceByIdAsync(dirId);
            return null;
        }

        #endregion

        #region ** social extension

        public event EventHandler CommentsChanged;

        public Task<FileSystemPerson> GetCurrentUserAsync(string dirId)
        {
            return Task.FromException<FileSystemPerson>(new NotImplementedException());
        }

        public bool CanAddComment(string path)
        {
            return true;
        }

        public async Task<ICollectionView<FileSystemComment>> GetCommentsAsync(string path)
        {
            var photoId = Path.GetFileName(path);
            try
            {
                var client = new FacebookClient(await GetAccessTokenAsync());
                var result = await client.GetCommentsAsync(photoId, _commentFields, cancellationToken: CancellationToken.None);
                var comments = new List<FileSystemComment>();
                foreach (var commentData in result)
                {
                    var comment = new FileSystemComment();
                    comment.Id = commentData.Id;
                    comment.From = new FacebookPerson(commentData.From);
                    comment.Message = commentData.Message;
                    comment.CreatedTime = DateTime.Parse(commentData.CreatedTime, CultureInfo.InvariantCulture.DateTimeFormat);
                    comments.Add(comment);
                }
                return new WrapCollectionView<FileSystemComment>(comments);
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        public async Task AddCommentAsync(string path, string message)
        {
            var photoId = Path.GetFileName(path);
            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION, PUBLISH_PERMISSIONS }, true, CancellationToken.None));
            await client.AddCommentAsync(photoId, message, _commentFields);
            OnCommentsChanged();
        }

        private void OnCommentsChanged()
        {
            CommentsChanged?.Invoke(this, new EventArgs());
        }

        public bool CanThumbUp(string path)
        {
            return false;
        }

        public async Task AddThumbUp(string path)
        {
            var fileId = Path.GetFileName(path);
            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION, PUBLISH_PERMISSIONS }, true, CancellationToken.None));
            await client.AddLikeAsync(fileId, _likeFields);
        }

        public async Task RemoveThumbUp(string path)
        {
            var fileId = Path.GetFileName(path);
            var client = new FacebookClient(await GetAccessTokenAsync(new string[] { USER_PHOTOS_PERMISSION, PUBLISH_PERMISSIONS }, true, CancellationToken.None));
            await client.RemoveLikeAsync(fileId, _likeFields);
        }

        public async Task<ICollectionView<FileSystemPerson>> GetThumbsUpAsync(string path)
        {
            var photoId = Path.GetFileName(path);
            try
            {
                var client = new FacebookClient(await GetAccessTokenAsync(cancellationToken: CancellationToken.None));
                var result = await client.GetLikesAsync(photoId, _likeFields, cancellationToken: CancellationToken.None);
                var people = new List<FileSystemPerson>();
                foreach (var likeData in result)
                {
                    var person = new FileSystemPerson
                    {
                        Id = likeData.Id,
                        Name = likeData.Name,
                        IsAuthenticatedUser = (likeData.Id == _user.Id)
                    };
                    people.Add(person);
                }
                return new WrapCollectionView<FileSystemPerson>(people);
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        #endregion

        #region ** implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        private static Exception ProcessException(Exception exc)
        {
            var fExc = exc as FacebookException;
            if (fExc != null)
            {
                switch (fExc.Error.Code)
                {
                    case 100:
                        exc = new ArgumentNullException("Name", exc.Message);
                        break;
                    case 190:
                        exc = new AccessDeniedException();
                        break;
                }
            }
            return exc;
        }

        #endregion
    }
}
