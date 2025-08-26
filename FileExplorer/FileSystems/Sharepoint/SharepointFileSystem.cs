using Open.FileSystemAsync;
using Open.WebDav;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class SharepointFileSystem : WebDavFileSystem
    {
        #region ** fields

        private CookieContainer cookieJar = new CookieContainer();

        #endregion

        #region ** authentication

        public override Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var provider = new SharepointProvider();
            return authenticationBroker.FormAuthenticationBrokerAsync(async (server, domain, user, password, ignoreCertErrors) =>
                {
                    var cookieContainer = new CookieContainer();
                    await AuthenticateForm(server, user, password, domain, cookieContainer);
                    var client = new WebDavClient(server, domain, user, password);
                    var r = await client.PropFindAsync("", WebDavDepth.Zero);
                    return new AuthenticatonTicket { AuthToken = string.Format("{0}:{1}:{2}:{3}", Uri.EscapeDataString(server), Uri.EscapeDataString(domain), Uri.EscapeDataString(user), Uri.EscapeDataString(password), ignoreCertErrors) };
                }, provider.Name, provider.Color, provider.IconResourceKey);
        }

        private static Task AuthenticateForm(string tfsServerAddress, string user, string password, string domain, CookieContainer cookieJar)
        {
            try
            {
                //var uri = new Uri(string.Format("{0}/_vti_bin/authentication.asmx", tfsServerAddress));
                //var request = HttpWebRequest.Create(uri) as HttpWebRequest;
                //request.CookieContainer = cookieJar;
                //request.Headers["SOAPAction"] = "http://schemas.microsoft.com/sharepoint/soap/Login";
                //request.ContentType = "text/xml; charset=utf-8";
                //request.Method = "POST";
                //var requestStrem = await request.GetRequestStreamAsync();
                //string envelope = @"<?xml version=""1.0"" encoding=""utf-8""?> <soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""> <soap:Body> <Login xmlns=""http://schemas.microsoft.com/sharepoint/soap/""> <username>{0}</username> <password>{1}</password> </Login> </soap:Body> </soap:Envelope>";
                //var doc = XDocument.Parse(string.Format(envelope, domain + @"\" + user, password));
                //doc.Save(requestStrem);
                //requestStrem.Dispose();
                //await request.GetResponseAsync();
            }
            catch
            {
            }
            return Task.FromResult(true);
        }
        #endregion

    }
}
