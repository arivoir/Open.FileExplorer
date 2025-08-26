using Open.FileSystemAsync;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class FourSharedProvider : WebDavProvider
    {
        #region ** object model

        public override string Name
        {
            get { return "4Shared"; }
        }

        public override string Color
        {
            get { return "#FF7297B5"; }
        }

        public override string IconResourceKey
        {
            get
            {
                return "FourSharedIcon";
            }
        }

        #endregion

        #region ** methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new FourSharedFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
