using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using Open.OneDrive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class OneDriveFileSystem : AuthenticatedFileSystem, ISocialExtension
    {
        #region ** fields

        public static string ClientId { get; private set; }
        public static string ClientSecret { get; private set; }
        public static string Scopes { get; private set; }
        public static string RedirectUri { get; private set; }

        private const int MAX_SIZE = 720;

        #endregion

        #region ** initialization

        static OneDriveFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["OneDriveId"];
            ClientSecret = ConfigurationManager.AppSettings["OneDriveSecret"];
            Scopes = ConfigurationManager.AppSettings["OneDriveScopes"];
            RedirectUri = ConfigurationManager.AppSettings["OneDriveRedirectUri"];
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

        protected override bool IsFileNameExtensionRequired
        {
            get
            {
                return true;
            }
        }

        #endregion

        #region ** authentication

        private static bool IMPLICIT_FLOW = false;

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var authenticationUrl = new Uri(OneDriveClient.GetRequestUrl(ClientId, Scopes, RedirectUri, response_type: IMPLICIT_FLOW ? "token" : "code"), UriKind.Absolute);
            var result = await authenticationBroker.WebAuthenticationBrokerAsync(authenticationUrl, new Uri(RedirectUri));

            var fragments = UriEx.ProcessFragments(IMPLICIT_FLOW ? result.Fragment : result.Query);
            string code;
            if (fragments.TryGetValue("code", out code))
            {
                var token = await OneDriveClient.ExchangeCodeForAccessTokenAsync(code, ClientId, ClientSecret, RedirectUri);
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn) };
            }
            else if (fragments.TryGetValue("access_token", out code))
            {
                var expiresIn = int.Parse(fragments["expires_in"]);
                return new AuthenticatonTicket { AuthToken = code, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(expiresIn) };
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
                if (IMPLICIT_FLOW)
                {
                    var client = new OneDriveClient(refreshToken);
                    var space = await client.GetDriveAsync(cancellationToken: cancellationToken);
                    return new AuthenticatonTicket { AuthToken = refreshToken };
                }
                else
                {
                    var token = await OneDriveClient.RefreshAccessTokenAsync(refreshToken, ClientId, ClientSecret, cancellationToken);
                    return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn) };
                }
            }
            catch (Exception exc)
            {
                throw ProcessException(exc);
            }
        }

        #endregion

        #region ** get info

        protected override DirPathMode DirPathMode
        {
            get
            {
                return DirPathMode.DirIdAsId;
            }
        }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            return (directory as OneDriveDirectory).ParentDirId;
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            return (file as OneDriveFile).ParentDirId;
        }

        protected override async Task<FileSystemDrive> GetDriveAsyncOverride(CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var space = await client.GetDriveAsync(cancellationToken: cancellationToken);
            return new FileSystemDrive(space.Quota.Used, space.Quota.Total, null);
        }

        protected override Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IDataCollection<FileSystemDirectory>>(new CursorList<FileSystemDirectory>(
                async (top, skipToken, sd, fe, ct) =>
                {
                    try
                    {
                        var orderby = GetOrderBy(sd);
                        var client = new OneDriveClient(await GetAccessTokenAsync(ct));
                        var result = await client.GetItemsAsync(GetFolderPath(dirId), select: "id,name,description,folder,specialFolder,size", filter: "folder ne null", skipToken: skipToken, top: top, orderby: orderby);
                        var folders = new List<FileSystemDirectory>();
                        if (result.Value != null)
                        {
                            foreach (Item item in result.Value)
                            {
                                folders.Add(new OneDriveDirectory(item, dirId));
                            }
                        }
                        return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(GetSkipToken(result.NextLink), folders);
                    }
                    catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                }, CanSort));
        }

        protected override Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IDataCollection<FileSystemFile>>(new CursorList<FileSystemFile>(
                async (top, skipToken, sd, fe, ct) =>
                {
                    try
                    {
                        var orderby = GetOrderBy(sd);
                        var client = new OneDriveClient(await GetAccessTokenAsync(ct));
                        var result = await client.GetItemsAsync(GetFolderPath(dirId), select: "id,name,file,size", expand: "thumbnails(select=medium)", filter: "file ne null", skipToken: skipToken, top: top, orderby: orderby);
                        var files = new List<FileSystemFile>();
                        if (result.Value != null)
                        {
                            foreach (Item item in result.Value)
                            {
                                files.Add(new OneDriveFile(item, parentDirId: dirId));
                            }
                        }
                        return new Tuple<string, IReadOnlyList<FileSystemFile>>(GetSkipToken(result.NextLink), files);
                    }
                    catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                }, CanSort));
        }


        private bool CanSort(IReadOnlyList<SortDescription> sortDescriptions)
        {
            var sortDescription = sortDescriptions.FirstOrDefault();
            if (sortDescription != null)
            {
                return sortDescription.SortPath == "Name" || sortDescription.SortPath == "Size" || sortDescription.SortPath == "ModifiedDate";
            }
            return false;
        }

        private string GetOrderBy(IReadOnlyList<SortDescription> sd)
        {
            if (sd == null || sd.Count != 1)
                return null;
            var sortDescription = sd.First();
            var order = sortDescription.Direction == SortDirection.Ascending ? "" : " desc";
            switch (sortDescription.SortPath)
            {
                case "Name":
                    return "name" + order;
                case "Size":
                    return "size" + order;
                case "ModifiedDate":
                    return "lastModifiedDateTime" + order;
            }

            return null;
        }

        protected override async Task<FileSystemDirectory> GetDirectoryAsyncOverride(string dirId, bool full, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var result = await client.GetItemAsync(GetFolderPath(dirId));
            return new OneDriveDirectory(result);
        }

        protected override async Task<FileSystemFile> GetFileAsyncOverride(string fileId, bool full, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var result = await client.GetItemAsync(GetFilePath(fileId), expand: "thumbnails(select=medium)");
            return new OneDriveFile(result);
        }

        private FileSystemItem ConvertItem(Item item)
        {
            FileSystemItem f = null;
            if (item.Folder != null)
            {

                f = new OneDriveDirectory(item);
            }
            else //if (item.Type == "file" || item.Type == "audio" || item.Type == "video" || item.Type == "photo")
            {
                f = new OneDriveFile(item);
            }
            return f;
        }

        #endregion

        #region ** create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory dir, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var directory = dir as OneDriveDirectory;
            var result = await client.CreateFolderAsync(GetFolderPath(dirId), directory.Name, directory.Description, null, cancellationToken: cancellationToken);
            return new OneDriveDirectory(result);
        }

        #endregion

        #region ** upload

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var result = await client.UploadFileAsync(GetFolderPath(dirId), file.Name, fileStream, null, progress, expand: "thumbnails(select=medium)", cancellationToken: cancellationToken);
            return new OneDriveFile(result, file.ContentType, dirId);
        }

        #endregion

        #region ** download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            return await client.DownloadFileAsync(OneDriveClient.GetPathById(fileId), cancellationToken);
        }

        #endregion

        #region ** delete

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            await client.DeleteItemAsync(GetFolderPath(dirId), cancellationToken: cancellationToken);
            return null;
        }

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            await client.DeleteItemAsync(GetFilePath(fileId), cancellationToken: cancellationToken);
            return null;
        }

        #endregion

        #region ** update

        protected override Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string dirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var item = new Item { Name = directory.Name, Description = (directory as OneDriveDirectory).Description };
            var result = await client.UpdateItemAsync(GetFolderPath(dirId), item, select: "id,name,folder,specialFolder,parentReference", cancellationToken: cancellationToken);
            return new OneDriveDirectory(result);
        }

        protected override Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var item = new Item { Name = file.Name, Description = (file as OneDriveFile).Description };
            var result = await client.UpdateItemAsync(GetFilePath(fileId), item, select: "id,name,file,size,parentReference", expand: "thumbnails(select=medium)", cancellationToken: cancellationToken);
            return new OneDriveFile(result, file.ContentType);
        }

        #endregion

        #region ** move

        protected override Task<bool> CanMoveDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(sourceDirId != targetDirId);
        }

        protected override async Task<FileSystemDirectory> MoveDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var item = new Item();
            item.ParentReference = new ItemReference();
            if (string.IsNullOrWhiteSpace(targetDirId))
                item.ParentReference.Path = GetFolderPath("");
            else
                item.ParentReference.Id = targetDirId;
            var result = await client.UpdateItemAsync(GetFolderPath(sourceDirId), item, cancellationToken: cancellationToken);
            return new OneDriveDirectory(result);
        }

        protected override Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var item = new Item();
            item.ParentReference = new ItemReference();
            if (string.IsNullOrWhiteSpace(targetDirId))
                item.ParentReference.Path = GetFolderPath("");
            else
                item.ParentReference.Id = targetDirId;
            var result = await client.UpdateItemAsync(GetFilePath(sourceFileId), item, cancellationToken: cancellationToken);
            return new OneDriveFile(result);
        }

        #endregion

        #region ** copy

        protected override Task<bool> CanCopyDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> CopyDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var item = new Item();
            item.Name = directory.Name;
            item.ParentReference = new ItemReference();
            if (string.IsNullOrWhiteSpace(targetDirId))
                item.ParentReference.Path = GetFolderPath("");
            else
                item.ParentReference.Id = targetDirId;
            var result = await client.CopyItemAsync(GetFolderPath(sourceDirId), item, cancellationToken: cancellationToken);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();
                await Task.Delay(1000);
                var status = await client.GetCopyStatusAsync(result, cancellationToken);
                if (status.Item1 != null)
                {
                    continue;
                }
                else
                {
                    var dirId = GetItemIdFromUrl(status.Item2);
                    var newItem = await client.GetItemAsync(GetFolderPath(dirId), cancellationToken: cancellationToken);
                    return new OneDriveDirectory(newItem);
                }
            }
        }

        protected override Task<bool> CanCopyFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> CopyFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var item = new Item();
            item.Name = file.Name;
            item.ParentReference = new ItemReference();
            if (string.IsNullOrWhiteSpace(targetDirId))
                item.ParentReference.Path = GetFolderPath("");
            else
                item.ParentReference.Id = targetDirId;
            var result = await client.CopyItemAsync(GetFilePath(sourceFileId), item, cancellationToken: cancellationToken);

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();
                await Task.Delay(1000);
                var status = await client.GetCopyStatusAsync(result, cancellationToken);
                if (status.Item1 != null)
                {
                    continue;
                }
                else
                {
                    var fileId = GetItemIdFromUrl(status.Item2);
                    var newItem = await client.GetItemAsync(GetFilePath(fileId), expand: "thumbnails(select=medium)", cancellationToken: cancellationToken);
                    return new OneDriveFile(newItem);
                }
            }
            throw new OperationCanceledException();
        }

        private string GetItemIdFromUrl(Uri uri)
        {
            string id = uri.AbsoluteUri;
            var index = id.IndexOf("items('");
            id = id.Substring(index + 7);
            index = id.IndexOf("')");
            id = id.Substring(0, index);
            return id;
        }

        #endregion

        #region ** social extension

        public event EventHandler CommentsChanged;

        public Task<FileSystemPerson> GetCurrentUserAsync(string dirId)
        {
            return Task.FromException<FileSystemPerson>(new NotImplementedException());
        }

        public bool CanAddComment(string fileId)
        {
            return false;
        }

        public Task<IDataCollection<FileSystemComment>> GetCommentsAsync(string fileId)
        {
            var photoId = Path.GetFileName(fileId);
            //var client = new SkyDrive.SkyDriveClient(await GetAccessToken(ct));
            //var result = await client.GetCommentsAsync(photoId);
            var comments = new List<FileSystemComment>();
            //foreach (SkyDrive.Comment item in result)
            //{
            //    comments.Add(new FileSystemComment
            //    {
            //        Id = item.Id,
            //        From = new FileSystemPerson
            //        {
            //            Id = item.From.Id,
            //            Name = item.From.Name,
            //            Thumbnail = string.Format("https://apis.live.net/v5.0/{0}/picture?type=small", item.From.Id),
            //        },
            //        Message = item.Message,
            //        CreatedTime = DateTime.Parse(item.CreatedTime, CultureInfo.InvariantCulture.DateTimeFormat),
            //    });
            //}
            return Task.FromResult<IDataCollection<FileSystemComment>>(comments.AsDataCollection());
        }

        public Task AddCommentAsync(string fileId, string message)
        {
            //var itemId = Path.GetFileName(fileId);
            //var client = new SkyDrive.SkyDriveClient(await GetAccessToken(/*cancellationToken*/));
            //var result = await client.AddCommentAsync(itemId, message);
            //RaiseCommentsChanged();
            throw new NotImplementedException();
        }

        private void RaiseCommentsChanged()
        {
            if (CommentsChanged != null)
                CommentsChanged(this, new EventArgs());
        }

        public Task AddThumbUp(string fileId)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task RemoveThumbUp(string fileId)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public bool CanThumbUp(string fileId)
        {
            return false;
        }

        public Task<IDataCollection<FileSystemPerson>> GetThumbsUpAsync(string fileId)
        {
            return Task.FromException<IDataCollection<FileSystemPerson>>(new NotImplementedException());
        }

        #endregion

        #region ** search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var client = new OneDriveClient(await GetAccessTokenAsync(cancellationToken));
            var result = await client.SearchAsync(GetFolderPath(""), query, expand: "thumbnails(select=medium)", select: "id,name,description,file,folder,size,specialFolder,parentReference", cancellationToken: cancellationToken);
            var searchResult = new List<FileSystemSearchItem>();
            foreach (var item in result.Value)
            {
                var f = ConvertItem(item);
                if (f != null)
                {
                    searchResult.Add(new FileSystemSearchItem { DirectoryId = GetDirPath(item.ParentReference.Path, item.ParentReference.Id), Item = f });
                }
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region ** implementation

        private string GetSkipToken(string nextLink)
        {
            string skipToken = null;
            if (!string.IsNullOrWhiteSpace(nextLink))
            {
                var query = new UriBuilder(nextLink).Query;
                UriEx.ProcessFragments(query).TryGetValue("$skiptoken", out skipToken);
            }
            return skipToken;
        }

        private string GetFilePath(string fileId)
        {
            return OneDriveClient.GetPathById(fileId);
        }

        private string GetFolderPath(string dirId)
        {
            if (string.IsNullOrWhiteSpace(dirId))
                return OneDriveClient.GetPath("");
            else
                return OneDriveClient.GetPathById(dirId);
        }

        internal static string GetDirPath(string path, string id)
        {
            return path == "/drive/root:" ? "" : id;
        }

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        private static Exception ProcessException(Exception exc)
        {
            //if (exc is SkyDrive.SkyDriveException)
            //{
            //    var error = (exc as SkyDrive.SkyDriveException).Error;
            //    switch (error.Code)
            //    {
            //        case "resource_already_exists":
            //            return new DuplicatedItemException(error.Message);
            //        case "request_parameter_missing":
            //            return new ArgumentNullException("Name");//The request entity body is missing the required parameter: 'name'. Required parameters include: 'name'.
            //    }
            //}
            if (exc is OneDriveException)
            {
                var error = (exc as OneDriveException).Error;
                if (error != null)
                {
                    switch (error.Code)
                    {
                        case "unauthenticated":
                            return new AccessDeniedException();
                        case "nameAlreadyExists":
                            return new DuplicatedItemException(error.Message);
                        case "request_parameter_missing":
                            return new ArgumentNullException("Name");//The request entity body is missing the required parameter: 'name'. Required parameters include: 'name'.
                    }
                }
            }
            return ProcessOAuthException(exc);
        }

        #endregion
    }
}
