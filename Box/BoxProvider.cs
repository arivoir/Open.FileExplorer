using Open.FileSystemAsync;

namespace Open.FileExplorer.Box
{
    /// <summary>
    /// Provides methods to create a new connection to Box.
    /// </summary>
    public class BoxProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "Box"; }
        }

        public override string Color
        {
            get { return "#FF2B80BF"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new BoxFileSystem() { AuthenticationManager = authenticationManager };
        }

        #endregion
    }
}
