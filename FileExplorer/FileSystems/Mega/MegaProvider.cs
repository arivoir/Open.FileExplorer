using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Mega.
    /// </summary>
    public class MegaProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "Mega"; }
        }

        public override string Color
        {
            get { return "#FFD81A28"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new MegaFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new MegaFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
