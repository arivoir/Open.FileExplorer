using Open.FileSystemAsync;

namespace Open.FileExplorer.WebDav
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class WebDavProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "WebDav"; }
        }

        public override string Color
        {
            get { return "#FF33427E"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new WebDavFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
