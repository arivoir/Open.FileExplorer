using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class GoogleDriveDirectoryViewModel : FileSystemDirectoryViewModel
    {
        #region initialization

        public GoogleDriveDirectoryViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        //public override Brush BackgroundColor
        //{
        //    get
        //    {
        //        if (Item.Id == "root" ||
        //            Item.Id == "trashed" ||
        //            Item.Id == "sharedWithMe" ||
        //            Item.Id == "starred")
        //        {
        //            return FileSystemDirectoryViewModel.SpecialDirectoryBackgroundBrush;
        //        }
        //        else
        //        {
        //            return base.BackgroundColor;
        //        }
        //    }
        //}

        //public override TextAlignment NameTextAlignment
        //{
        //    get
        //    {
        //        if (Item.Id == "root" ||
        //            Item.Id == "trashed" ||
        //            Item.Id == "sharedWithMe" ||
        //            Item.Id == "starred")
        //        {
        //            return TextAlignment.Center;
        //        }
        //        else
        //        {
        //            return base.NameTextAlignment;
        //        }
        //    }
        //}

        public override string Icon
        {
            get
            {
                var directory = Item as FileSystemDirectory;
                if (directory.Id == GoogleDriveFileSystem.Root)
                {
                    return "GoogleDriveIcon";
                }
                else if (directory.Id == GoogleDriveFileSystem.Trashed)
                {
                    return "TrashIcon";
                }
                else if (directory.Id == GoogleDriveFileSystem.Photos)
                {
                    return "GooglePhotosIcon";
                }
                else if (directory.Id == GoogleDriveFileSystem.SharedWithMe)
                {
                    return "SharedIcon";
                }
                else if (directory.Id == GoogleDriveFileSystem.Starred)
                {
                    return "StarIcon";
                }
                return base.Icon;
            }
        }
    }
}
