using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class OneDriveFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        private static char[] INVALID_PATH_CHARS = new char[] { '/', '\\', ':', ';', '*', '?', '"', '<', '>', '|' };

        public OneDriveFileExplorerExtensions(FileExplorerViewModel explorer, OneDriveProvider provider)
            : base(explorer, provider)
        {
        }

        public override FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, IEnumerable<string> usedNames)
        {
            return new OneDriveDirectory(id, GetUniqueDirectoryName(name, usedNames));
        }

        public override FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames)
        {
            return new OneDriveFile(id, GetUniqueFileName(name, contentType, usedNames), contentType);
        }

        protected override string GetUniqueFileName(string name, string contentType, IEnumerable<string> usedNames)
        {
            name = name.Replace(INVALID_PATH_CHARS, '_').Trim('.');
            return base.GetUniqueFileName(name, contentType, usedNames);
        }

        protected override string GetUniqueDirectoryName(string name, IEnumerable<string> usedNames)
        {
            name = name.Replace(INVALID_PATH_CHARS, '_').Trim('.');
            return base.GetUniqueDirectoryName(name, usedNames);
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                return new OneDriveDirectoryViewModel(FileExplorer, dirId, item);
            }
            else
            {
                return new OneDriveFileViewModel(FileExplorer, dirId, item, file);
            }
        }

        #region ** actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            AddOpenDirectoryAction(context, actions);
            await AddOpenDirectoryLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new OneDriveProvider().Name));
            await AddOpenFileAction(context, actions);
            await AddOpenFileLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new OneDriveProvider().Name), isDefault: true);

            await AddShareDirectoryAction(context, actions);
            await AddShareFileAction(context, actions);

            string caption = FileSystemResources.UploadFilesLabel;
            //if ((context.Directories.Count() == 1 && context.Files.Count() == 0) ||
            //    !string.IsNullOrWhiteSpace(context.BaseDirectory))
            //{
            //    var directory = context.Directories.First() as SkyDriveDirectory;
            //    if (directory.Type == "album")
            //    {
            //        filter = FacebookResources.ImageFiles + "|*.gif;*.jpg;*.jpeg;*.png;*.psd;*.tiff;*.jp2;*.iff;*.wbmp;*.xbm";
            //        caption = FacebookResources.UploadPhotos;
            //    }
            //}
            await AddUploadFiles(context,
                actions,
                caption: caption);

            await AddCreateDirectory(context, actions);
            await AddDownloadFile(context, actions);

            await AddCopy(context, targetDirectoryId, actions);
            await AddMove(context, targetDirectoryId, actions);

            await AddDeleteFiles(context,
                actions,
                questionSingular: GlobalResources.DeleteFileQuestion.Format(1, new OneDriveProvider().Name),
                questionPlural: GlobalResources.DeleteFilesQuestion.Format(1, new OneDriveProvider().Name));
            await AddDeleteDirectories(context,
                actions,
                questionSingular: GlobalResources.DeleteFolderQuestion.Format(1, new OneDriveProvider().Name),
                questionPlural: GlobalResources.DeleteFoldersQuestion.Format(1, new OneDriveProvider().Name));

            await AddFileProperties(context, actions);
            await AddDirectoryProperties(context, actions);
            await AddRefreshAction(context, actions);
            await AddSearchAction(context, actions);
            await AddOnlineModeAction(context, actions);
            await AddSortAction(context, actions);

            return actions;
        }

        #endregion
    }
}