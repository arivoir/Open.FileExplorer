using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Text;

namespace Open.FileExplorer
{
    public class GooglePhotosDirectoryViewModel : FileSystemDirectoryViewModel
    {
        public GooglePhotosDirectoryViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        public override string Icon
        {
            get
            {
                var directory = Item as FileSystemDirectory;
                if (directory.Id == GooglePhotosFileSystem.Photos)
                {
                    return "MyPicturesIcon";
                }
                else if (directory.Id == GooglePhotosFileSystem.Albums)
                {
                    return "MyAlbumsIcon";
                }
                else if (directory.Id == GooglePhotosFileSystem.Sharing)
                {
                    return "SharedIcon";
                }
                return base.Icon;
            }
        }
    }
}
