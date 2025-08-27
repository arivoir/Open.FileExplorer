using C1.DataCollection;
using Open.FileSystemAsync;
using Open.GooglePhotos;
using Open.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class GooglePhotosFileSystem : AuthenticatedFileSystem
    {
        #region fields

        public static string ClientId { get; private set; }
        public static string ClientSecret { get; private set; }
        public static string Scopes { get; private set; }
        public static string RedirectUri { get; private set; }


        //private string _metadataFields = "gphoto:user,gphoto:nickname,gphoto:thumbnail,gphoto:quotacurrent,gphoto:quotalimit";
        private string _albumFields = "openSearch:totalResults,entry(gphoto:id,gphoto:location,gphoto:access,title,gphoto:numphotos,media:group(media:thumbnail,media:description,media:keywords),link[@rel='alternate'],georss:where)";
        private string _photoFields = "openSearch:totalResults,entry(published,link[@rel='alternate'],gphoto:id,title,gphoto:width,gphoto:height,gphoto:size,media:group,georss:where)";
        private string _photoFieldsWithAlbum = "openSearch:totalResults,entry(published,link[@rel='alternate'],gphoto:id,gphoto:albumid,title,gphoto:width,gphoto:height,gphoto:size,media:group,georss:where)";

        public const string Photos = "photos";
        public const string Sharing = "sharing";
        public const string Albums = "albums";

        public FileSystemDirectory PhotosDirectory { get; private set; }
        public FileSystemDirectory AlbumsDirectory { get; private set; }
        public FileSystemDirectory SharingDirectory { get; private set; }

        #endregion

        #region initialization

        static GooglePhotosFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["GoogleId"];
            ClientSecret = ConfigurationManager.AppSettings["GoogleSecret"];
            Scopes = GooglePhotosClient.LIBRARY_SCOPE + " " + GooglePhotosClient.SHARING_SCOPE;//ConfigurationManager.AppSettings["PicasaScopes"];
            RedirectUri = ConfigurationManager.AppSettings["GoogleRedirectUri"];
        }

        public GooglePhotosFileSystem()
        {
            PhotosDirectory = new GooglePhotosDirectory(Photos, GooglePhotosResources.PhotosLabel);
            AlbumsDirectory = new GooglePhotosDirectory(Albums, GooglePhotosResources.AlbumsLabel);
            SharingDirectory = new GooglePhotosDirectory(Sharing, GooglePhotosResources.SharingLabel);
        }

        #endregion

        #region authentication

        public override string[] GetScopes(string dirId)
        {
            return new string[] { Scopes };
        }

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            return ticket;
        }

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var callbackUrl = RedirectUri;
            var authenticationUrl = new Uri(GooglePhotosClient.GetRequestUrl(GooglePhotosFileSystem.ClientId, GooglePhotosFileSystem.Scopes, callbackUrl), UriKind.Absolute);
            var result = await authenticationBroker.WebAuthenticationBrokerAsync(authenticationUrl, new Uri(callbackUrl));

            var fragments = UriEx.ProcessFragments(result.Query);
            string code;
            if (fragments.TryGetValue("code", out code))
            {
                var token = await GooglePhotosClient.ExchangeCodeForAccessTokenAsync(code, GooglePhotosFileSystem.ClientId, GooglePhotosFileSystem.ClientSecret, callbackUrl);
                //var client = new GooglePhotosClient(token.AccessToken);
                //var metadata = await client.GetMetadataAsync();
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn), /*Tag = metadata, */GrantedScopes = new string[] { GooglePhotosFileSystem.Scopes } };
            }
            else if (fragments.TryGetValue("error", out code) && code == "access_denied")
            {
                throw new OperationCanceledException();
            }
            else
            {
                throw new Exception("code not found.");
            }
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var token = await GooglePhotosClient.RefreshAccessTokenAsync(refreshToken, ClientId, ClientSecret, cancellationToken);
                //var client = new GooglePhotosClient(token.AccessToken);
                //var metadata = await client.GetMetadataAsync();
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn),/* Tag = metadata,*/ GrantedScopes = new string[] { GooglePhotosFileSystem.Scopes } };
            }
            catch (Exception exc)
            {
                throw ProcessOAuthException(exc);
            }
        }

        #endregion

        #region get info

        protected override DirPathMode DirPathMode
        {
            get
            {
                return DirPathMode.FullPathAsId;
            }
        }

        protected override UniqueFileNameMode UniqueFileNameMode
        {
            get
            {
                return UniqueFileNameMode.DirName_FileId_Extension;
            }
        }

        public int PageSize { get; private set; } = 50;

        protected override bool CacheDirectoriesMetadata(string dirId)
        {
            return string.IsNullOrWhiteSpace(dirId);
        }

        protected override bool CacheFilesMetadata(string dirId)
        {
            return !string.IsNullOrWhiteSpace(dirId);
        }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            return "";
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            return (file as GooglePhotosPhoto).AlbumId;
        }

        protected override async Task<FileSystemDrive> GetDriveAsyncOverride(CancellationToken cancellationToken)
        {
            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, cancellationToken));
            throw new NotImplementedException();
        }

        protected override Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            IDataCollection<FileSystemDirectory> result;
            if (string.IsNullOrWhiteSpace(dirId))
            {
                var directories = new List<FileSystemDirectory>();
                directories.Add(PhotosDirectory);
                directories.Add(AlbumsDirectory);
                directories.Add(SharingDirectory);
                result = directories.AsDataCollection();
            }
            else if (dirId == Albums)
            {
                result = new CursorList<FileSystemDirectory>(
                    async (pageIndex, pageToken, sort, filter, ct) =>
                    {
                        try
                        {
                            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.GetAlbumsAsync(pageToken, PageSize, "albums(id,title,coverPhotoBaseUrl)", ct);
                            var albums = new List<FileSystemDirectory>();
                            if (r.Albums != null)
                            {
                                foreach (var album in r.Albums)
                                {
                                    var a = new GooglePhotosAlbum(album);
                                    albums.Add(a);
                                }
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(r.NextPageToken, albums);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else if (dirId == Sharing)
            {
                result = new CursorList<FileSystemDirectory>(
                    async (pageIndex, pageToken, sort, filter, ct) =>
                    {
                        try
                        {
                            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.GetSharedAlbumsAsync(pageToken, PageSize, "sharedAlbums(id,title,coverPhotoBaseUrl)", ct);
                            var albums = new List<FileSystemDirectory>();
                            if (r.SharedAlbums != null)
                            {
                                foreach (var album in r.SharedAlbums)
                                {
                                    var a = new GooglePhotosAlbum(album);
                                    albums.Add(a);
                                }
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(r.NextPageToken, albums);
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

        protected override Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            IDataCollection<FileSystemFile> result;
            var parentId = Open.FileSystemAsync.Path.GetParentPath(dirId);
            if (dirId == Photos)
            {
                result = new CursorList<FileSystemFile>(
                    async (pageIndex, pageToken, sort, filter, ct) =>
                    {
                        try
                        {
                            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.SearchAsync(null, PageSize, pageToken, cancellationToken: ct);
                            var photos = new List<FileSystemFile>();
                            if (r.MediaItems != null)
                            {
                                foreach (var mediaItem in r.MediaItems)
                                {
                                    var p = new GooglePhotosPhoto(mediaItem, dirId);
                                    photos.Add(p);
                                }
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(r.NextPageToken, photos);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    });
            }
            else if (parentId == Albums || parentId == Sharing)
            {
                result = new CursorList<FileSystemFile>(
                    async (pageIndex, pageToken, sort, filter, ct) =>
                    {
                        try
                        {
                            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.SearchAsync(Open.FileSystemAsync.Path.GetFileName(dirId), PageSize, pageToken, cancellationToken: ct);
                            var photos = new List<FileSystemFile>();
                            if (r.MediaItems != null)
                            {
                                foreach (var mediaItem in r.MediaItems)
                                {
                                    var p = new GooglePhotosPhoto(mediaItem, dirId);
                                    photos.Add(p);
                                }
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(r.NextPageToken, photos);
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
            var directories = await GetDirectoriesAsync("", cancellationToken);
            await directories.LoadAsync();
            return directories.FirstOrDefault(d => d.Id == dirId);
        }

        #endregion

        #region upload

        protected override string[] GetAcceptedFileTypesOverride(string dirId, bool includeSubDirectories)
        {
            return new string[] { "image/*", "video/*" };
        }

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(dirId == Photos || Open.FileSystemAsync.Path.GetParentPath(dirId) == Albums);
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile item, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            string albumId = string.IsNullOrWhiteSpace(dirId) || dirId == Photos ? null : Open.FileSystemAsync.Path.GetFileName(dirId);
            var photo = item as GooglePhotosPhoto;
            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, cancellationToken));
            var session = await client.InitiateUploadSessionAsync(item.ContentType, item.Name, fileStream.Length, cancellationToken);
            var uploadToken = await client.UploadResumableFileAsync(session.url, fileStream, progress, cancellationToken);
            var mediaItem = await client.CreateMediaItemAsync(uploadToken, albumId, photo.Summary, null, cancellationToken);
            return new GooglePhotosPhoto(mediaItem.NewMediaItemResults.First().MediaItem, dirId);
        }

        #endregion

        #region create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(dirId == Albums);
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var album = item as GooglePhotosAlbum;
            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.CreateAlbumAsync(album.Name);
            var createdAlbum = new GooglePhotosAlbum(result);
            return createdAlbum;
        }

        #endregion

        #region download

        protected override async Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var photo = await GetFileAsync(fileId, false, cancellationToken) as GooglePhotosPhoto;
            return photo.Content != null;
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var photo = await GetFileAsync(fileId, false, cancellationToken) as GooglePhotosPhoto;
            var uri = photo.Content;

            var client = new GooglePhotosClient(await GetAccessTokenAsync(true, cancellationToken));
            return await client.DownloadFileAsync(uri, cancellationToken);
        }

        #endregion

        #region implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            if (exc.Message == "Token revoked")
            {
                return Task.FromResult<Exception>(new AccessDeniedException());
            }
            return Task.FromResult(ProcessOAuthException(exc));
        }

        #endregion
    }
}
