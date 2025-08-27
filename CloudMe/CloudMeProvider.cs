using Open.FileExplorer.WebDav;
using Open.FileSystemAsync;

namespace Open.FileExplorer.CloudMe
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class CloudMeProvider : WebDavProvider
    {
        #region object model

        public override string Name
        {
            get { return "CloudMe"; }
        }

        public override string Color
        {
            get { return "#FF3A6CC1"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new CloudMeFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new CloudMeExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
