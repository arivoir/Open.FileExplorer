using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class SharepointProvider : WebDavProvider
    {
        #region ** object model

        public override string Name
        {
            get { return "Sharepoint"; }
        }

        public override string Color
        {
            get { return "#FF147EE5"; }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new SharepointFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
