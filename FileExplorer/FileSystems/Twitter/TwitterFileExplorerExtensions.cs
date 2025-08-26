using System.Collections.Generic;
using System.Threading.Tasks;

using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class TwitterFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public TwitterFileExplorerExtensions(FileExplorerViewModel explorer, TwitterProvider provider)
            : base(explorer, provider, false, false)
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

        //public override FileSystemDirectory CreateDirectory(string dirId, string name, IEnumerable<string> usedNames)
        //{
        //    throw new System.NotImplementedException();
        //}

        //public override FileSystemFile CreateFile(string dirId, string name, string contentType, IEnumerable<string> usedNames)
        //{
        //    return new TwitterFile() { Name = GetUniqueFileName(name, contentType, usedNames) };
        //}

        public override string GetEmptyDirectoryMessage(string directoryId)
        {
            return TwitterResources.EmptyTimelineMessage;
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            return new TwitterFileViewModel(FileExplorer, dirId, item, file);
        }

        #region ** actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();
            await AddOpenFileAction(context, actions);
            await AddOpenFileLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new TwitterProvider().Name), isDefault: true);
            await AddUploadFiles(context,
                actions,
                caption: GlobalResources.UploadPhotosLabel);

            await AddDownloadFile(context, actions);
            await AddDeleteFiles(context,
                actions,
                questionSingular: TwitterResources.DeleteTweetQuestion,
                questionPlural: TwitterResources.DeleteTweetsQuestion);
            await AddShareFileAction(context, actions);
            await AddRefreshAction(context, actions);
            await AddSearchAction(context, actions);
            await AddOnlineModeAction(context, actions);
            return actions;
        }

        #endregion
    }
}