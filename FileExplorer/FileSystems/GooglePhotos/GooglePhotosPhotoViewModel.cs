using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class GooglePhotosPhotoViewModel : FileSystemFileViewModel
    {
        #region ** fields

        private string _oldSummary;

        #endregion

        #region ** initialization

        public GooglePhotosPhotoViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        #region ** object model

        //protected override bool NameIsRequired
        //{
        //    get
        //    {
        //        return false;
        //    }
        //}

        public string Summary
        {
            get
            {
                return (Item as GooglePhotosPhoto).Summary;
            }
            set
            {
                (Item as GooglePhotosPhoto).Summary = value;
            }
        }

        #endregion

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                return "GooglePhotosPhotoFormTemplate";
            }
        }

        #endregion

        #region ** labels

        public string TitleLabel
        {
            get
            {
                return GooglePhotosResources.TitleLabel;
            }
        }

        public string SummaryLabel
        {
            get
            {
                return GooglePhotosResources.SummaryLabel;
            }
        }

        #endregion

        #region ** versions

        public override bool HasChanges()
        {
            return base.HasChanges() ||
                _oldSummary != (Item as GooglePhotosPhoto).Summary;
        }

        public override void BeginChanging()
        {
            base.BeginChanging();
            _oldSummary = (Item as GooglePhotosPhoto).Summary;
        }

        public override void UndoChanges()
        {
            base.UndoChanges();
            (Item as GooglePhotosPhoto).Summary = _oldSummary;
        }

        #endregion
    }
}
