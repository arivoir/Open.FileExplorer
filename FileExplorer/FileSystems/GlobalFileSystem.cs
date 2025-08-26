using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    /// <summary>
    /// Main file system that contains all the connections the user has configured. 
    /// Each connection is mapped to a folder of this file system.
    /// </summary>
    public class GlobalFileSystem : IFileSystemAsync, ISocialExtension, ISearchExtension
    {
        #region ** fields

        //private List<GlobalDirectory> _connections;
        private const string ENCRIPTED_CONNECTIONS_FILE_NAME = "Connections.json.encripted";
        private const string CONNECTIONS_FILE_NAME = "Connections.json";

        #endregion

        #region ** initialization

        public GlobalFileSystem(IAppService appService)
        {
            AppService = appService;
        }

        #endregion

        #region ** object model

        public IAppService AppService { get; private set; }
        private ObservableCollection<AccountDirectory> Accounts { get; set; }

        #endregion

        #region ** load/unload

        public async Task<bool> CheckAccessAsync(string dirId, bool promptIfNecessary, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dirId))
            {
                return true;
            }
            else
            {
                string subPath, providerName;
                var connections = await GetDirectoriesAsync("", cancellationToken);
                var connection = GetConnection(dirId, out providerName, out subPath);
                if (connection?.FileSystem == null)
                    throw new ArgumentException("Provider not found");

                //if (connection.State != GlobalDirectoryState.NotAuthenticated && connection.State != GlobalDirectoryState.AuthenticationFailed)
                //    return true;

                try
                {

                    return await connection.FileSystem.CheckAccessAsync(subPath, promptIfNecessary, cancellationToken);
                }
                catch (Exception exc)
                {
                    if (exc is AccessDeniedException || exc.InnerException is AccessDeniedException)
                    {

                    }
                    else
                    {

                    }
                    throw;
                }
            }
        }

        public async Task InvalidateAccessAsync(string dirId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(dirId))
            {
                string subPath, providerName;
                var connections = await GetDirectoriesAsync("", cancellationToken);
                var connection = GetConnection(dirId, out providerName, out subPath);
                if (connection == null)
                    throw new ArgumentException("Provider not found");


                await connection.FileSystem.InvalidateAccessAsync(subPath, cancellationToken);
            }
        }

        public async Task RefreshAsync(string dirId = null)
        {
            if (Accounts != null)
            {
                if (string.IsNullOrWhiteSpace(dirId))
                {
                    //if (dirId == null)
                    {
                        foreach (AccountDirectory globalDir in Accounts)
                        {
                            await globalDir.FileSystem.RefreshAsync(null);
                            await globalDir.FileSystem.InvalidateAccessAsync(null, CancellationToken.None);
                            ReleaseConnection(globalDir);
                        }
                    }
                    Accounts = null;
                }
                else
                {
                    string subPath, providerName;
                    var connection = GetConnection(dirId, out providerName, out subPath);
                    if (connection != null && connection.FileSystem != null)
                    {
                        await connection.FileSystem.RefreshAsync(subPath);
                    }
                }
            }
            var e = new RefreshedEventArgs(dirId);
            OnRefreshed(e);
            await e.WaitDeferralsAsync();

        }
        protected void OnRefreshed(RefreshedEventArgs e)
        {
            if (Refreshed != null)
            {
                Refreshed(this, e);
            }
        }

        public event EventHandler<RefreshedEventArgs> Refreshed;

        #endregion

        #region ** connections

        public Task<FileSystemDrive> GetDriveAsync(CancellationToken cancellationToken)
        {
            return Task.FromException<FileSystemDrive>(new NotImplementedException());
        }

        public async Task<AccountDirectory> AddConnection(IProvider provider, CancellationToken cancellationToken)
        {
            await GetDirectoriesAsync("", cancellationToken);
            var connections = Accounts;
            var name = provider.Name;

            var newAccount = new AccountDirectory();
            newAccount.Id = Guid.NewGuid().ToString();
            newAccount.ProviderId = provider.ToString();
            newAccount.Name = name;
            newAccount.AuthenticationManager = AppService.GetAuhtenticationManager(newAccount);
            var fileSystem = provider.CreateFileSystem(newAccount.AuthenticationManager);
            newAccount.FileSystem = fileSystem;
            var scopes = fileSystem.GetScopes("");
            var ticket = await newAccount.AuthenticationManager.AddNewAsync(scopes, cancellationToken);
            newAccount.ConnectionString = ticket.RefreshToken ?? ticket.AuthToken;
            newAccount.UserId = ticket.UserId;

            Accounts.Add(newAccount);
            await WriteAccounts(Accounts);
            PrepareConnection(newAccount);
            return newAccount;
        }

        internal async Task AddConnections(List<AccountDirectory> accounts)
        {
            await WriteAccounts(accounts);
            await RefreshAsync("");
        }

        private async Task RemoveConnection(AccountDirectory connection)
        {
            var connections = Accounts;
            var conn = connections.First(ci => ci.Id == connection.Id) as AccountDirectory;
            ReleaseConnection(conn);
            Accounts.Remove(conn);
            await WriteAccounts(Accounts);
        }

        private void PrepareConnection(AccountDirectory connection)
        {
            if (connection.AuthenticationManager == null)
            {
                var authenticationManager = AppService.GetAuhtenticationManager(connection);
                authenticationManager.ConnectionString = connection.ConnectionString;
                connection.AuthenticationManager = authenticationManager;
                connection.AuthenticationManager.ConnectionStringChanged += OnAuthenticationManagerConnectionStringChanged;
            }
            if (connection.FileSystem == null && connection.Provider != null)
            {
                connection.FileSystem = connection.Provider.CreateFileSystem(connection.AuthenticationManager);
            }
            if (connection.FileSystem != null)
            {
                if (connection.FileSystem is ISocialExtension)
                    ((ISocialExtension)connection.FileSystem).CommentsChanged += OnCommentsChanged;
            }
        }

        private void ReleaseConnection(AccountDirectory connection)
        {
            if (connection.AuthenticationManager != null)
                connection.AuthenticationManager.ConnectionStringChanged -= OnAuthenticationManagerConnectionStringChanged;
            if (connection.FileSystem != null)
            {
                if (connection.FileSystem is ISocialExtension)
                    ((ISocialExtension)connection.FileSystem).CommentsChanged -= OnCommentsChanged;
            }
        }

        private async void OnAuthenticationManagerConnectionStringChanged(object sender, AsyncEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                var authenticationManager = sender as AuthenticationManager;
                var account = Accounts.FirstOrDefault(a => a.AuthenticationManager == sender);
                account.ConnectionString = authenticationManager.ConnectionString;
                await WriteAccounts(Accounts);
            }
            finally
            {
                deferral.Complete();
            }
        }

        public async Task<bool> CanShiftDirectory(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (string.IsNullOrWhiteSpace(subPath))
                {
                    return false;
                }
                else if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.CanShiftDirectory(subPath, cancellationToken);
                }
            }
            return false;
        }

        public Task<bool> ShiftDirectoryAsync(string dirId, int targetIndex)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        #endregion

        #region ** private stuff

        public AccountDirectory GetConnection(string dirId)
        {
            string connectionName, subPath;
            return GetConnection(dirId, out connectionName, out subPath);
        }

        /// <summary>
        /// Gets the connection corresponding to the specified path.
        /// </summary>
        /// <param name="path">The specified path.</param>
        /// <param name="connectionName">Name of the connection to which the path belongs.</param>
        /// <param name="subPath">The sub path inside the connection.</param>
        /// <returns></returns>
        public AccountDirectory GetConnection(string path, out string connectionName, out string subPath)
        {
            path = Open.FileSystemAsync.Path.NormalizePath(path);
            var connId = Open.FileSystemAsync.Path.SplitPath(path).FirstOrDefault();
            var connections = Accounts;
            if (connections != null && !string.IsNullOrWhiteSpace(connId))
            {
                var connection = connections.FirstOrDefault(c => c.Id == connId) as AccountDirectory;
                if (connection != null)
                {
                    subPath = Open.FileSystemAsync.Path.NormalizePath(path.Substring(connId.Length));
                    connectionName = connId;
                    return connection;
                }
            }
            subPath = null;
            connectionName = null;
            return null;
        }

        #endregion

        #region ** names resolution

        public string GetDirectoryId(string parentDirId, string dirName)
        {
            if (string.IsNullOrWhiteSpace(parentDirId))
            {
                return dirName;
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(parentDirId, out providerName, out subPath);
                if (connection != null)
                {
                    return Path.Combine(connection.Id, connection.FileSystem != null ? connection.FileSystem.GetDirectoryId(subPath, dirName) : "");
                }
                else
                {
                    throw new ArgumentException("Provider not found");
                }
            }
        }

        public async Task<string> GetTrashId(string relativeDirId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(relativeDirId))
            {
                return null;
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(relativeDirId, out providerName, out subPath);
                if (connection != null)
                {
                    var trashId = await connection.FileSystem.GetTrashId(subPath, cancellationToken);
                    return trashId != null ? Path.Combine(connection.Id, trashId) : null;
                }
                else
                {
                    throw new ArgumentException("Provider not found");
                }
            }
        }

        public async Task<string> GetFullPathAsync(string directoryId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(directoryId))
            {
                return "";
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(directoryId, out providerName, out subPath);
                if (connection != null)
                {
                    if (string.IsNullOrWhiteSpace(subPath))
                    {
                        return connection.Id;
                    }
                    else
                    {
                        var fullPath = await connection.FileSystem.GetFullPathAsync(subPath, cancellationToken);
                        return Path.Combine(connection.Id, fullPath);
                    }
                }
                else
                {
                    throw new ArgumentException("Provider not found");
                }
            }
        }

        public string GetFileId(string dirId, string fileName)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                return Path.Combine(connection.Id, connection.FileSystem.GetFileId(subPath, fileName));
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<string> GetFullFilePathAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null)
            {
                var fullPath = await connection.FileSystem.GetFullFilePathAsync(subPath, cancellationToken);
                return Path.Combine(connection.Id, fullPath);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<string> GetDirectoryParentIdAsync(string dirId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dirId))
                return null;
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                if (string.IsNullOrWhiteSpace(subPath))
                    return "";
                var parentId = await connection.FileSystem.GetDirectoryParentIdAsync(subPath, cancellationToken);
                return Path.Combine(connection.Id, parentId);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<string> GetFileParentIdAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null)
            {
                var parentId = await connection.FileSystem.GetFileParentIdAsync(subPath, cancellationToken);
                return Path.Combine(connection.Id, parentId);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        #endregion

        #region ** queries

        public async Task<bool> ExistsDirectoryAsync(string path, CancellationToken cancellationToken)
        {
            path = Path.NormalizePath(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(path, out providerName, out subPath);
                if (connection != null && connection.FileSystem != null)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(subPath))
                            return true;
                        //await connection.Authenticate();
                        //var subPath = Path.NormalizePath(path.Substring(connId.Length));
                        return await connection.FileSystem.ExistsDirectoryAsync(subPath, cancellationToken);
                    }
                    catch { }
                }

                //var parts = Path.SplitPath(path);
                //var connId = parts.FirstOrDefault();
                //if (!string.IsNullOrWhiteSpace(connId))
                //{
                //    var connections = DirectoriesCache[""];
                //    var connection = connections.FirstOrDefault(c => c.Id == connId) as GlobalDirectory;
                //    if (connection != null && connection.FileSystem != null)
                //    {
                //    }
                //}
                return false;
            }
        }

        public virtual async Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (Accounts == null)
                {
                    #region ** legacy format

                    //try
                    //{
                    //    var storage = AppService.GetLocalStorage();
                    //    var file = await storage.TryGetFileAsync(ENCRIPTED_CONNECTIONS_FILE_NAME);
                    //    if (file != null)
                    //    {
                    //        using (var encriptedFile = await file.OpenSequentialReadAsync())
                    //        {
                    //            var sr = new BinaryReader(encriptedFile);
                    //            var fileStream = new MemoryStream(await AppService.UnprotectData(sr.ReadBytes((int)encriptedFile.Length)));
                    //            var serializer = new XmlSerializer(typeof(GlobalDirectory[]));
                    //            var oldConnections = serializer.Deserialize(fileStream) as GlobalDirectory[];
                    //            var existingaccounts = await AppService.AccountManager.GetAccountsAsync();
                    //            foreach (var oldConnection in oldConnections)
                    //            {
                    //                if (oldConnection.Provider != null)
                    //                {
                    //                    var cs = oldConnection.ConnectionString;
                    //                    if (oldConnection.Provider is HiDriveProvider ||
                    //                        oldConnection.Provider is FourSharedProvider ||
                    //                        oldConnection.Provider is CloudMeProvider)
                    //                    {
                    //                        var parts = cs.Split(':');
                    //                        if (parts.Length != 2)
                    //                            break;
                    //                        var user = Uri.UnescapeDataString(parts[0]);
                    //                        var password = Uri.UnescapeDataString(parts[1]);
                    //                        cs = new WebDavAuthenticationTicket { User = user, Password = password }.SerializeJson();
                    //                    }
                    //                    else if (oldConnection.Provider is WebDavProvider)
                    //                    {
                    //                        var parts = cs.Split(':');
                    //                        if (parts.Length < 4)
                    //                            break;
                    //                        var server = Uri.UnescapeDataString(parts[0]);
                    //                        var user = Uri.UnescapeDataString(parts[2]);
                    //                        var password = Uri.UnescapeDataString(parts[3]);
                    //                        var domain = Uri.UnescapeDataString(parts[1]);
                    //                        bool? ignoreCertErrors = null;
                    //                        if (parts.Length == 5)
                    //                        {
                    //                            bool ice;
                    //                            if (bool.TryParse(parts[4], out ice))
                    //                                ignoreCertErrors = ice;
                    //                        }
                    //                        cs = new WebDavAuthenticationTicket { Server = server, Domain = domain, User = user, Password = password, IgnoreCertErrors = ignoreCertErrors }.SerializeJson();
                    //                    }
                    //                    var existingAccount = existingaccounts.FirstOrDefault(a => a.Id == oldConnection.Id);
                    //                    if (existingAccount != null)
                    //                        await AppService.AccountManager.RemoveAccountAsync(existingAccount);
                    //                    await (AppService.AccountManager as AccountManager).AddAccountAsync(oldConnection.Provider, oldConnection.Id, oldConnection.Name, cs);
                    //                }
                    //            }
                    //        }
                    //        await storage.DeleteFileAsync(ENCRIPTED_CONNECTIONS_FILE_NAME);
                    //    }

                    //}
                    //catch { }
                    #endregion

                    Accounts = new ObservableCollection<AccountDirectory>(await ReadAccounts());
                    foreach (var account in Accounts)
                    {
                        PrepareConnection(account);
                    }
                    //var localFileSystem = new AccountDirectory("C","Phone");
                    //connections.Add(localFileSystem);
                }
                return new ReadOnlyCollectionView<FileSystemDirectory>(Accounts);
            }
            else
            {
                string subPath, providerName;
                var connections = await GetDirectoriesAsync("", cancellationToken);
                var connection = GetConnection(path, out providerName, out subPath);
                if (connection != null && connection.FileSystem != null)
                {
                    //await LoadAsync(connection, false, null);
                    return await connection.FileSystem.GetDirectoriesAsync(subPath, cancellationToken);
                }
                else
                {
                    throw new ArgumentException("Provider not found");
                }
            }
        }

        public virtual async Task<IDataCollection<FileSystemFile>> GetFilesAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new EmptyCollectionView<FileSystemFile>();
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(path, out providerName, out subPath);
                if (connection != null && connection.FileSystem != null)
                {
                    return await connection.FileSystem.GetFilesAsync(subPath, cancellationToken);
                }
                else
                {
                    throw new ArgumentException("Provider not found");
                }
            }
        }

        public async Task<Stream> GetDirectoryIconAsync(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (connection != null && connection.FileSystem != null)
                {
                    return await connection.FileSystem.GetDirectoryIconAsync(subPath, cancellationToken);
                }
                return null;
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }


        public async Task<bool> CanOpenDirectoryThumbnailAsync(string dirId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanOpenDirectoryThumbnailAsync(subPath, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<Stream> OpenDirectoryThumbnailAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.OpenDirectoryThumbnailAsync(subPath, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanOpenFileThumbnailAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanOpenFileThumbnailAsync(subPath, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }


        public async Task<Stream> OpenFileThumbnailAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.OpenFileThumbnailAsync(subPath, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<FileSystemFile> GetFileAsync(string fileId, bool full, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.GetFileAsync(subPath, full, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanGetDirectoryLinkAsync(string dirId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanGetDirectoryLinkAsync(subPath, cancellationToken);
            }
            return false;
        }

        public async Task<Uri> GetDirectoryLinkAsync(string dirId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.GetDirectoryLinkAsync(subPath, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanGetFileLinkAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanGetFileLinkAsync(subPath, cancellationToken);
            }
            return false;
        }

        public async Task<Uri> GetFileLinkAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.GetFileLinkAsync(subPath, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<FileSystemDirectory> GetDirectoryAsync(string dirId, bool full, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null && string.IsNullOrWhiteSpace(subPath))
            {
                if (full && connection.FileSystem != null)
                {
                    try
                    {
                        var driveInfo = await connection.FileSystem.GetDriveAsync(cancellationToken);
                        if (driveInfo != null)
                        {
                            if (driveInfo.UsedSize.HasValue)
                                connection.UsedSize = driveInfo.UsedSize.Value;
                            if (driveInfo.TotalSize.HasValue)
                                connection.TotalSize = driveInfo.TotalSize.Value;
                        }
                    }
                    catch { }
                }
                return connection;
            }
            else if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.GetDirectoryAsync(subPath, full, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        #endregion

        #region ** modifying

        public string[] GetAcceptedFileTypes(string dirId, bool includeSubDirectories)
        {
            if (string.IsNullOrWhiteSpace(dirId))
            {
                if (includeSubDirectories)
                {
                    return new string[] { "*/*" };
                }
                else
                {
                    return new string[0];
                }
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(dirId, out providerName, out subPath);
                if (connection != null && connection.FileSystem != null)
                {
                    return connection.FileSystem.GetAcceptedFileTypes(subPath, includeSubDirectories);
                }
                else
                {
                    throw new ArgumentException("Provider not found");
                }
            }
        }

        public async Task<bool> CanWriteFileAsync(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanWriteFileAsync(subPath, cancellationToken);
            }
            return false;
        }

        public async Task<FileSystemFile> WriteFileAsync(string dirId, FileSystemFile file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.WriteFileAsync(subPath, file, fileStream, progress, cancellationToken);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanOpenFileAsync(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanOpenFileAsync(subPath, cancellationToken);
            }
            return false;
        }

        public async Task<Stream> OpenFileAsync(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null)
            {
                if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.OpenFileAsync(subPath, cancellationToken);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanDeleteFile(string fileId, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanDeleteFile(subPath, cancellationToken);
            }
            return false;
        }

        public async Task<FileSystemFile> DeleteFileAsync(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null)
            {
                return await connection.FileSystem.DeleteFileAsync(subPath, sendToTrash, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanDeleteDirectory(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (string.IsNullOrWhiteSpace(subPath))
                {
                    return true;
                }
                else if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.CanDeleteDirectory(subPath, cancellationToken);
                }
            }
            return false;
        }

        public async Task<FileSystemDirectory> DeleteDirectoryAsync(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                if (string.IsNullOrWhiteSpace(subPath))
                {
                    await RemoveConnection(connection);
                    return null;
                }
                else
                {
                    return await connection.FileSystem.DeleteDirectoryAsync(subPath, sendToTrash, cancellationToken);
                }
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanCreateDirectory(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                return await connection.FileSystem.CanCreateDirectory(subPath, cancellationToken);
            }
            return false;
        }

        public async Task<FileSystemDirectory> CreateDirectoryAsync(string dirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                return await connection.FileSystem.CreateDirectoryAsync(subPath, directory, cancellationToken);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanMoveDirectory(string sourceDirPath, string destDirPath, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string destSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceDirPath, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(destDirPath, out destProviderName, out destSubPath);
            if (sourceConnection != null &&
                destConnection != null &&
                sourceConnection == destConnection)
            {
                return await sourceConnection.FileSystem.CanMoveDirectory(sourceSubPath, destSubPath, cancellationToken);
            }
            return false;
        }

        public async Task<FileSystemDirectory> MoveDirectoryAsync(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string targetSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceDirId, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirId, out destProviderName, out targetSubPath);
            if (sourceConnection != null &&
                destConnection != null &&
                sourceConnection == destConnection)
            {
                return await sourceConnection.FileSystem.MoveDirectoryAsync(sourceSubPath, targetSubPath, directory, cancellationToken);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<bool> CanCopyDirectory(string sourceDirPath, string targetDirPath, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string destSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceDirPath, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirPath, out destProviderName, out destSubPath);
            if (sourceConnection != null &&
                destConnection != null)
            {
                if (sourceConnection == destConnection)
                {
                    return await sourceConnection.FileSystem.CanCopyDirectory(sourceSubPath, destSubPath, cancellationToken);
                }
            }
            return false;
        }

        public async Task<FileSystemDirectory> CopyDirectoryAsync(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string targetSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceDirId, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirId, out destProviderName, out targetSubPath);
            if (sourceConnection != null &&
                destConnection != null &&
                sourceConnection == destConnection)
            {
                return await sourceConnection.FileSystem.CopyDirectoryAsync(sourceSubPath, targetSubPath, directory, cancellationToken);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<bool> CanCopyFile(string sourceFilePath, string targetDirId, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string destSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceFilePath, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirId, out destProviderName, out destSubPath);
            if (sourceConnection != null &&
                destConnection != null)
            {
                if (sourceConnection == destConnection)
                {
                    return await sourceConnection.FileSystem.CanCopyFile(sourceSubPath, destSubPath, cancellationToken);
                }
            }
            return false;
        }

        public async Task<FileSystemFile> CopyFileAsync(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string targetSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceFileId, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirId, out destProviderName, out targetSubPath);
            if (sourceConnection != null &&
                destConnection != null &&
                sourceConnection == destConnection)
            {
                return await sourceConnection.FileSystem.CopyFileAsync(sourceSubPath, targetSubPath, file, cancellationToken);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<bool> CanMoveFile(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string destSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceFileId, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirId, out destProviderName, out destSubPath);
            if (sourceConnection != null &&
                destConnection != null &&
                sourceConnection == destConnection)
            {
                return await sourceConnection.FileSystem.CanMoveFile(sourceSubPath, destSubPath, cancellationToken);
            }
            return false;
        }

        public async Task<FileSystemFile> MoveFileAsync(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            string sourceSubPath, sourceProviderName;
            string targetSubPath, destProviderName;
            var sourceConnection = GetConnection(sourceFileId, out sourceProviderName, out sourceSubPath);
            var destConnection = GetConnection(targetDirId, out destProviderName, out targetSubPath);
            if (sourceConnection != null &&
                destConnection != null &&
                sourceConnection == destConnection)
            {
                return await sourceConnection.FileSystem.MoveFileAsync(sourceSubPath, targetSubPath, file, cancellationToken);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public async Task<bool> CanUpdateDirectory(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (string.IsNullOrWhiteSpace(subPath))
                {
                    return true;
                }
                else if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.CanUpdateDirectory(subPath, cancellationToken);
                }
            }
            return false;
        }

        public async Task<FileSystemDirectory> UpdateDirectoryAsync(string dirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                if (string.IsNullOrWhiteSpace(subPath))
                {
                    if (!string.IsNullOrEmpty(directory.Name))
                    {
                        var index = Accounts.IndexOf(connection);
                        connection.Name = directory.Name;
                        Accounts[index] = connection;
                        await WriteAccounts(Accounts);
                    }
                    return connection;
                }
                else if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.UpdateDirectoryAsync(subPath, directory, cancellationToken);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<bool> CanUpdateFile(string path, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (!string.IsNullOrWhiteSpace(subPath) && connection.FileSystem != null)
                {
                    return await connection.FileSystem.CanUpdateFile(subPath, cancellationToken);
                }
            }
            return false;
        }

        public async Task<FileSystemFile> UpdateFileAsync(string fileId, FileSystemFile file, CancellationToken cancellationToken)
        {
            string subPath, providerName;
            var connection = GetConnection(fileId, out providerName, out subPath);
            if (connection != null)
            {
                if (connection.FileSystem != null)
                {
                    return await connection.FileSystem.UpdateFileAsync(subPath, file, cancellationToken);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        #endregion

        #region ** social extension

        public event EventHandler CommentsChanged;

        public async Task<FileSystemPerson> GetCurrentUserAsync(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null && connection.FileSystem is ISocialExtension)
            {
                var socialExtension = connection.FileSystem as ISocialExtension;
                return await socialExtension.GetCurrentUserAsync(subPath);
            }
            throw new NotImplementedException();
        }

        public bool CanAddComment(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (!string.IsNullOrWhiteSpace(subPath) &&
                    connection.FileSystem is ISocialExtension)
                {
                    return ((ISocialExtension)connection.FileSystem).CanAddComment(subPath);
                }
            }
            return false;
        }

        public async Task<IDataCollection<FileSystemComment>> GetCommentsAsync(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (connection.FileSystem is ISocialExtension)
                {
                    return await ((ISocialExtension)connection.FileSystem).GetCommentsAsync(subPath);
                }
                else
                {
                    return new EmptyCollectionView<FileSystemComment>();
                }
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task AddCommentAsync(string path, string message)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                await ((ISocialExtension)connection.FileSystem).AddCommentAsync(subPath, message);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public bool CanThumbUp(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                if (!string.IsNullOrWhiteSpace(subPath) &&
                    connection.FileSystem is ISocialExtension)
                {
                    return ((ISocialExtension)connection.FileSystem).CanThumbUp(subPath);
                }
            }
            return false;
        }

        public async Task AddThumbUp(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                await ((ISocialExtension)connection.FileSystem).AddThumbUp(subPath);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task RemoveThumbUp(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                await ((ISocialExtension)connection.FileSystem).RemoveThumbUp(subPath);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }

        public async Task<IDataCollection<FileSystemPerson>> GetThumbsUpAsync(string path)
        {
            string subPath, providerName;
            var connection = GetConnection(path, out providerName, out subPath);
            if (connection != null)
            {
                return await ((ISocialExtension)connection.FileSystem).GetThumbsUpAsync(subPath);
            }
            else
            {
                throw new ArgumentException("Provider not found");
            }
        }


        void OnCommentsChanged(object sender, EventArgs e)
        {
            RaiseCommentsChanged();
        }

        protected void RaiseCommentsChanged()
        {
            if (CommentsChanged != null)
            {
                CommentsChanged(this, new EventArgs());
            }
        }
        #endregion

        #region ** search

        public async Task<bool> CanSearchAsync(string dirId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dirId))
            {
                if (Accounts == null)
                    return false;
                return await Accounts.Cast<AccountDirectory>().AnyAsync(async c => c.FileSystem != null && /*c.State == GlobalDirectoryState.Authenticated &&*/ await c.FileSystem.CanSearchAsync("", cancellationToken));
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(dirId, out providerName, out subPath);
                if (connection != null)
                {
                    return await connection.FileSystem.CanSearchAsync(subPath, cancellationToken);
                }
                else
                {
                    return true;
                }
            }
        }

        public async Task<IDataCollection<FileSystemSearchItem>> SearchAsync(string dirId, string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(dirId))
            {
                var tasks = (await Accounts.Cast<AccountDirectory>().WhereAsync(async c =>

                    await c.FileSystem.CanSearchAsync("", cancellationToken))).Select(connection => Search(connection, "", query, cancellationToken)).ToArray();

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch { }
                var sequence = new C1SequenceDataCollection<FileSystemSearchItem>();
                foreach (var task in tasks.Where(t => t.Status == TaskStatus.RanToCompletion))
                {
                    sequence.Collections.Add(task.Result);
                }
                return sequence;
            }
            else
            {
                string subPath, providerName;
                var connection = GetConnection(dirId, out providerName, out subPath);
                if (connection != null)
                {
                    return await Search(connection, subPath, query, cancellationToken);
                }
                else
                {
                    return new EmptyCollectionView<FileSystemSearchItem>();
                }
            }
        }

        private static async Task<IDataCollection<FileSystemSearchItem>> Search(AccountDirectory connection,
            string subPath,
            string query,
            CancellationToken cancellationToken)
        {
            if (await connection.FileSystem.CheckAccessAsync("", false, CancellationToken.None))
            {
                var results = await connection.FileSystem.SearchAsync(subPath, query, cancellationToken);
                return new SearchTransformList(connection.Id, results);
            }
            throw new InvalidOperationException();
        }

        #endregion

        private static TaskScheduler GetTaskScheduler()
        {
            return SynchronizationContext.Current == null ? TaskScheduler.Current : TaskScheduler.FromCurrentSynchronizationContext();
        }

        #region ** storage


        private const string ACCOUNTS_FILE_NAME = "Accounts.json";
        private const string ACCOUNTS_FILE_NAME_ENCRIPTED = "Accounts.json.encripted";

        private async Task<List<AccountDirectory>> ReadAccounts()
        {
            Stream fileStream = null;
            try
            {
                var encript = AppService.DataProtectionEnabled;
                var storage = AppService.GetLocalStorage();
                if (encript)
                {
                    var encriptedFile = await storage.TryGetFileAsync(ACCOUNTS_FILE_NAME_ENCRIPTED);
                    if (encriptedFile != null)
                    {
                        fileStream = (await encriptedFile.OpenSequentialReadAsync());
                        var buffer = new byte[fileStream.Length];
                        await fileStream.ReadAsync(buffer, 0, (int)fileStream.Length);
                        var decriptedBuffer = await AppService.UnprotectData(buffer);
                        return new MemoryStream(decriptedBuffer).DeserializeJson<AccountDirectory[]>().ToList();
                    }
                }
                var file = await storage.TryGetFileAsync(ACCOUNTS_FILE_NAME);
                if (file != null)
                {
                    fileStream = (await file.OpenSequentialReadAsync());
                    return fileStream.DeserializeJson<AccountDirectory[]>().ToList();
                }
                return new List<AccountDirectory>();
            }
            catch
            {
                if (fileStream != null && fileStream.CanSeek)
                {
                    fileStream.Seek(0, System.IO.SeekOrigin.Begin);
                    var reader = new System.IO.StreamReader(fileStream);
                    var content = reader.ReadToEnd();
                }
                return new List<AccountDirectory>();
            }
            finally
            {
                if (fileStream != null)
                    fileStream.Dispose();
            }
        }

        private static readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(initialCount: 1);

        private async Task WriteAccounts(IEnumerable<AccountDirectory> accounts)
        {
            await _writeSemaphore.WaitAsync();
            Stream fileStream = null;
            try
            {
                var encript = AppService.DataProtectionEnabled;
                var fileName = encript ? ACCOUNTS_FILE_NAME_ENCRIPTED : ACCOUNTS_FILE_NAME;
                var storage = AppService.GetLocalStorage();
                var file = await storage.TryGetFileAsync(fileName);
                if (file == null)
                {
                    file = await storage.CreateFileAsync(fileName);
                }
                fileStream = await file.OpenWriteAsync();
                if (encript)
                {
                    var contentString = accounts.ToArray().SerializeJson();
                    var encodedContent = await AppService.ProtectData(Encoding.UTF8.GetBytes(contentString));
                    await fileStream.WriteAsync(encodedContent, 0, encodedContent.Length);
                }
                else
                {
                    await accounts.ToArray().SerializeJsonIntoStreamAsync(fileStream);
                }
            }
            finally
            {
                if (fileStream != null)
                    fileStream.Dispose();
                _writeSemaphore.Release();
            }
        }

        #endregion

    }

    internal class SearchTransformList : TransformList<FileSystemSearchItem, FileSystemSearchItem>
    {
        private string _connectionId;

        public SearchTransformList(string connectionId, IEnumerable<FileSystemSearchItem> innerList)
            : base(innerList)
        {
            _connectionId = connectionId;
        }

        protected override FileSystemSearchItem Transform(int index, FileSystemSearchItem item)
        {
            return new FileSystemSearchItem
            {
                DirectoryId = Path.Combine(_connectionId, item.DirectoryId),
                Item = item.Item
            };
        }

        protected override FileSystemSearchItem TransformBack(FileSystemSearchItem item)
        {
            throw new NotImplementedException();
        }
    }

    #region ** legacy

    public class GlobalDirectory : IXmlSerializable
    {
        #region ** object model
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string ConnectionString { get; internal set; }
        public string ProviderName { get; private set; }
        public IProvider Provider { get; private set; }

        #endregion

        #region ** implementation

        internal void Initialize(string connectionId, string name, string connectionProvider, string connectionString)
        {
            Id = connectionId;
            Name = name;
            ConnectionString = connectionString;
            IProvider provider = null;
            #region ** legacy providers
            connectionProvider = connectionProvider.Replace("Woopiti.Phone.FileSystem", "Open.FileSystem");
            if (connectionProvider == "Open.FileSystem.PicasaProvider" ||
                connectionProvider == "Open.FileSystem.GooglePlusProvider")
            {
                connectionProvider = "Open.FileSystem.GooglePhotosProvider";
                if (Name == "Picasa" || Name == "Google+")
                    Name = "Google Photos";
            }
            if (connectionProvider == "Open.FileSystem.SkyDriveProvider")
            {
                connectionProvider = "Open.FileSystem.OneDriveProvider";
                if (Name == "Sky Drive")
                    Name = "OneDrive";
            }
            #endregion
            if (!string.IsNullOrWhiteSpace(connectionProvider))
            {
                var type = Type.GetType(connectionProvider);
                if (type != null)
                {
                    provider = type.GetTypeInfo().DeclaredConstructors.First().Invoke(new object[0]) as IProvider;
                }
            }
            ProviderName = connectionProvider;
            Provider = provider;
        }

        #endregion

        #region ** serialization

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new System.NotImplementedException();
        }

        public void ReadXml(System.Xml.XmlReader reader)
        {
            Initialize(reader.GetAttribute("id"),
                reader.GetAttribute("name"),
                reader.GetAttribute("provider"),
                reader.GetAttribute("connectionString"));
            reader.Read();
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            //writer.WriteAttributeString("id", Id);
            //writer.WriteAttributeString("name", Name);
            //writer.WriteAttributeString("connectionString", ConnectionString);
            //writer.WriteAttributeString("provider", Provider != null ? Provider.GetType().FullName : "");
        }

        #endregion
    }
    #endregion
}
