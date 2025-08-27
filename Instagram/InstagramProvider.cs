using Open.FileSystemAsync;

namespace Open.FileExplorer.Instagram
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class InstagramProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "Instagram"; }
        }

        public override string Color
        {
            get { return "#FF614435"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new InstagramFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new ProviderFileExplorerExtensions(explorer, this, useExtension: false, filesHaveSize: false);
        }

        #endregion
    }
}
