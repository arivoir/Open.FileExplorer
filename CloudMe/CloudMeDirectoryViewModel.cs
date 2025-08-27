using Open.FileExplorer.WebDav;
using Open.FileSystemAsync;

namespace Open.FileExplorer.CloudMe
{
    public class CloudMeDirectoryViewModel : FileSystemDirectoryViewModel
    {
        #region initialization

        public CloudMeDirectoryViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        public override string Icon
        {
            get
            {
                var directory = Item as WebDavDirectory;
                if (directory != null && directory.FullPath.EndsWith("CloudDrive/Documents/CloudMe/"))
                {
                    return "CloudMeIcon";
                }
                return base.Icon;
            }
        }

        public override string BackgroundColor
        {
            get
            {
                var directory = Item as WebDavDirectory;
                if (directory != null && directory.FullPath.EndsWith("CloudDrive/Documents/CloudMe/"))
                {
                    return new CloudMeProvider().Color;
                }
                else
                {
                    return base.BackgroundColor;
                }
            }
        }
    }
}
