using Open.FileExplorer.GoogleDrive.Strings;
using Open.FileExplorer.Strings;
using Open.FileSystemAsync;

namespace Open.FileExplorer.GoogleDrive
{
    internal class GoogleDriveFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public GoogleDriveFileExplorerExtensions(FileExplorerViewModel explorer, GoogleDriveProvider provider)
            : base(explorer, provider)
        {
        }

        //public override FileSystemDirectory CreateDirectory(string dirId, string name, IEnumerable<string> usedNames)
        //{
        //    return new GoogleDriveDirectory() { Name = GetUniqueDirectoryName(name, usedNames) };
        //}

        //public override FileSystemFile CreateFile(string dirId, string name, string contentType, IEnumerable<string> usedNames)
        //{
        //    return new GoogleDriveFile(contentType) { Name = GetUniqueFileName(name, contentType, usedNames) };
        //}

        public override async Task<string> GetBackgroundTemplateKey(string dirId)
        {
            if (IsTrashDir(dirId))
            {
                return "TrashIcon";
            }
            else if (IsSharedWithMeDir(dirId))
            {
                return "SharedIcon";
            }
            else if (IsStarredDir(dirId))
            {
                return "StarIcon";
            }
            return await base.GetBackgroundTemplateKey(dirId);
        }

        public override string GetEmptyDirectoryMessage(string directoryId)
        {
            if (directoryId.EndsWith("trashed"))
            {
                return GoogleDriveResources.EmptyTrashMessage;
            }
            return base.GetEmptyDirectoryMessage(directoryId);
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                return new GoogleDriveDirectoryViewModel(FileExplorer, dirId, item);
            }
            else
            {
                return new GoogleDriveFileViewModel(FileExplorer, dirId, item, file);
            }
        }

        internal bool IsRootDir(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            return string.IsNullOrWhiteSpace(subPath);
        }

        internal bool IsTrashDir(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            return GoogleDriveFileSystem.IsTrashDir(subPath);
        }

        internal bool IsStarredDir(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            return GoogleDriveFileSystem.IsStarredDir(subPath);
        }

        internal bool IsSharedWithMeDir(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            return GoogleDriveFileSystem.IsSharedWithMeDir(subPath);
        }


        internal bool IsInRoot(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            //var parentId = await driveFileSystem.GetParentIdAsync(subPath, CancellationToken.None);
            return GoogleDriveFileSystem.IsInRoot(subPath, false);
        }

        internal async Task<bool> IsDirInTrash(string dirId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(dirId, out subPath);
            var parentId = await driveFileSystem.GetDirectoryParentIdAsync(subPath, CancellationToken.None);
            return GoogleDriveFileSystem.IsTrashDir(parentId);
        }

        internal async Task<bool> IsFileInTrash(string fileId)
        {
            string subPath;
            var driveFileSystem = GetActualFileSystem(fileId, out subPath);
            var parentId = await driveFileSystem.GetFileParentIdAsync(subPath, CancellationToken.None);
            return GoogleDriveFileSystem.IsTrashDir(parentId);
        }

        internal GoogleDriveFileSystem GetActualFileSystem(string dirPath, out string subPath)
        {
            if (FileSystem is GlobalFileSystem)
            {
                string connectionName;
                var dir = (FileSystem as GlobalFileSystem).GetConnection(dirPath, out connectionName, out subPath);
                return dir.FileSystem as GoogleDriveFileSystem;
            }
            subPath = null;
            return null;
        }

        #region actions

        public async override Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            if (context.IsSingleGroup)
            {
                AddOpenDirectoryAction(context, actions, dirPredicate: async dirId => !await IsDirInTrash(dirId));
                await AddOpenFileAction(context, actions, filePredicate: async fileId => !await IsFileInTrash(fileId));
                await AddOpenFileLinkAction(context, actions, GlobalResources.OpenInLabel.Format(0, new GoogleDriveProvider().Name), isDefault: true, filePredicate: async fileId => !await IsFileInTrash(fileId));

                await AddShareDirectoryAction(context, actions, dirPredicate: async dirId => !await IsDirInTrash(dirId));
                await AddShareFileAction(context, actions, filePredicate: async fileId => !await IsFileInTrash(fileId));

                await AddUploadFiles(context, actions);
                await AddDownloadFile(context, actions);
                await AddCreateDirectory(context, actions);

                await AddCopy(context, targetDirectoryId, actions, dirPredicate: dirId => Task.FromResult(IsInRoot(dirId)));
                await AddMove(context, targetDirectoryId, actions, dirPredicate: dirId => Task.FromResult(IsInRoot(dirId)));

                await AddUntrashFiles(context, actions);
                await AddUntrashDirectories(context, actions);
                await AddDeleteDirectories(context,
                    actions,
                    questionSingular: GlobalResources.DeleteFolderQuestion.Format(1, new GoogleDriveProvider().Name),
                    questionPlural: GlobalResources.DeleteFoldersQuestion.Format(1, new GoogleDriveProvider().Name),
                    //sendToTrash: true,
                    dirPredicate: async dirId => !await IsDirInTrash(dirId));
                await AddDeleteDirectories(context,
                    actions,
                    captionSingular: GoogleDriveResources.DeleteForeverLabel,
                    captionPlural: GoogleDriveResources.DeleteForeverLabel,
                    questionSingular: GlobalResources.DeleteFolderQuestion.Format(1, new GoogleDriveProvider().Name),
                    questionPlural: GlobalResources.DeleteFoldersQuestion.Format(1, new GoogleDriveProvider().Name),
                    dirPredicate: async dirId => await IsDirInTrash(dirId));
                await AddDeleteFiles(context,
                    actions,
                    questionSingular: GlobalResources.DeleteFileQuestion.Format(1, new GoogleDriveProvider().Name),
                    questionPlural: GlobalResources.DeleteFilesQuestion.Format(1, new GoogleDriveProvider().Name),
                    //sendToTrash: true,
                    filePredicate: async fileId => !await IsFileInTrash(fileId));
                await AddDeleteFiles(context,
                    actions,
                    caption: GoogleDriveResources.DeleteForeverLabel,
                    questionSingular: GlobalResources.DeleteFileQuestion.Format(1, new GoogleDriveProvider().Name),
                    questionPlural: GlobalResources.DeleteFilesQuestion.Format(1, new GoogleDriveProvider().Name),
                    filePredicate: async fileId => await IsFileInTrash(fileId));
                await AddEmptyTrash(context, actions);

                await AddDirectoryProperties(context, actions);
                await AddFileProperties(context, actions);

                await AddSortAction(context, actions);
                await AddRefreshAction(context, actions/*, dirPredicate: async dirId => !IsRootDir(dirId)*/);
                await AddSearchAction(context, actions);
                await AddOnlineModeAction(context, actions);
            }

            return actions;
        }

        private async Task AddUntrashDirectories(FileSystemActionContext context,
            List<FileSystemAction> actions)
        {
            if (context.IsSingleGroup &&
                context.IsMultiDirectory &&
                await context.Directories.AllAsync(d => IsDirInTrash(FileSystem.GetDirectoryId(d.Item1, d.Item2))))
            {
                var untrashDirectories = new FileSystemAction("UntrashDirectories", GoogleDriveResources.RestoreFromTrashLabel, context,
                    async (s, e) =>
                    {
                        try
                        {
                            await context.Directories.Select(async d =>
                            {
                                string subPath;
                                var dirId = FileSystem.GetDirectoryId(d.Item1, d.Item2);
                                var fileSystem = GetActualFileSystem(dirId, out subPath);
                                await fileSystem.UntrashDirectoryAsync(subPath, s.CancellationToken);
                                SelectionManager.RemoveItem(d.Item1, d.Item3);
                            }).WhenAll();
                        }
                        catch (AggregateException)
                        {
                            //message = exc.InnerExceptions.Count() == 1 ? GlobalResources.DeleteFileError : string.Format(GlobalResources.DeleteFilesError, exc.InnerExceptions.Count());
                            //AppService.ShowErrorAsync(message);
                            throw new OperationCanceledException();
                        }
                    })
                {
                    Category = FileSystemActionCategory.Modify,
                };
                actions.Add(untrashDirectories);
            }
        }

        private async Task AddUntrashFiles(FileSystemActionContext context,
            List<FileSystemAction> actions)
        {
            if (context.IsSingleGroup &&
                context.IsMultiFile &&
                await context.Files.AllAsync(f => IsFileInTrash(FileSystem.GetFileId(f.Item1, f.Item2))))
            {
                var delete = new FileSystemAction("UntrashFile", GoogleDriveResources.RestoreFromTrashLabel, context,
                    async (s, e) =>
                    {
                        try
                        {
                            await context.Files.Select(async f =>
                            {
                                string subPath;
                                var fileId = FileSystem.GetFileId(f.Item1, f.Item2);
                                var fileSystem = GetActualFileSystem(fileId, out subPath);
                                await fileSystem.UntrashFileAsync(subPath, s.CancellationToken);
                                SelectionManager.RemoveItem(f.Item1, f.Item3);
                            }).WhenAll();
                        }
                        catch (AggregateException)
                        {
                            //message = exc.InnerExceptions.Count() == 1 ? GlobalResources.DeleteFileError : string.Format(GlobalResources.DeleteFilesError, exc.InnerExceptions.Count());
                            //AppService.ShowErrorAsync(message);
                            throw new OperationCanceledException();
                        }
                    })
                {
                    Category = FileSystemActionCategory.Modify,
                };
                actions.Add(delete);
            }
        }

        #endregion
    }
}