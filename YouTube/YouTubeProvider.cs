using Open.FileSystemAsync;

namespace Open.FileExplorer.YouTube
{
    /// <summary>
    /// Provides methods to create a new connection to You Tube.
    /// </summary>
    public class YouTubeProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "YouTube"; }
        }

        public override string Color
        {
            get { return "#FFC8312B"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new YouTubeFileSystem() { AuthenticationManager = authenticationManager };
        }
        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new YouTubeFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
