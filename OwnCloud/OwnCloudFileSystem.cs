using Open.FileExplorer.WebDav;
using Open.FileSystemAsync;
using System.Text.Json;

namespace Open.FileExplorer.OwnCloud
{
    public class OwnCloudFileSystem : WebDavFileSystem
    {
        #region fields

        private static string OwnCloudWebDavPath = "remote.php/webdav";

        #endregion

        #region authentication

        public override Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var ticket = string.IsNullOrWhiteSpace(connectionString) ? new WebDavAuthenticationTicket() : JsonSerializer.Deserialize<WebDavAuthenticationTicket>(connectionString);
            var provider = new OwnCloudProvider();
            return authenticationBroker.FormAuthenticationBrokerAsync(async (server, domain, user, password, ignoreCertErrors) =>
                {
                    server = server.EndsWith("/") ? server : server + "/";
                    if (!server.Contains(OwnCloudWebDavPath))
                        server += OwnCloudWebDavPath;
                    return await WebDavFileSystem.LogInAsync(server, domain, user, password, ignoreCertErrors);
                }, 
                provider.Name, 
                provider.Color, 
                provider.IconResourceKey,
                server: ticket.Server,
                domain: ticket.Domain,
                user: ticket.User,
                password: ticket.Password,
                showServer: true, 
                showDomain: false);
        }

        #endregion
    }
}
