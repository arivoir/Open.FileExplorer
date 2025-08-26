using C1.DataCollection;
using Open.FileSystemAsync;
using Open.Google;
using Open.GoogleDrive;
using Open.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class GoogleDriveFileSystem : AuthenticatedFileSystem, ISearchExtension
    {
        #region ** fields

        public static string ClientId { get; private set; }
        public static string ClientSecret { get; private set; }
        public static string Scopes { get; private set; }
        public static string RedirectUri { get; private set; }

        private const string _folderMimeType = "application/vnd.google-apps.folder";
        private const string _fileFields = "id,name,createdTime,modifiedTime,parents,mimeType,thumbnailLink,webContentLink,webViewLink,size,imageMediaMetadata(width,height,location),shared";
        private const string _filesFields = "nextPageToken,files(" + _fileFields + ")";
        //private const string _directoriesFields = "nextPageToken,files(id,name,createdTime,modifiedTime,parents,mimeType,webViewLink,shared)";
        //private const string _searchFields = "nextPageToken,files(id,name,createdTime,modifiedTime,parents,mimeType,thumbnailLink,webContentLink,webViewLink,size,shared)";
        private const string _aboutFields = "user,storageQuota,maxUploadSize";

        private About _about;
        private File _rootFolder;

        #endregion

        #region ** initialization

        static GoogleDriveFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["GoogleId"];
            ClientSecret = ConfigurationManager.AppSettings["GoogleSecret"];
            Scopes = ConfigurationManager.AppSettings["GoogleDriveScopes"];
            RedirectUri = ConfigurationManager.AppSettings["GoogleRedirectUri"];
        }

        public GoogleDriveFileSystem()
        {
            RootDirectory = new GoogleDriveDirectory(Root, GoogleDriveResources.MyDriveLabel);
            SharedWithMeDirectory = new GoogleDriveDirectory(SharedWithMe, GoogleDriveResources.SharedWithMeLabel);
            StarredDirectory = new GoogleDriveDirectory(Starred, GoogleDriveResources.StarredLabel);
            TrashedDirectory = new GoogleDriveDirectory(Trashed, GoogleDriveResources.TrashLabel);
        }

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            await GetAboutAsync(cancellationToken, ticket);
            return ticket;
        }

        private SemaphoreSlim _getAboutSemaphore = new SemaphoreSlim(1);
        private async Task GetAboutAsync(CancellationToken cancellationToken, AuthenticatonTicket ticket)
        {
            if (_about == null)
            {
                try
                {
                    await _getAboutSemaphore.WaitAsync();
                    if (_about == null)
                    {
                        var client = new GoogleDriveClient(ticket.AuthToken);
                        _about = await client.GetAbout(_aboutFields, cancellationToken);
                        _rootFolder = await client.GetFileAsync(Root, "id", cancellationToken);
                    }
                }
                finally { _getAboutSemaphore.Release(); }
            }
        }

        protected override Task RefreshAsyncOverride(string dirId = null)
        {
            if (string.IsNullOrWhiteSpace(dirId))
                _about = null;
            return Task.FromResult(true);
        }

        #endregion

        #region ** object model

        public const string Root = "root";
        public const string SharedWithMe = "sharedWithMe";
        public const string Starred = "starred";
        public const string Trashed = "trashed";
        public const string Photos = "photos";

        public override Task<string> GetTrashId(string relativeDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Trashed);
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

        public override string[] GetScopes(string dirId)
        {
            return new string[] { Scopes };
        }

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var callbackUrl = RedirectUri;
            var authenticationUrl = new Uri(GoogleClient.GetRequestUrl(ClientId, Scopes, callbackUrl), UriKind.Absolute);

            var result = await authenticationBroker.WebAuthenticationBrokerAsync(authenticationUrl, new Uri(callbackUrl));

            var fragments = UriEx.ProcessFragments(result.Query);
            string code;
            if (fragments.TryGetValue("code", out code))
            {
                var token = await GoogleClient.ExchangeCodeForAccessTokenAsync(code,
                    GoogleDriveFileSystem.ClientId,
                    GoogleDriveFileSystem.ClientSecret,
                    callbackUrl);
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn), GrantedScopes = new string[] { GoogleDriveFileSystem.Scopes } };
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
                var token = await GoogleDriveClient.RefreshAccessTokenAsync(refreshToken, ClientId, ClientSecret, cancellationToken);
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn), GrantedScopes = new string[] { GoogleDriveFileSystem.Scopes } };
            }
            catch (Exception exc)
            {
                throw ProcessOAuthException(exc);
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

        protected override UniqueFileNameMode UniqueFileNameMode
        {
            get
            {
                return UniqueFileNameMode.DirName_FileId_Extension;
            }
        }

        public FileSystemDirectory RootDirectory { get; private set; }
        public FileSystemDirectory SharedWithMeDirectory { get; private set; }
        public FileSystemDirectory StarredDirectory { get; private set; }
        public FileSystemDirectory TrashedDirectory { get; private set; }

        protected override bool CacheDirectoriesMetadata(string dirId)
        {
            return !string.IsNullOrWhiteSpace(dirId) && dirId != Trashed;
        }

        protected override bool CacheFilesMetadata(string dirId)
        {
            return !string.IsNullOrWhiteSpace(dirId) && dirId != Trashed;
        }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            var gDir = directory as GoogleDriveDirectory;
            if (gDir.IsTrashed)
            {
                return Trashed;
            }
            else if (gDir.Parents == null)
            {
                return "";
            }
            else if (gDir.Parents.Any(p => p == _rootFolder.Id))
            {
                return Root;
            }
            else if (gDir.Parents.Count() > 0)
            {
                return gDir.Parents.First();
            }
            else if (gDir.Shared)
            {
                return SharedWithMe;
            }
            return null;
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            var gFile = file as GoogleDriveFile;
            if (gFile.IsTrashed)
            {
                return Trashed;
            }
            else
            if (gFile.Parents == null)
            {
                return "";
            }
            else if (gFile.Parents.Any(p => p == _rootFolder.Id))
            {
                return Root;
            }
            else if (gFile.Parents.Count() > 0)
            {
                return gFile.Parents.First();
            }
            return null;
        }

        protected override async Task<FileSystemDrive> GetDriveAsyncOverride(CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var about = await client.GetAbout(_aboutFields, cancellationToken);
            return new FileSystemDrive(about.StorageQuota.Usage, about.StorageQuota.Limit, about.MaxUploadSize);
        }

        protected override Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            IDataCollection<FileSystemDirectory> result;
            if (string.IsNullOrWhiteSpace(dirId))
            {
                var directories = new List<FileSystemDirectory>();
                directories.Add(RootDirectory);
                directories.Add(SharedWithMeDirectory);
                directories.Add(StarredDirectory);
                directories.Add(TrashedDirectory);
                result = directories.AsDataCollection();
            }
            else if (dirId == Photos)
            {
                result = new EmptyCollectionView<FileSystemDirectory>();
            }
            else
            {
                result = new CursorList<FileSystemDirectory>(
                    async (maxResults, pageToken, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.GetFilesAsync(GetDirQuery(dirId), _filesFields, maxResults, pageToken, GetOrderBy(sd), cancellationToken: ct);
                            var directories = new List<FileSystemDirectory>();
                            foreach (var directory in r.Items)
                            {
                                var d = ConvertFile(directory, dirId) as GoogleDriveDirectory;
                                directories.Add(d);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(r.NextPageToken, directories);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    }, CanSort);
            }
            return Task.FromResult(result);
        }

        protected override Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            IDataCollection<FileSystemFile> result;
            if (!string.IsNullOrWhiteSpace(dirId))
            {
                result = new CursorList<FileSystemFile>(
                    async (maxResults, pageToken, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.GetFilesAsync(GetFileQuery(dirId), _filesFields, maxResults, pageToken, GetOrderBy(sd), GetSpaces(dirId), ct);
                            var files = new List<FileSystemFile>();
                            foreach (var file in r.Items)
                            {
                                var f = ConvertFile(file, dirId) as GoogleDriveFile;
                                files.Add(f);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemFile>>(r.NextPageToken, files);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    }, CanSort);
            }
            else
            {
                result = new EmptyCollectionView<FileSystemFile>();
            }
            return Task.FromResult(result);
        }

        private bool CanSort(IReadOnlyList<SortDescription> sortDescriptions)
        {
            if (sortDescriptions == null)
                return true;
            foreach (var sortDescription in sortDescriptions)
            {
                if (sortDescription.SortPath != "Name" && sortDescription.SortPath != "Size" && sortDescription.SortPath != "ModifiedDate")
                    return false;
            }
            return true;
        }

        private string GetOrderBy(IReadOnlyList<SortDescription> sd)
        {
            if (sd == null || sd.Count == 0)
                return null;
            var orderBy = "";
            foreach (var sortDescription in sd)
            {
                var order = sortDescription.Direction == SortDirection.Ascending ? "" : " desc";
                switch (sortDescription.SortPath)
                {
                    case "Name":
                        orderBy += "name" + order;
                        break;
                    case "Size":
                        orderBy += "quotaBytesUsed" + order;
                        break;
                    case "ModifiedDate":
                        orderBy += "modifiedTime" + order;
                        break;
                }
            }
            return orderBy;
        }

        protected override async Task<FileSystemDirectory> GetDirectoryAsyncOverride(string dirId, bool full, CancellationToken cancellationToken)
        {
            switch (dirId)
            {
                case Root:
                    return RootDirectory;
                case SharedWithMe:
                    return SharedWithMeDirectory;
                case Starred:
                    return StarredDirectory;
                case Trashed:
                    return TrashedDirectory;
                default:
                    var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
                    var result = await client.GetFileAsync(dirId, _fileFields, cancellationToken);
                    return ConvertFile(result, dirId) as GoogleDriveDirectory;
            }
        }

        private static string GetDirQuery(string dirId)
        {
            switch (dirId)
            {
                case SharedWithMe:
                    return string.Format("mimeType = '{0}' and sharedWithMe", _folderMimeType);
                case Starred:
                    return string.Format("mimeType = '{0}' and starred", _folderMimeType);
                case Trashed:
                    return string.Format("mimeType = '{0}' and trashed", _folderMimeType);
                default:
                    return string.Format("mimeType = '{0}' and '{1}' in parents and trashed=false", _folderMimeType, dirId);
            }
        }

        private static string GetFileQuery(string dirId)
        {
            switch (dirId)
            {
                case SharedWithMe:
                    return string.Format("mimeType != '{0}' and sharedWithMe", _folderMimeType);
                case Photos:
                    return string.Format("mimeType != '{0}'", _folderMimeType);
                case Starred:
                    return string.Format("mimeType != '{0}' and starred", _folderMimeType);
                case Trashed:
                    return string.Format("mimeType != '{0}' and trashed", _folderMimeType);
                default:
                    return string.Format("mimeType != '{0}' and '{1}' in parents and trashed=false", _folderMimeType, dirId);
            }
        }

        private string GetSpaces(string dirId)
        {
            if (dirId == Photos)
                return Photos;
            return null;
        }

        public static bool IsInRoot(string dirId, bool acceptRoot = true)
        {
            return dirId != "" && dirId != SharedWithMe && dirId != Starred && dirId != Trashed && (acceptRoot || dirId != Root);
        }

        public static bool IsTrashDir(string dirId)
        {
            return dirId == Trashed;
        }

        public static bool IsStarredDir(string dirId)
        {
            return dirId == Starred;
        }

        public static bool IsSharedWithMeDir(string dirId)
        {
            return dirId == SharedWithMe;
        }

        private FileSystemItem ConvertFile(File file, string dirId = null)
        {
            if (file.MimeType == _folderMimeType)
            {
                var d = new GoogleDriveDirectory(file);
                d.IsTrashed = dirId == Trashed;
                return d;
            }
            else
            {

                var f = new GoogleDriveFile(file, _about.User);
                f.IsTrashed = dirId == Trashed;
                return f;
            }
        }

        #endregion

        #region ** download

        protected override async Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var parentId = await GetFileParentIdAsync(fileId, cancellationToken);
            if (IsTrashDir(parentId))
                return false;
            var file = await GetFileAsync(fileId, false, cancellationToken) as GoogleDriveFile;
            return file != null && file.DownloadUri != null;
        }

        protected override async Task<System.IO.Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            return await client.DownloadFileAsync(fileId, cancellationToken);
        }

        #endregion

        #region ** upload

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsInRoot(dirId));
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string path, FileSystemFile item, System.IO.Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var collectionId = Path.GetFileName(path);
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var dirId = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(dirId))
                dirId = Root;
            var file = new File
            {
                Name = item.Name,
                MimeType = item.ContentType,
                Parents = new string[] { dirId }
            };
            var length = fileStream.GetLength();
            if (length.HasValue && length > (5 * 1024 * 1024) && fileStream.CanSeek && !string.IsNullOrWhiteSpace(item.ContentType))
            {
                var resumableSessionUri = await client.UploadResumableFileAsync(file, length.Value, _fileFields, cancellationToken);
                long offset = 0;
                int retries = 0;
                var rand = new Random();
            sendFile:
                try
                {
                    var partitionLength = length.Value - offset;// Math.Min(length.Value - offset, 1024 * 1024);
                    var partition = new StreamPartition(fileStream, offset, partitionLength);
                    var tuple = await client.SendResumableFileAsync(resumableSessionUri, item.ContentType, partition, offset, offset + partitionLength - 1, length.Value, progress, cancellationToken);
                    if (tuple.Item1 != null)
                    {
                        return new GoogleDriveFile(tuple.Item1, _about.User);
                    }
                    else
                    {
                        offset = tuple.Item2.To.Value + 1;
                        goto sendFile;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (GoogleDriveException exc)
                {
                    if (retries < 5 &&
                        (exc.StatusCode == HttpStatusCode.InternalServerError ||
                        exc.StatusCode == HttpStatusCode.BadGateway ||
                        exc.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        exc.StatusCode == HttpStatusCode.GatewayTimeout))
                    {

                        var timeSpan = Math.Pow(2, retries) * 1000 + rand.Next(0, 1000);
                        await Task.Delay(TimeSpan.FromMilliseconds(timeSpan));
                        var tuple = await client.GetResumableFileRangeAsync(resumableSessionUri, length.Value, cancellationToken);
                        if (tuple.Item1 != null)
                        {
                            return new GoogleDriveFile(tuple.Item1, _about.User);
                        }
                        else
                        {
                            offset = tuple.Item2.To.Value;
                            fileStream.Seek(offset, System.IO.SeekOrigin.Begin);
                            retries++;
                            goto sendFile;
                        }
                    }
                    else
                    {
                        throw;
                    }
                }

            }
            else
            {
                var fileResp = await client.UploadMultipartFileAsync(file, fileStream, progress, _fileFields, cancellationToken);
                return new GoogleDriveFile(fileResp, _about.User);
            }
        }

        #endregion

        #region ** create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsInRoot(dirId));
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var newFolder = new File
            {
                Name = item.Name,
                MimeType = _folderMimeType,
                Parents = new string[] { string.IsNullOrWhiteSpace(dirId) ? Root : dirId }
            };
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.InsertFileAsync(newFolder, _fileFields, cancellationToken);
            var directory = new GoogleDriveDirectory(result);
            return directory;
        }

        #endregion

        #region ** copy

        protected override async Task<bool> CanCopyFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            var sourceDirId = await GetFileParentIdAsync(sourceFileId, cancellationToken);
            return IsInRoot(sourceDirId) && IsInRoot(targetDirId);
        }

        protected override async Task<FileSystemFile> CopyFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetDirId))
                targetDirId = Root;

            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var newFile = new File { Parents = new string[] { targetDirId } };
            var result = await client.CopyFileAsync(Path.GetFileName(sourceFileId), newFile, _fileFields, cancellationToken);
            return new GoogleDriveFile(result, _about.User);
        }

        #endregion

        #region ** move

        protected override async Task<bool> CanMoveDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            var sourceParentId = await GetDirectoryParentIdAsync(sourceDirId, cancellationToken);
            return IsInRoot(sourceParentId) && IsInRoot(targetDirId);
        }

        protected override async Task<FileSystemDirectory> MoveDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            var sourceParentDirId = await GetDirectoryParentIdAsync(sourceDirId, cancellationToken);
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            try
            {
                var result = await client.UpdateFileAsync(sourceDirId, fields: _fileFields, addParents: string.IsNullOrWhiteSpace(targetDirId) ? Root : targetDirId, removeParents: sourceParentDirId, cancellationToken: cancellationToken);
                return new GoogleDriveDirectory(result);
            }
            catch (Exception exc)
            {
                if (exc.Message.Contains("already exists"))
                {
                    throw new DuplicatedDirectoryException(exc.Message);
                }
                else
                {
                    throw;
                }
            }
        }

        protected override async Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            var sourceDirId = await GetFileParentIdAsync(sourceFileId, cancellationToken);
            return IsInRoot(sourceDirId) && IsInRoot(targetDirId);
        }

        protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(targetDirId))
                targetDirId = Root;

            var sourceDirId = await GetFileParentIdAsync(sourceFileId, cancellationToken);
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.UpdateFileAsync(sourceFileId, fields: _fileFields, addParents: targetDirId, removeParents: sourceDirId, cancellationToken: cancellationToken);
            return new GoogleDriveFile(result, _about.User);
        }

        #endregion

        #region ** update

        protected override async Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            var parentId = await GetDirectoryParentIdAsync(dirId, cancellationToken);
            return IsInRoot(parentId);
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var newDoc = new File { Name = item.Name };
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.UpdateFileAsync(dirId, newDoc, _fileFields, cancellationToken: cancellationToken);
            return new GoogleDriveDirectory(result);
        }

        protected override async Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            var parentId = await GetFileParentIdAsync(fileId, cancellationToken);
            return IsInRoot(parentId);
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile item, CancellationToken cancellationToken)
        {
            var newDoc = new File { Name = item.Name };
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.UpdateFileAsync(fileId, newDoc, _fileFields, cancellationToken: cancellationToken);
            return new GoogleDriveFile(result, _about.User);
        }


        #endregion

        #region ** delete

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(IsInRoot(dirId, false));
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            if (sendToTrash)
            {
                var updatedFile = new File { Trashed = true };
                var file = await client.UpdateFileAsync(dirId, updatedFile, _fileFields, cancellationToken: cancellationToken);
                return ConvertFile(file, await GetTrashId(dirId, cancellationToken)) as GoogleDriveDirectory;
            }
            else
            {
                await client.DeleteFileAsync(dirId, cancellationToken);
                return null;
            }
        }

        protected override async Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            var dirId = await GetFileParentIdAsync(fileId, cancellationToken);
            return IsInRoot(dirId) || IsTrashDir(dirId);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            if (sendToTrash)
            {
                var updatedFile = new File { Trashed = true };
                var file = await client.UpdateFileAsync(fileId, updatedFile, _fileFields, cancellationToken: cancellationToken);
                return ConvertFile(file, await GetTrashId(await GetFileParentIdAsync(fileId, cancellationToken), cancellationToken)) as GoogleDriveFile;
            }
            else
            {
                await client.DeleteFileAsync(Path.GetFileName(fileId), cancellationToken);
                return null;
            }
        }

        public async Task UntrashDirectoryAsync(string dirId, CancellationToken cancellationToken)
        {
            dirId = Path.NormalizePath(dirId);
            var parentId = await GetDirectoryParentIdAsync(dirId, cancellationToken);
            var directory = await UntrashDirectoryAsyncOverride(dirId, cancellationToken) as GoogleDriveDirectory;

            IDataCollection<FileSystemDirectory> dirs;
            if (DirListCache.TryGetValue(parentId, out dirs))
            {
                var dir = dirs.Where(d => GetDirectoryId(parentId, d.Id) == dirId).FirstOrDefault();
                if (dir != null)
                {
                    var index = dirs.IndexOf(dir);
                    await dirs.RemoveAsync(index);
                }
            }

            var newParentId = GetDirectoryParentId(directory);
            IDataCollection<FileSystemDirectory> subdirectories;
            if (DirListCache.TryGetValue(newParentId, out subdirectories))
            {
                await subdirectories.AddAsync(directory);
            }
        }

        public async Task UntrashFileAsync(string fileId, CancellationToken cancellationToken)
        {
            fileId = Path.NormalizePath(fileId);
            var parentId = await GetFileParentIdAsync(fileId, cancellationToken);
            var file = await UntrashFileAsyncOverride(fileId, cancellationToken) as GoogleDriveFile;

            await RemoveFileFromCache(parentId, fileId);
            var dirId = GetFileParentId(file);
            await AddFileToCache(dirId, file);
        }

        public async Task<FileSystemDirectory> UntrashDirectoryAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var updatedFile = new File { Trashed = false };
            var directory = await client.UpdateFileAsync(dirId, updatedFile, _fileFields, cancellationToken: cancellationToken);
            return ConvertFile(directory) as GoogleDriveDirectory;
        }

        public async Task<FileSystemFile> UntrashFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var updatedFile = new File { Trashed = false };
            var file = await client.UpdateFileAsync(fileId, updatedFile, _fileFields, cancellationToken: cancellationToken);
            return ConvertFile(file) as GoogleDriveFile;
        }

        #endregion

        #region ** search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult<bool>(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var client = new GoogleDriveClient(await GetAccessTokenAsync(true, cancellationToken));
            var q = string.Format("fullText contains '{0}' and trashed=false", query.Replace("'", @"\'"));
            var result = await client.GetFilesAsync(q, _filesFields, cancellationToken: cancellationToken);
            var searchResult = new List<FileSystemSearchItem>();
            foreach (var file in result.Items)
            {
                var f = ConvertFile(file);
                string id;
                if (f.IsDirectory)
                {
                    id = f.Id;
                }
                else
                {
                    id = file.Parents != null && file.Parents.Count() > 0 ? file.Parents.FirstOrDefault() : "";
                }
                searchResult.Add(new FileSystemSearchItem { DirectoryId = id, Item = f });
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region ** implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            var gExc = exc as GoogleDriveException;
            if (gExc != null)
            {
                if (gExc.Error.Errors.Any(e => e.Reason == "authError"))
                {
                    return Task.FromResult<Exception>(new AccessDeniedException());
                }
            }
            return Task.FromResult(ProcessOAuthException(exc));
        }

        #endregion
    }
}
