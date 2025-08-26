using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class FlickrProvider : Provider
    {
        #region ** object model

        public override string Name
        {
            get { return "Flickr"; }
        }

        public override string Color
        {
            get { return "#FFFF0084"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new FlickrFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new FlickrFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
