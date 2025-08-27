using Open.FileExplorer.OneDrive.Strings;
using Open.FileSystemAsync;

namespace Open.FileExplorer.OneDrive
{
    public class OneDriveFileViewModel : FileSystemFileViewModel
    {
        #region fields

        private string _oldDescription;

        #endregion

        #region initialization

        public OneDriveFileViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        #region object model

        public string Description
        {
            get
            {
                return (Item as OneDriveFile).Description;
            }
            set
            {
                (Item as OneDriveFile).Description = value;
            }
        }

        #endregion

        #region templates

        public override string FormTemplate
        {
            get
            {
                return "OneDriveFileFormTemplate";
            }
        }

        public override string NewFormTemplate
        {
            get
            {
                return "FileFormTemplate";
            }
        }

        #endregion

        #region labels

        public string DescriptionLabel
        {
            get
            {
                return OneDriveResources.DescriptionLabel;
            }
        }

        #endregion

        #region versions

        public override bool HasChanges()
        {
            return base.HasChanges() ||
                _oldDescription != (Item as OneDriveFile).Description;
        }

        public override void BeginChanging()
        {
            base.BeginChanging();
            _oldDescription = (Item as OneDriveFile).Description;
        }

        public override void UndoChanges()
        {
            base.UndoChanges();
            (Item as OneDriveFile).Description = _oldDescription;
        }

        #endregion
    }
}
