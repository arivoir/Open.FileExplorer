using System.Collections.Generic;
using System.Threading.Tasks;
using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class FlickrFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public FlickrFileExplorerExtensions(FileExplorerViewModel explorer, FlickrProvider provider)
            : base(explorer, provider)
        {
        }

        public override bool AllowsDuplicatedNames
        {
            get
            {
                return true;
            }
        }

        public override bool NameRequired
        {
            get
            {
                return true;
            }
        }

        public override bool UseExtension
        {
            get
            {
                return false;
            }
        }

        //public override FileSystemDirectory CreateDirectory(string dirId, string id, string name, IEnumerable<string> usedNames)
        //{
        //    return new FlickrAlbum(id, GetUniqueDirectoryName(name, usedNames));
        //}

        public override FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames)
        {
            return new FlickrPhoto() { Name = Path.GetFileNameWithoutExtension(GetUniqueFileName(name, contentType, usedNames)), IsPublic = true, SafetyLevel = 1 };
        }

        public override string GetEmptyDirectoryMessage(string directoryId)
        {
            return GlobalResources.EmptyAlbumMessage;
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                return base.CreateViewModel(dirId, item, file);
            }
            else
            {
                return new FlickrPhotoViewModel(FileExplorer, dirId, item, file);
            }
        }

        #region actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            AddOpenDirectoryAction(context, actions);
            await AddOpenDirectoryLinkAction(context, actions, FlickrResources.OpenAlbumLabel);
            await AddOpenFileAction(context, actions);
            await AddOpenFileLinkAction(context, actions, FlickrResources.OpenPhotoLabel);

            await AddShareDirectoryAction(context, actions);
            await AddShareFileAction(context, actions);

            await AddUploadFiles(context,
                actions,
                caption: GlobalResources.UploadPhotosLabel);

            await AddCreateDirectory(context,
                actions,
                caption: GlobalResources.CreateAlbumLabel);

            await AddDownloadFile(context, actions);

            await AddDeleteFiles(context,
                actions,
                questionSingular: FlickrResources.DeleteFileQuestion,
                questionPlural: FlickrResources.DeleteFilesQuestion);
            await AddDeleteDirectories(context,
                actions,
                questionSingular: FlickrResources.DeleteDirectoryQuestion,
                questionPlural: FlickrResources.DeleteDirectoriesQuestion);

            //AddDirectoryProperties(context,
            //    actions,
            //    directory => new FlickrAlbumViewModel() { Data = directory as FlickrAlbum });
            await AddFileProperties(context,
                actions);

            await AddRefreshAction(context, actions);
            await AddSearchAction(context, actions);
            await AddOnlineModeAction(context, actions);

            return actions;
        }

        #endregion

    }
}