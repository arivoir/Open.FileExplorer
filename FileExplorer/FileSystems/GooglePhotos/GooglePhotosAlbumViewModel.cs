using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class GooglePhotosAlbumViewModel : FileSystemDirectoryViewModel
    {
        #region ** fields

        private string _oldDescription;

        #endregion

        #region ** initialization

        public GooglePhotosAlbumViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        #region ** object model

        public string Description
        {
            get
            {
                return (Item as GooglePhotosAlbum).Description;
            }
            set
            {
                (Item as GooglePhotosAlbum).Description = value;
            }
        }

        #endregion

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                return "GooglePhotosAlbumFormTemplate";
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
                _oldDescription != (Item as GooglePhotosAlbum).Description;
        }

        public override void BeginChanging()
        {
            base.BeginChanging();
            _oldDescription = (Item as GooglePhotosAlbum).Description;
        }

        public override void UndoChanges()
        {
            base.UndoChanges();
            (Item as GooglePhotosAlbum).Description = _oldDescription;
        }

        #endregion
    }
}
