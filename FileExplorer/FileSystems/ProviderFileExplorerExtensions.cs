using System.Collections.Generic;
using System.Threading.Tasks;

using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class ProviderFileExplorerExtensions : FileExplorerExtensions
    {
        protected IProvider Provider { get; set; }
        private bool _filesHaveSize;

        public ProviderFileExplorerExtensions(FileExplorerViewModel explorer, IProvider provider, bool useExtension = true, bool filesHaveSize = true)
            : base(explorer, useExtension)
        {
            Provider = provider;
            _filesHaveSize = filesHaveSize;
        }

        public override Task<string> GetBackgroundTemplateKey(string directoryId)
        {
            return Task.FromResult(Provider.IconResourceKey);
        }

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            AddOpenDirectoryAction(context, actions);
            await AddOpenFileAction(context, actions);
            await AddOpenDirectoryLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, Provider.Name));
            await AddOpenFileLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, Provider.Name), isDefault: true);

            await AddShareDirectoryAction(context, actions);
            await AddShareFileAction(context, actions);

            await AddUploadFiles(context, actions);
            await AddDownloadFile(context, actions);
            await AddCreateDirectory(context, actions);

            await AddCopy(context, targetDirectoryId, actions);
            await AddMove(context, targetDirectoryId, actions);

            await AddDeleteFiles(context,
                actions,
                questionSingular: GlobalResources.DeleteFileQuestion.Format(1, Provider.Name),
                questionPlural: GlobalResources.DeleteFilesQuestion.Format(1, Provider.Name));
            await AddDeleteDirectories(context,
                actions,
                questionSingular: GlobalResources.DeleteFolderQuestion.Format(1, Provider.Name),
                questionPlural: GlobalResources.DeleteFoldersQuestion.Format(1, Provider.Name));
            await AddEmptyTrash(context, actions);

            await AddDirectoryProperties(context, actions);
            await AddFileProperties(context, actions);

            await AddSortAction(context, actions);
            await AddRefreshAction(context, actions);
            await AddSearchAction(context, actions);
            await AddOnlineModeAction(context, actions);

            return actions;
        }

        public override bool FilesHaveSize(string dirId)
        {
            return _filesHaveSize;
        }
    }
}