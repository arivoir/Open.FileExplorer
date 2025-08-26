using Open.FileSystem;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FacebookPhotoViewModel : FileSystemFileViewModel
    {
        #region ** initialization

        public FacebookPhotoViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file = null)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        #region ** object model

        protected override bool NameIsRequired
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                return "FacebookPhotoFormTemplate";
            }
        }

        #endregion

        #region ** labels

        public string DescriptionLabel
        {
            get
            {
                return FacebookResources.DescriptionLabel;
            }
        }

        #endregion
    }
}
