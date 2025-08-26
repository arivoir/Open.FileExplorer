using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class GoogleDriveFileViewModel : FileSystemFileViewModel
    {
        #region ** initialization

        public GoogleDriveFileViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        public override string ItemTemplate
        {
            get
            {
                var file = Item as FileSystemFile;
                if (file.ContentType == "image/jpeg" ||
                    file.ContentType == "image/png" ||
                    file.ContentType == "image/bmp")
                {
                    return "PhotoTemplate";
                }
                return "FileTemplate";
            }
        }

        public override string SmallItemTemplate
        {
            get
            {
                var file = Item as FileSystemFile;
                if (file.ContentType == "image/jpeg" ||
                    file.ContentType == "image/png" ||
                    file.ContentType == "image/bmp")
                {
                    return "SmallPhotoTemplate";
                }
                return "SmallFileTemplate";
            }
        }

        public override Task<string> GetSmallItemTemplateWithoutName()
        {
            var file = Item as FileSystemFile;
            if (file.ContentType == "image/jpeg" ||
                file.ContentType == "image/png" ||
                file.ContentType == "image/bmp")
            {
                return Task.FromResult("SmallPhotoTemplateWithoutName");
            }
            return Task.FromResult("SmallFileTemplateWithoutName");
        }

        #region ** properties

        public override string Extension
        {
            get
            {
                var extension = (Item as GoogleDriveFile)?.FileExtension;
                if (extension == null)
                    extension = base.Extension;
                return extension;
            }
        }

        #endregion
    }
}
