using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to SkyDrive.
    /// </summary>
    public class OneDriveProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "OneDrive"; }
        }

        public override string Color
        {
            get { return "#FF094AB2"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new OneDriveFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new OneDriveFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
