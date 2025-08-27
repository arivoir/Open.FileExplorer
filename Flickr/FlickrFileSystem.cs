using C1.DataCollection;
using Open.FileSystemAsync;
using Open.Flickr;
using Open.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class FlickrFileSystem : AuthenticatedFileSystem, ISocialExtension, ISearchExtension
    {
        #region fields

        public static string ConsumerToken { get; private set; }
        public static string ConsumerTokenSecret { get; private set; }
        public static string RedirectUri { get; private set; }

        private string _userId;

        #endregion

        #region initialization

        static FlickrFileSystem()
        {
            ConsumerToken = ConfigurationManager.AppSettings["FlickrConsumerToken"];
            ConsumerTokenSecret = ConfigurationManager.AppSettings["FlickrConsumerTokenSecret"];
            RedirectUri = ConfigurationManager.AppSettings["FlickrRedirectUri"];
        }

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            _userId = (ticket.Tag as User).Id;
            return ticket;
        }

        private async Task<string[]> GetAccessTokensAsync(bool promptForUserInteraction, CancellationToken cancellationToken)
        {
            var ticket = await AuthenticateAsync(null, promptForUserInteraction, cancellationToken);
            var credentials = ticket.AuthToken.Split('&');
            return credentials;
        }

        #endregion

        #region authentication

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var requestToken = await FlickrClient.GetRequestTokenAsync(FlickrFileSystem.ConsumerToken, FlickrFileSystem.ConsumerTokenSecret, callbackUrl: RedirectUri);
            var loginUrl = new Uri(FlickrClient.GetAuthorizeUrl(FlickrFileSystem.ConsumerToken, FlickrFileSystem.ConsumerTokenSecret, requestToken.Token, perms: "delete"));

            var result = await authenticationBroker.WebAuthenticationBrokerAsync(loginUrl, new Uri(RedirectUri));
            var fragments = UriEx.ProcessFragments(result.Query);
            if (fragments.ContainsKey("oauth_token") && fragments.ContainsKey("oauth_verifier"))
            {
                var token = await FlickrClient.GetAccessTokenAsync(ConsumerToken, ConsumerTokenSecret, fragments["oauth_token"], requestToken.TokenSecret, fragments["oauth_verifier"]);
                var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, token.Token, token.TokenSecret);
                var user = await client.GetUserInfoAsync(CancellationToken.None);
                return new AuthenticatonTicket { AuthToken = token.Token + "&" + token.TokenSecret, Tag = user };
            }
            else
            {
                throw new Exception("oauth_token or oauth_verifier not found.");
            }
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var credentials = refreshToken.Split('&');
                var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
                var user = await client.GetUserInfoAsync(cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = user };
            }
            catch (Exception exc)
            {
                throw ProcessException(exc);
            }
        }

        #endregion

        #region object model

        #endregion

        #region get info

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

        protected override bool CacheDirectoriesMetadata(string dirId)
        {
            return string.IsNullOrWhiteSpace(dirId);
        }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            return "";
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            return "";
        }

        protected override Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dirId))
            {
                return Task.FromResult<IDataCollection<FileSystemDirectory>>(new PagedList<FileSystemDirectory>(
                    async (pageIndex, pageSize, ct) =>
                    {
                        try
                        {
                            var credentials = await GetAccessTokensAsync(true, ct);
                            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
                            var photosets = await client.GetPhotosetsAsync((int)Math.Floor((double)pageIndex / (double)pageSize) + 1, pageSize);
                            var albums = new List<FileSystemDirectory>();
                            foreach (var albumData in photosets.Photoset)
                            {
                                var album = new FlickrAlbum(_userId, albumData);
                                albums.Add(album);
                            }
                            return new Tuple<int, IList<FileSystemDirectory>>(photosets.Total, albums);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    }));
            }
            else
            {
                return Task.FromResult<IDataCollection<FileSystemDirectory>>(new EmptyCollectionView<FileSystemDirectory>());
            }
        }

        protected override Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IDataCollection<FileSystemFile>>(new PagedList<FileSystemFile>(
                async (pageIndex, pageSize, ct) =>
                {
                    try
                    {
                        var offset = (int)Math.Floor((double)pageIndex / (double)pageSize) + 1;
                        var credentials = await GetAccessTokensAsync(true, ct);
                        var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
                        if (string.IsNullOrWhiteSpace(dirId))
                        {
                            var result = await client.GetUserPhotosAsync(_userId, extras: "geo,date_upload,owner_name", page: offset, perPage: pageSize);
                            var photos = ConvertPhotos(result.List);
                            return new Tuple<int, IList<FileSystemFile>>(result.Total, photos);
                        }
                        else
                        {
                            var result = await client.GetPhotosAsync(dirId, extras: "geo,date_upload,owner_name", page: offset, perPage: pageSize);
                            var photos = ConvertPhotos(result.List, result.Owner);
                            return new Tuple<int, IList<FileSystemFile>>(result.Total, photos);
                        }
                    }
                    catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                })
            { AddToTheBeginning = true });
        }

        private List<FileSystemFile> ConvertPhotos(Photo[] list, string owner = null)
        {
            var photos = new List<FileSystemFile>();
            foreach (var photo in list)
            {
                if (!string.IsNullOrWhiteSpace(owner))
                    photo.Owner = owner;
                var p = new FlickrPhoto(photo);
                photos.Add(p);
            }
            return photos;
        }

        #endregion

        #region upload

        protected override string[] GetAcceptedFileTypesOverride(string dirId, bool includeSubDirectories)
        {
            return new string[] { "image/jpeg", "image/png", "image/gif", "image/tiff" };
        }

        protected override Task<bool> CanWriteFileAsyncOverride(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult<bool>(string.IsNullOrWhiteSpace(path));
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string path, FileSystemFile file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var collectionId = Path.GetFileName(path);
            var credentials = await GetAccessTokensAsync(true, cancellationToken);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            var dirId = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(dirId))
                dirId = "root";
            var item = file as FlickrPhoto;
            var f = new Photo
            {
                Title = item.Name,
                ContentType = item.ContentType,
                Description = item.Description,
                IsFamily = item.IsFamily ? 1 : 0,
                IsFriend = item.IsFriend ? 1 : 0,
                IsPublic = item.IsPublic ? 1 : 0,
                SafetyLevel = item.SafetyLevel.ToString(),
                Hidden = item.IsHidden ? "1" : "2",
            };
            var fileResp = await client.UploadPhotoAsync(f, fileStream, progress, cancellationToken);
            var f2 = await client.GetPhotoAsync(fileResp.Id);
            return new FlickrPhoto(f2);
        }

        #endregion

        #region create

        //protected override bool CanCreateDirectoryOverride(string path)
        //{
        //    return string.IsNullOrWhiteSpace(path);
        //}

        //protected override void CreateDirectoryAsyncOverride(string path, FileSystemItem directory, bool renameIfAlreadyExist, Action<AsyncResult<FileSystemItem>> completed)
        //{
        //    if (string.IsNullOrWhiteSpace(path))
        //    {
        //        ExecuteOperation<string, FileSystemItem, FileSystemItem>(CreateDirectoryTransaction, path, directory, completed);
        //    }
        //    else
        //    {
        //        completed(new AsyncResult<FileSystemItem>(new NotImplementedException()));
        //    }
        //}

        //private void CreateDirectoryTransaction(string path, FileSystemItem directory, Transaction<FileSystemItem> operation
        //{
        //    var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, AccessToken, AccessTokenSecret);
        //    client.CreateAlbumAsync(directory.Name, completed: r =>
        //    {
        //        if (r.Error == null)
        //        {
        //            operationnd(new FlickrAlbum(r.Result));
        //        }
        //        else
        //            operationnd(r.Error);
        //    });
        //}

        #endregion

        #region update

        protected override Task<bool> CanUpdateFileOverride(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult<bool>(true);
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string path, FileSystemFile item, CancellationToken cancellationToken)
        {
            var resourceId = Path.GetFileName(path);
            var folderPath = Path.GetParentPath(path);
            var newPhoto = new Photo { Id = item.Id, Title = item.Name };

            var credentials = await GetAccessTokensAsync(true, cancellationToken);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            await client.UpdatePhotoAsync(item.Id, item.Name);
            return item;
        }

        #endregion

        #region delete

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var credentials = await GetAccessTokensAsync(true, cancellationToken);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            await client.DeletePhotosetAsync(dirId);
            return null;
        }

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var credentials = await GetAccessTokensAsync(true, cancellationToken);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            await client.DeletePhotoAsync(fileId);
            return null;
        }

        #endregion

        #region download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var photo = await GetFileAsync(fileId, false, cancellationToken) as FlickrPhoto;
            var url = photo.Content;

            var credentials = await GetAccessTokensAsync(true, cancellationToken);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            return await client.DownloadPhotoAsync(url, cancellationToken);
        }

        #endregion

        #region social extension

        public event EventHandler CommentsChanged;

        public Task<FileSystemPerson> GetCurrentUserAsync(string dirId)
        {
            return Task.FromException<FileSystemPerson>(new NotImplementedException());
        }

        public bool CanAddComment(string path)
        {
            return true;
        }

        public async Task<IDataCollection<FileSystemComment>> GetCommentsAsync(string path)
        {
            var photoId = Path.GetFileName(path);
            try
            {
                var credentials = await GetAccessTokensAsync(true, CancellationToken.None);
                var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
                var result = await client.GetPhotosCommentsAsync(photoId, null, null, 0, 0, CancellationToken.None);
                var comments = new List<FileSystemComment>();
                if (result.List != null)
                {
                    foreach (var commentData in result.List)
                    {
                        var comment = new FileSystemComment();
                        comment.Id = commentData.Id;
                        comment.From = new FlickrPerson(commentData.IconFarm, commentData.IconServer, commentData.Author, commentData.AuthorName);
                        comment.Message = commentData.Text;
                        comment.CreatedTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) + TimeSpan.FromSeconds(long.Parse(commentData.CreatedDate));
                        comments.Add(comment);
                    }
                }
                return comments.AsDataCollection();
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        public async Task AddCommentAsync(string path, string message)
        {
            var photoId = Path.GetFileName(path);
            var credentials = await GetAccessTokensAsync(true, CancellationToken.None);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            await client.AddCommentAsync(photoId, message);
            RaiseCommentsChanged();
        }

        private void RaiseCommentsChanged()
        {
            if (CommentsChanged != null)
                CommentsChanged(this, new EventArgs());
        }

        public bool CanThumbUp(string path)
        {
            return false;
        }

        public Task AddThumbUp(string path)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task RemoveThumbUp(string path)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task<IDataCollection<FileSystemPerson>> GetThumbsUpAsync(string path)
        {
            return Task.FromException<IDataCollection<FileSystemPerson>>(new NotImplementedException());
        }

        #endregion

        #region search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var credentials = await GetAccessTokensAsync(true, cancellationToken);
            var client = new FlickrClient(ConsumerToken, ConsumerTokenSecret, credentials[0], credentials[1]);
            var result = await client.GetUserPhotosAsync(_userId, query, extras: "geo");
            var searchResult = new List<FileSystemSearchItem>();
            foreach (FileSystemItem file in ConvertPhotos(result.List))
            {
                searchResult.Add(new FileSystemSearchItem { DirectoryId = "", Item = file });
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        protected static Exception ProcessException(Exception exc)
        {
            var fExc = exc as FlickrException;
            if (fExc != null)
            {
                if (fExc.Code == 98)
                {
                    return new AccessDeniedException();
                }
            }
            return ProcessOAuthException(exc);
        }

        #endregion
    }
}
