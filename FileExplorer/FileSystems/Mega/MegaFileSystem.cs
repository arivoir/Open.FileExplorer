using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using Open.Mega;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class MegaFileSystem : AuthenticatedFileSystem, ISearchExtension
    {
        #region ** initialization

        protected override async Task<bool> CheckAccessAsyncOverride(string dirId, bool promptForUserInteraction, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(promptForUserInteraction, cancellationToken);
            return client != null;
        }

        #endregion

        #region ** object model

        public override Task<string> GetTrashId(string relativeDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_trash?.Id);
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

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var provider = new MegaProvider();
            string user = "";
            string password = "";
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                var parts = connectionString.Split(':');
                if (parts.Length == 2)
                {
                    user = parts[0];
                    password = parts[1];
                }
            }
            return await authenticationBroker.FormAuthenticationBrokerAsync(async (server, domain, user2, password2, ignoreCertErrors) =>
            {
                try
                {
                    var client = new MegaClient();
                    await client.Login(user2, password2, CancellationToken.None);
                    return new AuthenticatonTicket { AuthToken = $"{user2}:{password2}", Tag = client };
                }
                catch (Exception exc) { throw ProcessException(exc); }
            }, provider.Name, provider.Color, provider.IconResourceKey, showServer: false, showDomain: false, userNameIsEmail: true, user: user, password: password);
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var parts = refreshToken.Split(':');
                if (parts.Length != 2)
                    throw new AccessDeniedException();
                var user = parts[0];
                var password = parts[1];
                var client = new MegaClient();
                await client.Login(user, password, cancellationToken);
                return new AuthenticatonTicket { AuthToken = $"{user}:{password}", Tag = client };
            }
            catch (Exception exc)
            {
                throw ProcessException(exc);
            }
        }

        private async Task<MegaClient> GetClientAsync(bool promptForUserInteraction, CancellationToken cancellationToken)
        {
            var ticket = await AuthenticateAsync(null, promptForUserInteraction, cancellationToken);
            return ticket.Tag as MegaClient;
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

        protected override UniqueFileNameMode UniqueFileNameMode
        {
            get
            {
                return UniqueFileNameMode.DirName_FileId_Extension;
            }
        }

        public string[] AllowedDirectorySortFields { get => new string[] { "Name" }; }
        public string[] AllowedFileSortFields { get => new string[] { "Name", "Size" }; }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            return (directory as MegaDirectory).Node.ParentId;
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            return (file as MegaFile).Node.ParentId;
        }

        private IEnumerable<FileSystemItem> Items;
        private SemaphoreSlim _getItemsSemaphore = new SemaphoreSlim(1);
        private Node _trash;
        private async Task<IEnumerable<FileSystemItem>> GetAllItems(CancellationToken cancellationToken)
        {
            try
            {
                await _getItemsSemaphore.WaitAsync();
                if (Items == null)
                {
                    Items = await GetAllItemsOverride(cancellationToken);
                }
                return Items;
            }
            finally
            {
                _getItemsSemaphore.Release();
            }
        }

        protected override Task RefreshAsyncOverride(string dirId = null)
        {
            Items = null;
            return Task.FromResult(true);
        }

        private async Task<IEnumerable<FileSystemItem>> GetAllItemsOverride(CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(true, cancellationToken);
            var nodes = (await client.GetNodes(cancellationToken)).ToArray();
            _trash = nodes.FirstOrDefault(n => n.Type == NodeType.Trash);
            var items = nodes.Select(n => n.Type == NodeType.File ? (FileSystemItem)new MegaFile(n) : (FileSystemItem)new MegaDirectory(n)).ToArray();
            return items;
        }

        protected override async Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            var items = await GetAllItems(cancellationToken);
            var directories = items.OfType<FileSystemDirectory>().Where(i => GetItemNode(i).ParentId == dirId);
            if (string.IsNullOrWhiteSpace(dirId))
            {
                return directories.OrderBy(d => GetItemNode(d).Type).AsDataCollection();
            }
            else
            {
                return new CustomSortCollectionView<FileSystemDirectory>(directories.ToList(), AllowedDirectorySortFields);
            }
        }

        private Node GetItemNode(FileSystemItem i)
        {
            return i.IsDirectory ? (i as MegaDirectory).Node : (i as MegaFile).Node;
        }

        protected override async Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dirId))
            {
                return new EmptyCollectionView<FileSystemFile>();
            }
            else
            {
                var items = await GetAllItems(cancellationToken);
                var files = items.OfType<FileSystemFile>().Where(i => GetItemNode(i).ParentId == dirId).ToList();
                return new CustomSortCollectionView<FileSystemFile>(files, AllowedFileSortFields);
            }
        }

        protected override async Task<FileSystemDirectory> GetDirectoryAsyncOverride(string dirId, bool full, CancellationToken cancellationToken)
        {
            var items = await GetAllItems(cancellationToken);
            return items.FirstOrDefault(i => i.Id == dirId) as FileSystemDirectory;
        }

        protected override async Task<FileSystemFile> GetFileAsyncOverride(string fileId, bool full, CancellationToken cancellationToken)
        {
            var items = await GetAllItems(cancellationToken);
            return items.FirstOrDefault(i => i.Id == fileId) as FileSystemFile;
        }
        #endregion

        #region ** download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(true, cancellationToken);
            var file = await GetFileAsync(fileId, false, cancellationToken) as MegaFile;
            return await client.Download(file.Node, cancellationToken);
        }

        #endregion

        #region ** upload

        protected override async Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return !string.IsNullOrWhiteSpace(dirId) && !await IsInTrash(dirId, cancellationToken);
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(true, cancellationToken);
            var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
            var fileResp = await client.Upload(fileStream, file.Name, dir.Node, progress, cancellationToken);
            return new MegaFile(fileResp);
        }

        #endregion

        #region ** create

        protected override async Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return !string.IsNullOrWhiteSpace(dirId) && !await IsInTrash(dirId, cancellationToken);
        }

        private async Task<bool> IsInTrash(string dirId, CancellationToken cancellationToken)
        {
            while (!string.IsNullOrWhiteSpace(dirId))
            {

                var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
                if (dir.Node.Type == NodeType.Trash)
                {
                    return true;
                }
                dirId = await GetDirectoryParentIdAsync(dirId, cancellationToken);
            }
            return false;
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(true, cancellationToken);
            var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
            var result = await client.CreateFolder(item.Name, dir.Node, cancellationToken);
            var directory = new MegaDirectory(result);
            return directory;
        }

        #endregion

        #region ** move

        protected override async Task<bool> CanMoveDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            var sourceDir = await GetDirectoryAsync(sourceDirId, false, cancellationToken) as MegaDirectory;
            var targetDir = await GetDirectoryAsync(targetDirId, false, cancellationToken) as MegaDirectory;
            return sourceDir.Node.Type == NodeType.Directory && targetDir != null;
        }

        protected override async Task<FileSystemDirectory> MoveDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var sourceDir = await GetDirectoryAsync(sourceDirId, false, cancellationToken) as MegaDirectory;
            var targetDir = await GetDirectoryAsync(targetDirId, false, cancellationToken) as MegaDirectory;
            var client = await GetClientAsync(true, cancellationToken);
            var result = await client.Move(sourceDir.Node, targetDir.Node, cancellationToken);
            sourceDir.Node.ParentId = targetDir.Node.Id;
            return sourceDir;
        }

        protected override async Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            var targetDir = await GetDirectoryAsync(targetDirId, false, cancellationToken) as MegaDirectory;
            return targetDir != null;
        }

        protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var sourceFile = await GetFileAsync(sourceFileId, false, cancellationToken) as MegaFile;
            var targetDir = await GetDirectoryAsync(targetDirId, false, cancellationToken) as MegaDirectory;
            var client = await GetClientAsync(true, cancellationToken);
            var result = await client.Move(sourceFile.Node, targetDir.Node, cancellationToken);
            sourceFile.Node.ParentId = targetDir.Node.Id;
            return sourceFile;
        }

        #endregion

        #region ** update

        protected override async Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
            return dir.Node.Type == NodeType.Directory;
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
            var client = await GetClientAsync(true, cancellationToken);
            var result = await client.RenameNode(dir.Node, item.Name, cancellationToken);
            return item;
        }

        protected override Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile item, CancellationToken cancellationToken)
        {
            var file = await GetFileAsync(fileId, false, cancellationToken) as MegaFile;
            var client = await GetClientAsync(true, cancellationToken);
            var result = await client.RenameNode(file.Node, item.Name, cancellationToken);
            return item;
        }

        #endregion

        #region ** delete

        protected override async Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
            return dir.Node.Type == NodeType.Directory;
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(true, cancellationToken);
            if (sendToTrash)
            {
                var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
                var r = await client.Move(dir.Node, _trash, cancellationToken);
                dir.Node.ParentId = _trash.Id;
                return dir;
            }
            else
            {
                var dir = await GetDirectoryAsync(dirId, false, cancellationToken) as MegaDirectory;
                var result = await client.Delete(dir.Node, cancellationToken);
                if (result == 0)
                    return dir;
                else
                    throw new Exception("Folder could not be deleted.");
            }
        }

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = await GetClientAsync(true, cancellationToken);
            if (sendToTrash)
            {
                var file = await GetFileAsync(fileId, false, cancellationToken) as MegaFile;
                var r = await client.Move(file.Node, _trash, cancellationToken);
                file.Node.ParentId = _trash.Id;
                return file;
            }
            else
            {
                var file = await GetFileAsync(fileId, false, cancellationToken) as MegaFile;
                var result = await client.Delete(file.Node, cancellationToken);
                if (result == 0)
                    return file;
                else
                    throw new Exception("File could not be deleted.");
            }
        }

        #endregion

        #region ** search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var items = (await GetAllItems(cancellationToken)).Where(n => n.Name.ToLower().Contains(query.ToLower())).ToArray();
            var searchResult = new List<FileSystemSearchItem>();
            foreach (var item in items)
            {
                string id;
                if (item.IsDirectory)
                {
                    id = item.Id;
                }
                else
                {
                    id = GetItemNode(item).ParentId;
                }
                searchResult.Add(new FileSystemSearchItem { DirectoryId = id, Item = item });
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region ** implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        private static Exception ProcessException(Exception exc)
        {
            var mExc = exc as MegaException;
            if (mExc != null)
            {
                if (mExc.Codes.Contains(-9))
                {
                    return new AccessDeniedException();
                }
            }
            return ProcessOAuthException(exc);
        }

        #endregion
    }
}
