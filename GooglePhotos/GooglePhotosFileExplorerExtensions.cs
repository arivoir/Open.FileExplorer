using System.Collections.Generic;
using System.Threading.Tasks;

using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class GooglePhotosFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public GooglePhotosFileExplorerExtensions(FileExplorerViewModel explorer, GooglePhotosProvider provider)
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
                return false;
            }
        }

        public override bool UseExtension
        {
            get
            {
                return false;
            }
        }

        public override FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, IEnumerable<string> usedNames)
        {
            return new GooglePhotosAlbum(id, GetUniqueDirectoryName(name, usedNames));
        }

        public override FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames)
        {
            return new GooglePhotosPhoto(id, Path.GetFileNameWithoutExtension(GetUniqueFileName(name, contentType, usedNames)), contentType);
        }

        protected override string GetUniqueDirectoryName(string name, IEnumerable<string> usedNames)
        {
            name = name.Replace(Path.GetInvalidPathChars(), '_').Trim('.');
            return base.GetUniqueDirectoryName(name, usedNames);
        }

        protected override string GetUniqueFileName(string name, string contentType, IEnumerable<string> usedNames)
        {
            name = name.Replace(Path.GetInvalidPathChars(), '_').Trim('.');
            return base.GetUniqueFileName(name, contentType, usedNames);
        }

        public override string GetEmptyDirectoryMessage(string directoryId)
        {
            return GlobalResources.EmptyAlbumMessage;
        }

        public override bool FilesHaveSize(string dirId)
        {
            return false;
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                var parent = Path.GetParentPath(dirId);
                if (string.IsNullOrWhiteSpace(parent))
                    return new GooglePhotosDirectoryViewModel(FileExplorer, dirId, item);
                else
                    return new GooglePhotosAlbumViewModel(FileExplorer, dirId, item);
            }
            else
            {
                return new GooglePhotosPhotoViewModel(FileExplorer, dirId, item, file);
            }
        }

        #region actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            AddOpenDirectoryAction(context, actions);
            await AddOpenDirectoryLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new GooglePhotosProvider().Name));
            await AddOpenFileAction(context, actions);
            await AddOpenFileLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new GooglePhotosProvider().Name));

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
                questionSingular: GlobalResources.DeletePhotoQuestion.Format(1, new GooglePhotosProvider().Name),
                questionPlural: GlobalResources.DeletePhotosQuestion.Format(1, new GooglePhotosProvider().Name));
            await AddDeleteDirectories(context,
                actions,
                questionSingular: GlobalResources.DeleteAlbumQuestion.Format(1, new GooglePhotosProvider().Name),
                questionPlural: GlobalResources.DeleteAlbumsQuestion.Format(1, new GooglePhotosProvider().Name));

            await AddDirectoryProperties(context, actions);
            await AddFileProperties(context, actions);

            await AddRefreshAction(context, actions);
            await AddSearchAction(context, actions);
            await AddOnlineModeAction(context, actions);

            return actions;
        }

        #endregion
    }
}