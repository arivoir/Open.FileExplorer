using Open.FileSystemAsync;

namespace Open.FileExplorer.GoogleDrive
{
    /// <summary>
    /// Provides methods to create a new connection to Google Drive.
    /// </summary>
    public class GoogleDriveProvider : Provider
    {
        #region object model

        public override string Name
        {
            get { return "Google Drive"; }
        }

        public override string Color
        {
            get { return "#FFDEA715"; }
        }

        #endregion

        #region methods

        public override AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager)
        {
            return new GoogleDriveFileSystem() { AuthenticationManager = authenticationManager };
        }

        public override IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new GoogleDriveFileExplorerExtensions(explorer, this);
        }

        #endregion
    }
}
