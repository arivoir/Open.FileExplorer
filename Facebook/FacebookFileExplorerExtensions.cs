using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Open.FileSystem;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class FacebookFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public FacebookFileExplorerExtensions(FileExplorerViewModel explorer, FacebookProvider provider)
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

        public override bool FilesHaveSize(string dirId)
        {
            return false;
        }

        public override FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, IEnumerable<string> usedNames)
        {
            return new FacebookAlbum("", GetUniqueDirectoryName(name, usedNames), FacebookPermission.Public);
        }

        public override FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames)
        {
            return new FacebookPhoto();
        }

        public override string GetEmptyDirectoryMessage(string dirId)
        {
            if (dirId.EndsWith(FacebookFileSystem.Videos))
            {
                return FacebookResources.EmptyUploadedVideosMessage;
            }
            else if (dirId.EndsWith(FacebookFileSystem.PhotosOfYou))
            {
                return FacebookResources.EmptyPhotoOfYouMessage;
            }
            else if (dirId.EndsWith(FacebookFileSystem.YourPhotos))
            {
                return FacebookResources.EmptyYourPhotosMessage;
            }
            else if (dirId.EndsWith(FacebookFileSystem.Albums))
            {
                return FacebookResources.EmptyAlbumsMessage;
            }
            return GlobalResources.EmptyAlbumMessage;
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                return new FacebookAlbumViewModel(FileExplorer, dirId, item);
            }
            else
            {
                return new FacebookPhotoViewModel(FileExplorer, dirId, item, file);
            }
        }

        #region ** actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            var contextGroup = context.Groups.FirstOrDefault();
            AddOpenDirectoryAction(context, actions);
            await AddOpenDirectoryLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new FacebookProvider().Name));
            await AddOpenFileAction(context, actions);
            await AddOpenFileLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new FacebookProvider().Name));

            await AddShareDirectoryAction(context, actions);
            await AddShareFileAction(context, actions);

            await AddUploadFiles(context,
                actions,
                caption: GlobalResources.UploadPhotosLabel,
                showUploadForm: true);

            await AddCreateDirectory(context,
                actions,
                caption: GlobalResources.CreateAlbumLabel);

            await AddDownloadFile(context, actions);

            await AddDeleteFiles(context,
                actions,
                questionSingular: FacebookResources.DeletePhotoQuestion,
                questionPlural: FacebookResources.DeletePhotosQuestion);
            await AddDeleteDirectories(context,
                actions,
                questionSingular: GlobalResources.DeleteAlbumQuestion.Format(1, new FacebookProvider().Name),
                questionPlural: GlobalResources.DeleteAlbumsQuestion.Format(1, new FacebookProvider().Name));

            await AddDirectoryProperties(context, actions);
            await AddFileProperties(context, actions);

            await AddRefreshAction(context, actions);
            await AddSearchAction(context, actions);
            await AddOnlineModeAction(context, actions);

            return actions;
        }


        private bool IsRootDir(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            return string.IsNullOrWhiteSpace(subPath);
        }

        private FacebookFileSystem GetActualFileSystem(string dirPath, out string subPath)
        {
            if (FileSystem is GlobalFileSystem)
            {
                string connectionName;
                var dir = (FileSystem as GlobalFileSystem).GetConnection(dirPath, out connectionName, out subPath);
                return dir.FileSystem as FacebookFileSystem;
            }
            subPath = null;
            return null;
        }

        #endregion

    }
}