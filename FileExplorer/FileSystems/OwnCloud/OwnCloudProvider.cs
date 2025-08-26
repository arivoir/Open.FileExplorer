using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class OwnCloudProvider : WebDavProvider
    {
        #region ** object model

        public override string Name
        {
            get { return "OwnCloud"; }
        }

        public override string Color
        {
            get { return "#FF1D2D44"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new OwnCloudFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
