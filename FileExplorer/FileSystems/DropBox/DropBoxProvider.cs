using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to DropBox.
    /// </summary>
    public class DropBoxProvider : Provider
    {
        #region ** object model

        public override string Name
        {
            get { return "Dropbox"; }
        }

        public override string Color
        {
            get { return "#FF008BD3"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new DropBoxFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
