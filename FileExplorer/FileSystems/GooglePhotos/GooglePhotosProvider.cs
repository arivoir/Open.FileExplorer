using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Google+ (Picasa).
    /// </summary>
    public class GooglePhotosProvider : Provider
    {
        #region ** object model

        public override string Name
        {
            get { return "Google Photos"; }
        }

        public override string Color
        {
            get { return "#FFDD4B39"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new GooglePhotosFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new GooglePhotosFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
