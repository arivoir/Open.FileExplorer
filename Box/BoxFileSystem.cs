using C1.DataCollection;
using Open.Box;
using Open.FileSystemAsync;
using Open.IO;
using System.Globalization;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer.Box
{
    public class BoxFileSystem : UnifiedItemsFileSystem, ISocialExtension, ISearchExtension
    {
        #region fields

        public static string ClientId { get; private set; }
        public static string ClientSecret { get; private set; }
        public static string RedirectUri { get; private set; }

        private string _filesFields = "name,parent,size,shared_link,owned_by,created_at";
        private string _commentsFields = "id,message,created_by,created_at,item";
        private string _rootFolderId = "0";
        #endregion

        #region initialization

        static BoxFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["BoxId"];
            ClientSecret = ConfigurationManager.AppSettings["BoxSecret"];
            RedirectUri = ConfigurationManager.AppSettings["BoxRedirectUri"];
        }

        #endregion

        #region object model

        public override string[] AllowedDirectorySortFields => new string[] { "Name", "Size", "CreatedDate" };
        public override string[] AllowedFileSortFields => new string[] { "Name", "Size", "CreatedDate" };

        #endregion

        #region authentication

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var loginUrl = new Uri(BoxClient.GetRequestUrl(BoxFileSystem.ClientId, null, RedirectUri));

            var result = await authenticationBroker.WebAuthenticationBrokerAsync(loginUrl,
                new Uri(RedirectUri));

            var fragments = UriEx.ProcessFragments(result.Query);
            string code;
            if (fragments.TryGetValue("code", out code))
            {
                var token = await BoxClient.ExchangeCodeForAccessTokenAsync(code, BoxFileSystem.ClientId, BoxFileSystem.ClientSecret, RedirectUri);
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn) };
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
                var token = await BoxClient.RefreshAccessTokenAsync(refreshToken, ClientId, ClientSecret, cancellationToken);
                return new AuthenticatonTicket() { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn) };
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
                return DirPathMode.DirIdAsId;
            }
        }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            return GetFolderPath((directory as BoxDirectory).ParentDirId);
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            return GetFolderPath((file as BoxFile).ParentDirId);
        }

        protected override async Task<FileSystemDrive> GetDriveAsyncOverride(CancellationToken cancellationToken)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var user = await client.GetCurrentUser(cancellationToken);
            return new FileSystemDrive(user.SpaceUsed, user.SpaceAmount, user.MaxUploadSize);
        }

        protected override async Task<IList<FileSystemItem>> GetItemsAsync(string dirId, CancellationToken cancellationToken)
        {
            var pagedList = new PagedList<FileSystemItem>(
                async (pageIndex, pageSize, ct) =>
                {
                    try
                    {
                        var client = new BoxClient(await GetAccessTokenAsync(null, true, ct));
                        var items = await client.GetFolderItemsAsync(GetFolderId(dirId), pageSize, pageIndex.ToString(), _filesFields, ct);
                        var items2 = ProcessItemsList(client, items);
                        return new Tuple<int, IList<FileSystemItem>>(items.TotalCount, items2);
                    }
                    catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                })
            { AddToTheBeginning = true };
            await pagedList.LoadAsync();
            return pagedList.ToList();
        }

        protected override Task<FileSystemDirectory> GetDirectoryAsyncOverride(string dirId, bool full, CancellationToken cancellationToken)
        {
            return GetFolder(dirId, cancellationToken);
        }

        private async Task<FileSystemDirectory> GetFolder(string dirId, CancellationToken cancellationToken)
        {
            if (await GetAccessTokenAsync(null, true, cancellationToken) != null)
            {
                var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
                var item = await client.GetFolderAsync(GetFolderId(dirId), _filesFields);
                return ConvertItem(client, item) as FileSystemDirectory;
            }
            return null;
        }
        private List<FileSystemItem> ProcessItemsList(BoxClient client, ItemCollection items)
        {
            var res = new List<FileSystemItem>();
            if (items.Entries != null)
            {
                foreach (var item in items.Entries)
                {
                    try
                    {
                        var i = ConvertItem(client, item);
                        res.Add(i);
                    }
                    catch { }
                }
            }
            return res;
        }

        private FileSystemItem ConvertItem(BoxClient client, Item item)
        {
            if (item.Type == "folder")
            {
                return new BoxDirectory(item);
            }
            else
            {
                return new BoxFile(item);
            }
        }

        protected override async Task<bool> CanOpenFileThumbnailAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var file = await GetFileAsync(fileId, false, cancellationToken);
            return file != null && MimeType.Parse(file.ContentType).Type == "image";
        }

        protected override async Task<Stream> OpenFileThumbnailAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            return await client.GetThumbnailAsync(fileId, maxWidth: 128);
        }

        #endregion

        #region upload

        protected override Task<bool> CanWriteFileAsyncOverride(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId,
            FileSystemFile item,
            Stream fileStream,
            IProgress<StreamProgress> progress,
            CancellationToken cancellationToken)
        {
            var file = new Item
            {
                Name = item.Name,
                Parent = new Item { Id = GetFolderId(dirId) },
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var uploadedFile = (await client.UploadFileAsync(file, fileStream, progress, cancellationToken)).Entries.First();
            return ConvertItem(client, uploadedFile) as FileSystemFile;
        }

        #endregion

        #region download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var file = await client.DownloadFileAsync(fileId, cancellationToken);
            return file;
        }

        #endregion

        #region delete

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            await client.DeleteFolderAsync(dirId, cancellationToken: cancellationToken);
            return null;
        }

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            await client.DeleteFileAsync(fileId, cancellationToken: cancellationToken);
            return null;
        }

        #endregion

        #region create

        protected override Task<bool> CanCreateDirectoryOverride(string parentDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string parentDirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var newFolder = new Item
            {
                Name = item.Name,
                Parent = new Item { Id = GetFolderId(parentDirId) },
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var folder = await client.CreateFolderAsync(newFolder, cancellationToken: cancellationToken);
            return new BoxDirectory(folder);
        }

        #endregion

        #region copy

        protected override Task<bool> CanCopyDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> CopyDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var folder = new Item
            {
                Id = sourceDirId,
                Name = directory.Name,
                Parent = new Item { Id = GetFolderId(targetDirId) },
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var item = await client.CopyFolderAsync(folder, cancellationToken: cancellationToken);
            return ConvertItem(client, item) as FileSystemDirectory;
        }

        protected override Task<bool> CanCopyFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> CopyFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var f = new Item
            {
                Id = Path.GetFileName(sourceFileId),
                Name = file.Name,
                Parent = new Item { Id = GetFolderId(targetDirId) },
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var item = await client.CopyFileAsync(f, cancellationToken: cancellationToken);
            return ConvertItem(client, item) as FileSystemFile;
        }

        #endregion

        #region move

        protected override Task<bool> CanMoveDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> MoveDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var folder = new Item
            {
                Id = sourceDirId,
                Name = directory.Name,
                Parent = new Item { Id = GetFolderId(targetDirId) },
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var f = await client.UpdateFolderAsync(folder, cancellationToken: cancellationToken);
            return ConvertItem(client, f) as FileSystemDirectory;
        }

        protected override Task<bool> CanMoveFileOverride(string sourceFilePath, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var f = new Item
            {
                Id = sourceFileId,
                Name = file.Name,
                Parent = new Item { Id = GetFolderId(targetDirId) },
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var f2 = await client.UpdateFileAsync(f, cancellationToken: cancellationToken);
            return ConvertItem(client, f2) as FileSystemFile;
        }

        #endregion

        #region update

        protected override Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var folder = new Item
            {
                Id = dirId,
                Name = item.Name,
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var directory = await client.UpdateFolderAsync(folder, cancellationToken: cancellationToken);
            return new BoxDirectory(directory);
        }

        protected override Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile item, CancellationToken cancellationToken)
        {
            var folder = new Item
            {
                Id = Path.GetFileName(fileId),
                Name = item.Name,
            };
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var directory = await client.UpdateFileAsync(folder, cancellationToken: cancellationToken);
            return new BoxFile(directory);
        }

        #endregion

        #region search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, cancellationToken));
            var items = await client.SearchAsync(query/*, pageSize, pageIndex * pageSize*/, cancellationToken: cancellationToken);
            var searchResult = new List<FileSystemSearchItem>();
            foreach (var item in items.Entries)
            {
                var file = ConvertItem(client, item);
                string parentDirId;
                if (file.IsDirectory)
                {
                    parentDirId = item.Id;
                }
                else
                {
                    parentDirId = item.Parent.Id == "0" ? "" : item.Parent.Id;
                }
                searchResult.Add(new FileSystemSearchItem { DirectoryId = parentDirId, Item = file });
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region social extension

        public event EventHandler CommentsChanged;

        private void OnCommentsChanged()
        {
            CommentsChanged?.Invoke(this, new EventArgs());
        }

        public Task<FileSystemPerson> GetCurrentUserAsync(string dirId)
        {
            return Task.FromException<FileSystemPerson>(new NotImplementedException());
        }

        public bool CanAddComment(string fileId)
        {
            return true;
        }

        public async Task<IDataCollection<FileSystemComment>> GetCommentsAsync(string fileId)
        {
            try
            {
                var client = new BoxClient(await GetAccessTokenAsync(null, true, CancellationToken.None));
                var comments = await client.GetCommentsAsync(Path.GetFileName(fileId), _commentsFields);
                return comments.Entries.Select(
                    c => new FileSystemComment
                    {
                        Id = c.Id,
                        Message = c.Message,
                        From = new BoxPerson(c.CreatedBy),
                        CreatedTime = DateTime.Parse(c.CreatedAt, CultureInfo.InvariantCulture.DateTimeFormat),
                    }).AsDataCollection();
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        public async Task AddCommentAsync(string fileId, string message)
        {
            var client = new BoxClient(await GetAccessTokenAsync(null, true, CancellationToken.None));
            var comment = new Comment
            {
                Message = message,
                Item = new Item
                {
                    Type = "file",
                    Id = Path.GetFileName(fileId),
                }
            };
            await client.AddCommentAsync(comment);
        }

        public bool CanThumbUp(string fileId)
        {
            return false;
        }

        public Task AddThumbUp(string fileId)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task RemoveThumbUp(string fileId)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task<IDataCollection<FileSystemPerson>> GetThumbsUpAsync(string fileId)
        {
            return Task.FromException<IDataCollection<FileSystemPerson>>(new NotImplementedException());
        }

        #endregion

        #region implementation

        private string GetFolderId(string dirId)
        {
            return string.IsNullOrWhiteSpace(dirId) ? _rootFolderId : dirId;
        }

        private string GetFolderPath(string dirId)
        {
            return dirId == _rootFolderId ? "" : dirId;
        }

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            var bExc = exc as BoxException;
            if (bExc != null)
            {
                if (bExc.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    return Task.FromResult<Exception>(new AccessDeniedException());
                if (bExc.Error != null && bExc.Error.Code == "item_name_in_use")
                    return Task.FromResult<Exception>(new DuplicatedItemException(exc.Message));
            }
            return Task.FromResult(ProcessOAuthException(exc));
        }

        #endregion
    }
}
