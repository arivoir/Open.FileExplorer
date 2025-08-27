using System.Collections.Generic;
using System.Threading.Tasks;
using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class YouTubeFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public YouTubeFileExplorerExtensions(FileExplorerViewModel explorer, YouTubeProvider provider)
            : base(explorer, provider)
        {
        }

        public override FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, IEnumerable<string> usedNames)
        {
            return new YoutubePlaylist(id, GetUniqueDirectoryName(name, usedNames), "public");
        }

        public override FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames)
        {
            return new YouTubeVideo(id, Path.GetFileNameWithoutExtension(GetUniqueFileName(name, contentType, usedNames)), contentType);
        }

        public override string GetEmptyDirectoryMessage(string directoryId)
        {
            return YouTubeResources.EmptyPlaylistMessage;
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                if (item is YoutubePlaylist)
                {
                    return new YouTubePlaylistViewModel(FileExplorer, dirId, item);
                }
                else
                {
                    return base.CreateViewModel(dirId, item);
                }
            }
            else
            {
                return new YouTubeVideoViewModel(FileExplorer, dirId, item, file);
            }
        }

        public override bool FilesHaveSize(string dirId)
        {
            return false;
        }

        #region actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            AddOpenDirectoryAction(context, actions);
            await AddOpenFileAction(context, actions);
            await AddOpenFileLinkAction(context, actions, YouTubeResources.OpenVideoLabel, isDefault: true);

            await AddShareDirectoryAction(context, actions);
            await AddShareFileAction(context, actions);

            await AddUploadFiles(context,
                    actions,
                    caption: GlobalResources.UploadVideosLabel);

            await AddCreateDirectory(context,
                actions,
                YouTubeResources.CreatePlaylistLabel);

            await AddDeleteFiles(context,
                actions,
                questionSingular: YouTubeResources.DeleteVideoQuestion,
                questionPlural: YouTubeResources.DeleteVideosQuestion);
            await AddDeleteDirectories(context,
                actions,
                questionSingular: YouTubeResources.DeletePlaylistQuestion,
                questionPlural: YouTubeResources.DeletePlaylistsQuestion);

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