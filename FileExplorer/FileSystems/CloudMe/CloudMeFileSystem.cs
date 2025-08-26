using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;
using Open.WebDav;

namespace Open.FileExplorer
{
    public class CloudMeFileSystem : WebDavFileSystem
    {
        #region ** fields

        private static string CloudMeServer = "http://webdav.cloudme.com";
        private static string CloudMeServerPath = "/{0}/CloudDrive/Documents/";
        private static string CloudMeFolder = "CloudMe";

        #endregion

        #region ** initialization

        public CloudMeFileSystem()
        {
            Server = CloudMeServer;
        }

        #endregion

        #region ** authentication

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            var webDavTicket = ticket.AuthToken.DeserializeJson<WebDavAuthenticationTicket>();
            ServerPath = string.Format(CloudMeServerPath, webDavTicket.User);
            _options = ticket.Tag as WebDavOptions;
            return ticket;
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var ticket = refreshToken.DeserializeJson<WebDavAuthenticationTicket>();
                var client = new WebDavClient(CloudMeServer, ticket.Domain, ticket.User, ticket.Password);
                var options = await client.OptionsAsync(GetDirRelativePath(string.Format(CloudMeServerPath, ticket.User), ""), cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = options };
            }
            catch (Exception exc) { throw ProcessException(exc); }
        }

        public override Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var ticket = string.IsNullOrWhiteSpace(connectionString) ? new WebDavAuthenticationTicket() : connectionString.DeserializeJson<WebDavAuthenticationTicket>();
            var provider = new CloudMeProvider();
            return authenticationBroker.FormAuthenticationBrokerAsync(async (server, domain, user, password, ignoreCertErrors) =>
                {
                    try
                    {
                        var client = new WebDavClient(CloudMeServer, domain, user, password);
                        var r = await client.PropFindAsync(string.Format(CloudMeServerPath, user), WebDavDepth.Zero);
                        var options = await client.OptionsAsync(GetDirRelativePath(string.Format(CloudMeServerPath, user), ""), CancellationToken.None);
                        return new AuthenticatonTicket { AuthToken = new WebDavAuthenticationTicket { User = user, Password = password }.SerializeJson(), Tag = options };
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

        #region ** implementation

        public override Task<bool> CanGetDirectoryLinkAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override Task<Uri> GetDirectoryLinkAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new Uri(string.Format("https://www.cloudme.com/en#sync:/Documents/{0}", string.Join("/", Path.SplitPath(dirId).Select(s => Uri.EscapeUriString(s)).ToArray()))));
        }

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(dirId));
        }

        protected override Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(dirId != CloudMeFolder);
        }

        protected override Task<bool> CanCopyDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        protected override Task<bool> CanCopyFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(targetDirId));
        }

        protected override Task<bool> CanMoveDirectoryOverride(string sourceDirId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(targetDirId));
        }

        protected override Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(targetDirId));
        }

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(dirId != CloudMeFolder);
        }

        #endregion
    }
}
