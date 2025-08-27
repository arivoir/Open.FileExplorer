using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class TwitterProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "Twitter"; }
        }

        public override string Color
        {
            get { return "#FF00ACED"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new TwitterFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new TwitterFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
