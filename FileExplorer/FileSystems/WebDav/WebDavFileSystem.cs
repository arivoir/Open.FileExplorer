using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using Open.WebDav;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class WebDavFileSystem : UnifiedItemsFileSystem
    {
        #region ** fields

        protected string Server { get; set; }
        protected string ServerPath { get; set; }
        protected bool IgnoreCertErrors { get; set; }
        protected WebDavOptions _options;

        #endregion

        #region ** initialization

        static WebDavFileSystem()
        {
        }

        protected override async Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            var webDavTicket = ticket.AuthToken.DeserializeJson<WebDavAuthenticationTicket>();
            var uri = new Uri(webDavTicket.Server ?? Server);
            Server = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
            ServerPath = uri.AbsolutePath;
            _options = ticket.Tag as WebDavOptions;
            return ticket;
        }

        protected new async Task<WebDavAuthenticationTicket> GetAccessTokenAsync(bool promptForUserInteraction, CancellationToken cancellationToken)
        {
            var ticket = await AuthenticateAsync(null, promptForUserInteraction, cancellationToken);
            return ticket.AuthToken.DeserializeJson<WebDavAuthenticationTicket>();
        }

        #endregion

        #region ** object model

        protected override bool IsFileNameExtensionRequired
        {
            get
            {
                return true;
            }
        }

        public override string[] AllowedDirectorySortFields => new string[] { "Name", "ModifiedDate", "CreatedDate" };
        public override string[] AllowedFileSortFields => new string[] { "Name", "Size", "ModifiedDate", "CreatedDate" };

        #endregion

        #region ** authentication

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var ticket = string.IsNullOrWhiteSpace(connectionString) ? new WebDavAuthenticationTicket() : connectionString.DeserializeJson<WebDavAuthenticationTicket>();
            var provider = new WebDavProvider();
            return await authenticationBroker.FormAuthenticationBrokerAsync(LogInAsync,
                provider.Name,
                provider.Color,
                provider.IconResourceKey,
                server: ticket.Server,
                domain: ticket.Domain,
                user: ticket.User,
                password: ticket.Password,
                userAndPasswordRequired: false);
        }

        public static async Task<AuthenticatonTicket> LogInAsync(string server, string domain, string user, string password, bool ignoreCertErrors)
        {
            try
            {
                server = server.EndsWith("/") ? server : server + "/";
                var uri = new Uri(server);
                var client = new WebDavClient(uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped), domain, user, password, ignoreCertErrors);
                var r = await client.PropFindAsync(uri.AbsolutePath, WebDavDepth.Zero);
                var options = await client.OptionsAsync(GetDirRelativePath(uri.AbsolutePath, ""), CancellationToken.None);
                return new AuthenticatonTicket { AuthToken = new WebDavAuthenticationTicket { Server = server, Domain = domain, User = user, Password = password, IgnoreCertErrors = ignoreCertErrors }.SerializeJson(), Tag = options };
            }
            catch (Exception exc) { throw ProcessException(exc); }
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var ticket = refreshToken.DeserializeJson<WebDavAuthenticationTicket>();
                var uri = new Uri(ticket.Server);
                var server = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
                var client = new WebDavClient(server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                var options = await client.OptionsAsync(GetDirRelativePath(uri.AbsolutePath, ""), cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = options };
            }
            catch (Exception exc) { throw ProcessException(exc); }
        }

        #endregion

        #region ** get info

        protected override async Task<IList<FileSystemItem>> GetItemsAsync(string dirId, CancellationToken cancellationToken)
        {
            var dirPath = GetDirRelativePath(dirId);
            var ticket = await GetAccessTokenAsync(true, cancellationToken);
            var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
            var result = await client.PropFindAsync(dirPath);
            var items = new List<FileSystemItem>();
            foreach (var responseItem in result.Descendants(WebDavClient.Response))
            {
                FileSystemItem item;
                if (IsCollection(responseItem))
                {
                    var href = Uri.UnescapeDataString(responseItem.Descendants(WebDavClient.HRef).First().Value);
                    if (!href.EndsWith("/"))
                        href += "/";
                    if (href == dirPath || href == GetDirAbsolutePath(dirId))
                        continue;
                    item = CreateDirectory(responseItem);
                }
                else
                {
                    item = CreateFile(responseItem);
                }
                items.Add(item);
            }
            return items;
        }

        protected virtual WebDavFile CreateFile(XElement responseItem)
        {
            return new WebDavFile(responseItem);
        }

        protected virtual WebDavDirectory CreateDirectory(XElement responseItem)
        {
            return new WebDavDirectory(responseItem);
        }

        #endregion

        #region ** download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("GET"));
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var filePath = GetFileRelativePath(fileId);
            var ticket = await GetAccessTokenAsync(true, cancellationToken);
            var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
            return await client.DownloadFileAsync(filePath, cancellationToken);
        }

        #endregion

        #region ** upload

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("PUT"));
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            try
            {
                var filePath = GetFileRelativePath(Path.Combine(dirId, file.Name));
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                await client.UploadFileAsync(filePath, file.ContentType, fileStream, progress, cancellationToken);
                return new WebDavFile(file.Name, file.Name, file.ContentType);
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        #endregion

        #region ** create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("MKCOL") || _options.Dav.Contains("extended-mkcol"));
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            try
            {
                var dirName = item.Name;
                if (string.IsNullOrWhiteSpace(dirName))
                    throw new ArgumentNullException("Name");
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                var dirPath = GetDirRelativePath(Path.Combine(dirId, dirName));
                await client.MkColAsync(dirPath, cancellationToken);
                return new WebDavDirectory(dirName, dirPath);
            }
            catch (WebDavException exc)
            {
                if (exc.ReasonPhrase == "Collection already exists" ||
                    exc.ReasonPhrase == "Conflict")
                    throw new DuplicatedDirectoryException(exc.Message);
                throw;
            }
        }

        #endregion

        #region ** copy

        protected override Task<bool> CanCopyDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("COPY"));
        }

        protected override async Task<FileSystemDirectory> CopyDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            try
            {
                var dirName = directory != null ? directory.Name : Path.GetFileName(sourceDirId);
                var sourceDirPath = GetDirRelativePath(sourceDirId);
                var targetDirPath = GetDirRelativePath(Path.Combine(targetDirId, dirName));
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                await client.CopyResourceAsync(sourceDirPath, targetDirPath);
                return new WebDavDirectory(dirName, targetDirPath);
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        protected override Task<bool> CanCopyFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("COPY"));
        }

        protected override async Task<FileSystemFile> CopyFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            try
            {
                var fileName = file != null ? file.Name : Path.GetFileName(sourceFileId);
                var sourceFilePath = GetFileRelativePath(sourceFileId);
                var targetFilePath = GetFileRelativePath(Path.Combine(targetDirId, fileName));
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                await client.CopyResourceAsync(sourceFilePath, targetFilePath);
                return new WebDavFile(fileName, fileName, MimeType.GetContentTypeFromExtension(Path.GetExtension(fileName)));
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        #endregion

        #region ** move

        protected override Task<bool> CanMoveDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("MOVE"));
        }

        protected override async Task<FileSystemDirectory> MoveDirectoryAsyncOverride(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            try
            {
                var dirName = directory != null ? directory.Name : Path.GetFileName(sourceDirId);
                var sourceDirPath = GetDirRelativePath(sourceDirId);
                var targetDirPath = GetDirRelativePath(Path.Combine(targetDirId, dirName));
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                await client.MoveResourceAsync(sourceDirPath, targetDirPath);
                return new WebDavDirectory(dirName, targetDirPath);
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        protected override Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("MOVE"));
        }

        protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            try
            {
                var fileName = file != null ? file.Name : Path.GetFileName(sourceFileId);
                var sourceFilePath = GetFileRelativePath(sourceFileId);
                var targetFilePath = GetFileRelativePath(Path.Combine(targetDirId, fileName));
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                await client.MoveResourceAsync(sourceFilePath, targetFilePath);
                return new WebDavFile(fileName, fileName, MimeType.GetContentTypeFromExtension(Path.GetExtension(fileName)));
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        #endregion

        #region ** update

        protected override Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("MOVE"));
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string dirId, FileSystemDirectory dir, CancellationToken cancellationToken)
        {
            try
            {
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                var dirName = dir.Name;
                var sourceDirPath = GetDirRelativePath(dirId);
                var targetDirPath = GetDirRelativePath(Path.Combine(Path.GetParentPath(dirId), dirName));
                await client.MoveResourceAsync(sourceDirPath, targetDirPath);
                return new WebDavDirectory(dirName, targetDirPath);
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        protected override Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("MOVE"));
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile file, CancellationToken cancellationToken)
        {
            try
            {
                var ticket = await GetAccessTokenAsync(true, cancellationToken);
                var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
                var fileName = file.Name;
                var sourceFilePath = GetFileRelativePath(fileId);
                var targetFilePath = GetFileRelativePath(Path.Combine(Path.GetParentPath(fileId), fileName));
                await client.MoveResourceAsync(sourceFilePath, targetFilePath);
                return new WebDavFile(fileName, fileName, file.ContentType);
            }
            catch (Exception exc)
            {
                throw await ProcessExceptionAsync(exc);
            }
        }

        #endregion

        #region ** delete

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("DELETE"));
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var dirPath = GetDirRelativePath(dirId);
            var ticket = await GetAccessTokenAsync(true, cancellationToken);
            var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
            await client.DeleteResourceAsync(dirPath, cancellationToken);
            return null;
        }

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_options.Allow.Contains("DELETE"));
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var filePath = GetFileRelativePath(fileId);
            var ticket = await GetAccessTokenAsync(true, cancellationToken);
            var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
            await client.DeleteResourceAsync(filePath, cancellationToken);
            return null;
        }

        #endregion

        #region ** search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var ticket = await GetAccessTokenAsync(true, cancellationToken);
            var client = new WebDavClient(Server, ticket.Domain, ticket.User, ticket.Password, ticket.IgnoreCertErrors ?? false);
            //<D:searchrequest xmlns:D="DAV:" xmlns:F="http://example.com/foo">
            //  <F:natural-language-query>
            //    Find the locations of good Thai restaurants in Los Angeles
            //  </F:natural-language-query>
            //</D:searchrequest>
            var queryDoc = new XDocument();
            var searchRequest = new XElement(WebDavClient.SearchRequest);
            var queryElem = new XElement(WebDavClient.NaturalLanguageQuery);
            queryElem.Value = query;
            searchRequest.Add(queryElem);
            queryDoc.Add(searchRequest);
            var result = await client.SearchAsync(dirId, queryDoc, cancellationToken);
            var searchResult = new List<FileSystemSearchItem>();
            //foreach (var file in result)
            //{
            //    var f = ConvertFile(file);
            //    string id;
            //    if (f.IsDirectory)
            //    {
            //        id = f.Id;
            //    }
            //    else
            //    {
            //        id = file.Parents != null && file.Parents.FirstOrDefault() != null ? file.Parents.FirstOrDefault().Id : "";
            //    }
            //    searchResult.Add(new FileSystemSearchItem { DirectoryId = id, Item = f });
            //}
            return searchResult.AsDataCollection();
        }

        #endregion

        #region ** implementation

        public static bool IsCollection(XElement responseItem)
        {
            var resourceTypeElem = responseItem.Descendants(WebDavClient.ResourceType).FirstOrDefault();
            return resourceTypeElem != null && resourceTypeElem.Descendants(WebDavClient.Collection).Count() > 0;
        }

        public static string GetName(XElement responseItem)
        {
            var displayNameElem = responseItem.Descendants(WebDavClient.DisplayName).FirstOrDefault();
            if (displayNameElem != null)
                return displayNameElem.Value;
            else
            {
                var hrefElem = responseItem.Descendants(WebDavClient.HRef).FirstOrDefault();
                return Path.GetFileName(Uri.UnescapeDataString(hrefElem.Value));
            }
        }

        public static string GetHRef(XElement element)
        {
            var hrefElem = element.Descendants(WebDavClient.HRef).FirstOrDefault();
            if (hrefElem != null)
                return hrefElem.Value;
            return null;
        }

        protected string GetDirRelativePath(string dirId)
        {
            return GetDirRelativePath(ServerPath, dirId);
        }

        protected static string GetDirRelativePath(string serverPath, string dirId)
        {
            return serverPath + (string.IsNullOrWhiteSpace(dirId) ? "" : dirId.Replace(@"\", "/") + "/");
        }

        protected string GetDirAbsolutePath(string dirId)
        {
            return Server + GetDirRelativePath(dirId);
        }

        protected string GetFileRelativePath(string dirId)
        {
            return ServerPath + (string.IsNullOrWhiteSpace(dirId) ? "" : dirId.Replace(@"\", "/"));
        }

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        protected static Exception ProcessException(Exception exc)
        {
            var wExc = exc as WebDavException;
            if (wExc != null)
            {
                if (wExc.StatusCode == 401)
                {
                    return new AccessDeniedException();
                }
                else if (wExc.StatusCode == (int)HttpStatusCode.PreconditionFailed)
                {
                    return new DuplicatedFileException(exc.Message);
                }
            }
            return ProcessOAuthException(exc);
        }

        #endregion
    }

    [DataContract]
    public class WebDavAuthenticationTicket
    {
        [DataMember(Name = "server")]
        public string Server { get; set; }
        [DataMember(Name = "domain")]
        public string Domain { get; set; }
        [DataMember(Name = "user")]
        public string User { get; set; }
        [DataMember(Name = "password")]
        public string Password { get; set; }
        [DataMember(Name = "ignoreCertErrors")]
        public bool? IgnoreCertErrors { get; set; }
    }
}
