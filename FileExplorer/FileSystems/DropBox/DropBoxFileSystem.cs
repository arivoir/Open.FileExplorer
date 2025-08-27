using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using C1.DataCollection;
using Open.DropBox;
using Open.FileSystemAsync;
using Open.IO;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class DropBoxFileSystem : UnifiedItemsFileSystem, ISearchExtension
    {
        #region fields

        public static string ClientId { get; private set; }
        public static string ClientSecret { get; private set; }
        public static string RedirectUri { get; private set; }

        private const long MAX_UPLOAD_SIZE = 5 * 1024 * 1024;

        #endregion

        #region initialization

        static DropBoxFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["DropBoxToken"];
            ClientSecret = ConfigurationManager.AppSettings["DropBoxSecret"];
            RedirectUri = ConfigurationManager.AppSettings["DropBoxRedirectUri"];
        }

        #endregion

        #region object model

        protected override bool IsFileNameExtensionRequired
        {
            get
            {
                return true;
            }
        }

        public override string[] AllowedDirectorySortFields => new string[] { "Name" };
        public override string[] AllowedFileSortFields => new string[] { "Name", "Size", "ModifiedDate" };

        #endregion

        #region authentication

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var authenticationUrl = new Uri(DropBoxClient.GetRequestUrl(DropBoxFileSystem.ClientId, RedirectUri), UriKind.Absolute);

            var result = await authenticationBroker.WebAuthenticationBrokerAsync(authenticationUrl, new Uri(RedirectUri));

            var fragments = UriEx.ProcessFragments(result.Fragment);
            string code;
            string error;
            if (fragments.TryGetValue("access_token", out code))
            {
                return new AuthenticatonTicket { AuthToken = code };
            }
            else if (fragments.TryGetValue("error", out error))
            {
                string description;
                if (fragments.TryGetValue("error_description", out description))
                    throw new OperationCanceledException(description);
                else
                    throw new OperationCanceledException();
            }
            else
            {
                throw new Exception("code not found.");
            }
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string connectionString, CancellationToken cancellationToken)
        {
            try
            {
                var client = new DropBoxClient(connectionString);
                var accountInfo = await client.GetCurrentAccountAsync(cancellationToken);
            }
            catch (DropboxException exc)
            {
                throw ProcessException(exc);
            }
            return new AuthenticatonTicket { AuthToken = connectionString };
        }

        #endregion

        #region get info

        protected override async Task<FileSystemDrive> GetDriveAsyncOverride(CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var space = await client.GetSpaceUsage(cancellationToken);
            return new FileSystemDrive((long)space.Used, (long)space.Allocation.Allocated, null);
        }

        protected override async Task<IList<FileSystemItem>> GetItemsAsync(string dirId, CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var folderContent = await client.ListFolderAsync(DropBoxClient.GetValidPath(dirId), CancellationToken.None);
            return ProcessItemsList(client, folderContent.Entries);
        }

        protected override async Task<bool> CanOpenFileThumbnailAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var file = await GetFileAsync(fileId, false, cancellationToken);
            if (file != null)
            {
                return file.Size < 20000000 && MimeType.Parse(file.ContentType).Type == "image";
            }
            return false;
        }

        protected override async Task<Stream> OpenFileThumbnailAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            return await client.GetThumbnailAsync(DropBoxClient.GetValidPath(fileId), size: "w128h128");
        }

        private List<FileSystemItem> ProcessItemsList(DropBoxClient client, IList<Item> items)
        {
            var res = new List<FileSystemItem>();
            foreach (var item in items)
            {
                var i = ConvertItem(client, item);
                res.Add(i);
            }
            return res;
        }

        private FileSystemItem ConvertItem(DropBoxClient client, Item item)
        {
            if (item.Tag == "folder")
            {
                return new DropBoxDirectory(item);
            }
            else
            {
                return new DropBoxFile(item);
            }
        }

        #endregion

        #region upload

        protected override Task<bool> CanWriteFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile item, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var path = DropBoxClient.GetValidPath(dirId, item.Name);
            var streamLength = fileStream.Length;
            if (streamLength > MAX_UPLOAD_SIZE)
            {
                long offset = 0;
                long size = MAX_UPLOAD_SIZE;
                var progress1 = new Progress<StreamProgress>(p =>
                {
                    progress?.Report(new StreamProgress(offset + p.Bytes, streamLength));
                });
                var session = await client.UploadSessionStartAsync(new StreamPartition(fileStream, offset, size), progress1, cancellationToken);
                offset += size;
                while (streamLength - offset > MAX_UPLOAD_SIZE)
                {
                    await client.UploadSessionAppendAsync(session.SessionId, new StreamPartition(fileStream, offset, size), offset, progress1, cancellationToken);
                    offset += size;
                }
                size = streamLength - offset;
                var file = await client.UploadSessionFinishAsync(session.SessionId, path, new StreamPartition(fileStream, offset, size), offset, progress1, false, cancellationToken);
                return new DropBoxFile(file);
            }
            else
            {
                var file = await client.UploadFileAsync(path, fileStream, progress, false, cancellationToken);
                return new DropBoxFile(file);
            }
        }

        #endregion

        #region download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var file = await client.DownloadFileAsync(DropBoxClient.GetValidPath(fileId), cancellationToken);
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
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            await client.DeleteAsync(DropBoxClient.GetValidPath(dirId), cancellationToken);
            return null;
        }

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            await client.DeleteAsync(DropBoxClient.GetValidPath(fileId), cancellationToken);
            return null;
        }

        #endregion

        #region create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var newDirectoryPath = DropBoxClient.GetValidPath(DropBoxClient.GetValidPath(dirId), item.Name);
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var folder = await client.CreateFolderAsync(newDirectoryPath, cancellationToken);
            return new DropBoxDirectory(folder);
        }

        #endregion

        #region copy

        protected override Task<bool> CanCopyDirectoryOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> CopyDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var folderName = directory != null ? directory.Name : Path.GetFileName(sourceDirId);
            var targetDirPath = DropBoxClient.GetValidPath(targetDirId, folderName);
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var folder = await client.CopyAsync(DropBoxClient.GetValidPath(sourceDirId), targetDirPath, cancellationToken);
            return new DropBoxDirectory(folder);
        }

        protected override Task<bool> CanCopyFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> CopyFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var fileName = file != null ? file.Name : Path.GetFileName(sourceFileId);
            var targetFilePath = DropBoxClient.GetValidPath(targetDirId, fileName);
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var f = await client.CopyAsync(DropBoxClient.GetValidPath(sourceFileId), targetFilePath, cancellationToken);
            return new DropBoxFile(f);
        }

        #endregion

        #region move

        protected override Task<bool> CanMoveDirectoryOverride(string sourceDirId, string destDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(sourceDirId != destDirId);
        }

        protected override async Task<FileSystemDirectory> MoveDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var targetPath = Path.Combine(targetDirId, directory != null ? directory.Name : Path.GetFileName(sourceDirId));
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var d = await client.MoveAsync(DropBoxClient.GetValidPath(sourceDirId), DropBoxClient.GetValidPath(targetPath), cancellationToken);
            return new DropBoxDirectory(d);
        }

        protected override Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var sourceFileName = file != null ? file.Name : Path.GetFileName(sourceFileId);
            var sourceParentPath = Path.GetParentPath(sourceFileId);
            var targetFilePath = DropBoxClient.GetValidPath(DropBoxClient.GetValidPath(targetDirId), sourceFileName);

            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var f = await client.MoveAsync(DropBoxClient.GetValidPath(sourceFileId), targetFilePath, cancellationToken);
            return new DropBoxFile(f);
        }

        #endregion

        #region update

        protected override Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var sourceParentPath = Path.GetParentPath(dirId);
            var sourceDirPath = dirId;
            var targetDirPath = Path.Combine(sourceParentPath, item.Name);

            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var directory = await client.MoveAsync(DropBoxClient.GetValidPath(sourceDirPath), DropBoxClient.GetValidPath(targetDirPath), cancellationToken);
            return new DropBoxDirectory(directory);
        }

        protected override Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile item, CancellationToken cancellationToken)
        {
            var sourceParentPath = Path.GetParentPath(fileId);
            var sourceDirPath = fileId;
            var targetDirPath = Path.Combine(sourceParentPath, item.Name);

            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var file = await client.MoveAsync(DropBoxClient.GetValidPath(sourceDirPath), DropBoxClient.GetValidPath(targetDirPath), cancellationToken);
            return new DropBoxFile(file);
        }

        #endregion

        #region search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var client = new DropBoxClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.SearchAsync(query, dirId, cancellationToken: cancellationToken);
            var searchResult = new List<FileSystemSearchItem>();
            foreach (var item in result.Matches)
            {
                var file = ConvertItem(client, item.Metadata);
                searchResult.Add(new FileSystemSearchItem { DirectoryId = Path.GetParentPath(item.Metadata.PathLower), Item = file });
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region implementation


        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        private static Exception ProcessException(Exception exc)
        {
            var dropboxException = exc as DropboxException;
            if (dropboxException?.Error?.ErrorInfo?.Tag == "invalid_access_token")
            {
                return new AccessDeniedException();
            }
            if (exc.Message == "Access token does not belong to this app." ||
                exc.Message == "Access token not valid." ||
                exc.Message == "The given OAuth 2 access token doesn't exist or has expired.")
            {
                return new AccessDeniedException();
            }
            else if (exc.Message == "Path must not be empty")
            {
                return new ArgumentNullException("Name");
            }
            else if (exc.Message.StartsWith("path/conflict"))
            {
                return new DuplicatedItemException(exc.Message);
            }
            else
            {
                return exc;
            }
        }

        #endregion
    }
}
