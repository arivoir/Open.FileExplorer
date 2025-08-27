using Open.FileExplorer.WebDav;
using Open.FileSystemAsync;

namespace Open.FileExplorer.HiDrive
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class HiDriveProvider : WebDavProvider
    {
        #region object model

        public override string Name
        {
            get { return "HiDrive"; }
        }

        public override string Color
        {
            get { return "#FFEC7703"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new HiDriveFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
