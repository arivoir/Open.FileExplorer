using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class HiDriveProvider : WebDavProvider
    {
        #region ** object model

        public override string Name
        {
            get { return "HiDrive"; }
        }

        public override string Color
        {
            get { return "#FFEC7703"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new HiDriveFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
