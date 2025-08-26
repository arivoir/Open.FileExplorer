using Open.FileSystem;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Facebook.
    /// </summary>
    public class FacebookProvider : Provider
    {
        #region ** object model

        public override string Name
        {
            get { return "Facebook"; }
        }

        public override string Color
        {
            get { return "#FF3A579A"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new FacebookFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new FacebookFileExplorerExtensions(explorer, this);
        }
        #endregion
    }
}
