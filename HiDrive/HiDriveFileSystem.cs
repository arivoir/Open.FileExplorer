using Open.FileExplorer.WebDav;
using Open.FileSystemAsync;
using Open.WebDav;
using System.Text.Json;

namespace Open.FileExplorer.HiDrive
{
    public class HiDriveFileSystem : WebDavFileSystem
    {
        #region fields

        private static string HiDriveServer = "https://webdav.hidrive.strato.com";
        private static string HiDriveServerPath = "/users/{0}/";

        #endregion

        #region initialization

        public HiDriveFileSystem()
        {
            Server = HiDriveServer;
        }

        #endregion

        #region authentication

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            var webDavTicket = JsonSerializer.Deserialize<WebDavAuthenticationTicket>(ticket.AuthToken);
            ServerPath = string.Format(HiDriveServerPath, webDavTicket.User);
            _options = ticket.Tag as WebDavOptions;
            return ticket;
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var ticket = JsonSerializer.Deserialize<WebDavAuthenticationTicket>(refreshToken);
                var client = new WebDavClient(HiDriveServer, ticket.Domain, ticket.User, ticket.Password);
                var options = await client.OptionsAsync(GetDirRelativePath(string.Format(HiDriveServerPath, ticket.User), ""), cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = options };
            }
            catch (Exception exc) { throw ProcessException(exc); }
        }

        public override Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var ticket = string.IsNullOrWhiteSpace(connectionString) ? new WebDavAuthenticationTicket() : JsonSerializer.Deserialize<WebDavAuthenticationTicket>(connectionString);
            var provider = new HiDriveProvider();
            return authenticationBroker.FormAuthenticationBrokerAsync(async (server, domain, user, password, ignoreCertErrors) =>
                {
                    try
                    {
                        var client = new WebDavClient(HiDriveServer, domain, user, password);
                        var r = await client.PropFindAsync(string.Format(HiDriveServerPath, user), WebDavDepth.Zero);
                        var options = await client.OptionsAsync(GetDirRelativePath(string.Format(HiDriveServerPath, user), ""), CancellationToken.None);
                        return new AuthenticatonTicket { AuthToken = JsonSerializer.Serialize(new WebDavAuthenticationTicket { User = user, Password = password }), Tag = options };
                    }
                    catch (Exception exc) { throw ProcessException(exc); }
                },
                provider.Name,
                provider.Color,
                provider.IconResourceKey,
                user: ticket.User,
                password: ticket.Password,
                showServer: false,
                showDomain: false);
        }

        #endregion

        #region implementation

        protected override async Task<IList<FileSystemItem>> GetItemsAsync(string dirId, CancellationToken cancellationToken)
        {
            var items = await base.GetItemsAsync(dirId, cancellationToken);
            return items.Where(i => i.Name != ".hidrive").ToList();
        }

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        #endregion
    }
}
