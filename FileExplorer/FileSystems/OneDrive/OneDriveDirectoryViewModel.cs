using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class OneDriveDirectoryViewModel : FileSystemDirectoryViewModel
    {
        #region ** fields

        private string _oldDescription;

        #endregion

        #region ** initialization

        public OneDriveDirectoryViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        #region ** object model

        public string Description
        {
            get
            {
                return (Item as OneDriveDirectory).Description;
            }
            set
            {
                (Item as OneDriveDirectory).Description = value;
            }
        }

        public override string Icon
        {
            get
            {
                var directory = Item as OneDriveDirectory;
                if (directory != null)
                {
                    if (directory.SpecialFolder == OneDriveDirectory.Documents)
                    {
                        return "MyDocumentsIcon";
                    }
                    else if (directory.SpecialFolder == OneDriveDirectory.Photos)
                    {
                        return "MyPicturesIcon";
                    }
                    else if (directory.SpecialFolder == OneDriveDirectory.CameraRol)
                    {
                        return "CameraIcon";
                    }
                    else if (directory.SpecialFolder == OneDriveDirectory.Music)
                    {
                        return "MusicIcon";
                    }
                    else if (directory.SpecialFolder == OneDriveDirectory.Favorites)
                    {
                        return "StarIcon";
                    }
                }
                return base.Icon;
            }
        }

        #endregion

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                return "OneDriveDirectoryFormTemplate";
            }
        }

        #endregion

        #region ** labels

        public string DescriptionLabel
        {
            get
            {
                return OneDriveResources.DescriptionLabel;
            }
        }

        #endregion

        #region ** versions

        public override bool HasChanges()
        {
            return base.HasChanges() ||
                _oldDescription != (Item as OneDriveDirectory).Description;
        }

        public override void BeginChanging()
        {
            base.BeginChanging();
            _oldDescription = (Item as OneDriveDirectory).Description;
        }

        public override void UndoChanges()
        {
            base.UndoChanges();
            (Item as OneDriveDirectory).Description = _oldDescription;
        }

        #endregion
    }
}
