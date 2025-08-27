using Open.FileExplorer.WebDav;
using Open.FileSystemAsync;
using Open.WebDav;
using System.Text.Json;
using System.Xml.Linq;

namespace Open.FileExplorer.FourShared
{
    public class FourSharedFileSystem : WebDavFileSystem
    {
        #region fields

        private static string FourSharedServer = "https://webdav.4shared.com";
        private static string FourSharedServerPath = "/";

        #endregion

        #region initialization

        public FourSharedFileSystem()
        {
            Server = FourSharedServer;
            ServerPath = FourSharedServerPath;
        }

        #endregion

        #region authentication

        protected override async Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            _options = ticket.Tag as WebDavOptions;
            return ticket;
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var ticket = JsonSerializer.Deserialize<WebDavAuthenticationTicket>(refreshToken);
                var client = new WebDavClient(FourSharedServer, ticket.Domain, ticket.User, ticket.Password);
                var options = await client.OptionsAsync(GetDirRelativePath(FourSharedServerPath, ""), cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = options };
            }
            catch (Exception exc) { throw ProcessException(exc); }
        }

        public override Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var ticket = string.IsNullOrWhiteSpace(connectionString) ? new WebDavAuthenticationTicket() : JsonSerializer.Deserialize<WebDavAuthenticationTicket>(connectionString);
            var provider = new FourSharedProvider();
            return authenticationBroker.FormAuthenticationBrokerAsync(async (server, domain, user, password, ignoreCertErrors) =>
                {
                    try
                    {
                        var client = new WebDavClient(FourSharedServer, domain, user, password);
                        var r = await client.PropFindAsync(FourSharedServerPath, WebDavDepth.Zero);
                        var options = await client.OptionsAsync(GetDirRelativePath(FourSharedServerPath, ""), CancellationToken.None);
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

        protected override WebDavFile CreateFile(XElement responseItem)
        {
            var file = base.CreateFile(responseItem);
            file.SetContentType(MimeType.GetContentTypeFromExtension(System.IO.Path.GetExtension(file.Name)));
            return file;
        }

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        #endregion
    }
}
