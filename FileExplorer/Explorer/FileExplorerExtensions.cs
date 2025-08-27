using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FileExplorerExtensions : IFileExplorerExtensions
    {
        private bool? _useExtension;

        public FileExplorerExtensions(FileExplorerViewModel explorer, bool? useExtension)
        {
            FileExplorer = explorer;
            _useExtension = useExtension;
        }

        public FileExplorerViewModel FileExplorer { get; private set; }

        public IFileSystemAsync FileSystem
        {
            get
            {
                return FileExplorer.FileSystem;
            }
        }

        public IAppService AppService
        {
            get
            {
                return FileExplorer.AppService;
            }
        }

        public IFileSystemSelectionManager SelectionManager
        {
            get
            {
                return FileExplorer.SelectionManager;
            }
        }

        public virtual bool AllowsDuplicatedNames
        {
            get
            {
                return false;
            }
        }
        public virtual bool NameRequired
        {
            get
            {
                return true;
            }
        }
        public virtual bool UseExtension
        {
            get
            {
                return _useExtension ?? NameRequired;
            }
        }

        public virtual bool UseFileExtension(string dirId)
        {
            return UseExtension;
        }

        public virtual FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, IEnumerable<string> usedNames)
        {
            return new FileSystemDirectory() { Name = GetUniqueDirectoryName(name, usedNames) };
        }

        public virtual FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames)
        {
            return new FileSystemFile(id, GetUniqueFileName(name, contentType, usedNames), contentType, false);
        }

        public virtual FileSystemDirectory CopyDirectoryItem(string dirId, FileSystemDirectory directory, IEnumerable<string> usedNames)
        {
            var newDir = CreateDirectoryItem(dirId, directory.Id, directory.Name, usedNames);
            FileSystemItem.Copy(directory, newDir);
            newDir.Name = GetUniqueDirectoryName(directory.Name, usedNames);
            return newDir;
        }

        public virtual FileSystemFile CopyFileItem(string dirId, FileSystemFile file, IEnumerable<string> usedNames)
        {
            var newFile = CreateFileItem(dirId, file.Id, file.Name, file.ContentType, usedNames);
            FileSystemItem.Copy(file, newFile);
            newFile.Name = GetUniqueFileName(file.Name, file.ContentType, usedNames);
            return newFile;
        }

        protected virtual string GetUniqueDirectoryName(string name, IEnumerable<string> usedNames)
        {
            if (!AllowsDuplicatedNames)
            {
                return Path.GetUniqueDirectoryName(name, usedNames);
            }
            return name;
        }

        protected virtual string GetUniqueFileName(string name, string contentType, IEnumerable<string> usedNames)
        {
            var originalPattern = "{0}{1}";
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            if (string.IsNullOrWhiteSpace(nameWithoutExtension) && NameRequired)
            {
                nameWithoutExtension = FileSystemResources.UnknownFileLabel;
            }
            var extension = "";
            if (UseExtension)
            {
                extension = Path.GetExtension(name).ToLower();
                var validExtensions = MimeType.GetExtensionsFromContentType(contentType);
                if (validExtensions.Count() > 0)
                {
                    if (!validExtensions.Contains(extension))
                    {
                        extension = validExtensions.FirstOrDefault();
                    }
                }
            }
            //if (NameLength > 0)
            //{
            //    if (nameWithoutExtension.Length + extension.Length > NameLength)
            //    {
            //        nameWithoutExtension = nameWithoutExtension.Substring(0, NameLength - extension.Length);
            //    }
            //}
            name = string.Format(originalPattern, nameWithoutExtension, extension);
            if (!AllowsDuplicatedNames)
            {
                return Path.GetUniqueFileName(name, usedNames);
            }
            return name;
        }

        public virtual Task<string> GetBackgroundTemplateKey(string directoryId)
        {
            return Task.FromResult("WoopitiIcon");
        }

        public virtual string GetEmptyDirectoryMessage(string directoryId)
        {
            return FileSystemResources.EmptyFolderMessage;
        }

        public string GetNotCachedDirectoryMessage(string directoryId)
        {
            return FileSystemResources.NotCachedFolderMessage;
        }

        public virtual FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item != null)
            {
                if (item.IsDirectory)
                {
                    return new FileSystemDirectoryViewModel(FileExplorer, dirId, item);
                }
                else
                {
                    return new FileSystemFileViewModel(FileExplorer, dirId, item, file);
                }
            }
            return null;
        }

        public virtual bool FilesHaveSize(string dirId)
        {
            return true;
        }

        #region actions

        public virtual async Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            AddOpenDirectoryAction(context, actions);
            await AddOpenFileAction(context, actions);
            await AddCreateDirectory(context, actions);
            await AddUploadFiles(context, actions);
            await AddDeleteDirectories(context, actions);
            await AddDeleteFiles(context, actions);
            await AddFileProperties(context, actions);
            await AddDirectoryProperties(context, actions);
            await AddRefreshAction(context, actions);
            await AddOnlineModeAction(context, actions);

            return actions.ToArray();
        }

        protected async void AddOpenDirectoryAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            bool isDefault = true,
            Func<string, Task<bool>> dirPredicate = null)
        {
            if (context.IsSingleDirectory)
            {
                var dirId = FileExplorer.GetDirectoryId(context.SingleGroup.BaseDirectoryId, context.SingleDirectory.Id);
                if (dirPredicate == null || await dirPredicate(dirId))
                {
                    var open = new FileSystemAction("OpenDirectory", FileSystemResources.OpenLabel, context, (a, e) =>
                    {
                        return FileExplorer.OpenDirectoryAsync(dirId, context.SingleDirectory.Name, a.SourceOrigin, a.CancellationToken);
                    })
                    {
                        NeedsInternetAccess = false,
                        Category = FileSystemActionCategory.Open,
                        IsDefault = isDefault,
                    };
                    actions.Add(open);
                }
            }
        }

        protected async Task AddOpenFileAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            bool isDefault = true,
            Func<string, Task<bool>> filePredicate = null)
        {
            if (context.IsSingleFile)
            {
                var file = context.SingleFile;
                var fileId = FileSystem.GetFileId(context.SingleGroup.BaseDirectoryId, file.Id);
                if (filePredicate == null || await filePredicate(fileId))
                {
                    if (await FileExplorer.CanOpenFile(fileId, file.ContentType))
                    {
                        actions.Add(new FileSystemAction("OpenFile", FileSystemResources.OpenLabel, context,
                            (s, e) =>
                            {
                                return FileExplorer.OpenFile(context.SingleGroup.BaseDirectoryId, fileId, file, s.CancellationToken, s.SourceOrigin);
                            })
                        {
                            NeedsInternetAccess = false,
                            IsDefault = isDefault,
                            Category = FileSystemActionCategory.Open,
                        });
                    }
                }
            }
        }

        protected async Task AddOpenDirectoryLinkAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            bool isDefault = true)
        {
            if (FileExplorer.IsOnline && context.IsSingleDirectory)
            {
                var directory = context.SingleDirectory;
                var dirId = FileExplorer.GetDirectoryId(context.SingleGroup.BaseDirectoryId, directory.Id);
                if (directory.Link != null || await FileSystem.CanGetDirectoryLinkAsync(dirId, CancellationToken.None))
                {
                    actions.Add(new FileSystemAction("OpenDirectoryLink", caption ?? FileSystemResources.OpenLabel, context,
                        async (s, e) =>
                        {
                            await AppService.NavigateUrl(directory.Link != null ? directory.Link : await FileSystem.GetDirectoryLinkAsync(dirId, s.CancellationToken));
                        })
                    {
                        IsDefault = isDefault,
                        Category = FileSystemActionCategory.Open,
                    });
                }
            }
        }

        protected async Task AddOpenFileLinkAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            bool isDefault = true,
            Func<string, Task<bool>> filePredicate = null)
        {
            if (FileExplorer.IsOnline && context.IsSingleFile)
            {
                var file = context.SingleFile;
                var fileId = FileExplorer.GetFileId(context.SingleGroup.BaseDirectoryId, file.Id);
                if (filePredicate == null || await filePredicate(fileId))
                {
                    if (file.Link != null || await FileSystem.CanGetFileLinkAsync(fileId, CancellationToken.None))
                    {
                        actions.Add(new FileSystemAction("OpenFileLink", caption ?? FileSystemResources.OpenLabel, context,
                            async (s, e) =>
                            {
                                await AppService.NavigateUrl(file.Link != null ? file.Link : await FileSystem.GetFileLinkAsync(fileId, s.CancellationToken));
                            })
                        {
                            IsDefault = isDefault,
                            Category = FileSystemActionCategory.Open,
                        });
                    }
                }
            }
        }

        protected async Task AddShareDirectoryAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            bool isDefault = false,
            Func<string, Task<bool>> dirPredicate = null)
        {
            if (context.IsSingleDirectory)
            {
                var directory = context.SingleDirectory;
                var dirId = FileExplorer.GetDirectoryId(context.SingleGroup.BaseDirectoryId, directory.Id);
                if (dirPredicate == null || await dirPredicate(dirId))
                {
                    if (AppService.CanShareLink() && (directory.Link != null || await FileSystem.CanGetDirectoryLinkAsync(dirId, CancellationToken.None)))
                        actions.Add(new FileSystemAction("Share", caption ?? FileSystemResources.ShareLabel, context,
                            async (s, e) =>
                            {
                                var packages = new List<SharedSource>();
                                if (AppService.CanShareLink())
                                {
                                    packages.Add(new SharedLink(directory.Name, directory.Link != null ? directory.Link : await FileSystem.GetDirectoryLinkAsync(dirId, s.CancellationToken)));
                                }
                                await AppService.ShareLinkAsync(packages, s.SourceOrigin);
                            })
                        {
                            IsDefault = isDefault,
                            Category = FileSystemActionCategory.Share,
                        });
                }
            }
        }

        protected async Task AddShareFileAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            bool isDefault = false,
            Func<string, Task<bool>> filePredicate = null)
        {
            if (context.IsSingleFile)
            {
                var file = context.SingleFile;
                var fileId = FileExplorer.GetFileId(context.SingleGroup.BaseDirectoryId, file.Id);
                if (filePredicate == null || await filePredicate(fileId))
                {
                    if (AppService.CanShareLink() && (file.Link != null || await FileSystem.CanGetFileLinkAsync(fileId, CancellationToken.None)) ||
                        (AppService.CanShareFile() && (await FileSystem.CanOpenFileAsync(fileId, CancellationToken.None))))
                        actions.Add(new FileSystemAction("Share", caption ?? FileSystemResources.ShareLabel, context,
                                                         (s, e) => FileExplorer.ShareFileAsync(context.SingleGroup.BaseDirectoryId, file, fileId, s.SourceOrigin, s.CancellationToken)
                            )
                        {
                            IsDefault = isDefault,
                            Category = FileSystemActionCategory.Share,
                            NeedsInternetAccess = false,
                        });
                }
            }
        }



        protected async Task AddUploadFiles(
            FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            bool multiSelect = true,
            bool showUploadForm = false)
        {
            if (await FileExplorer.CanUploadAsync(context))
            {
                actions.Add(new FileSystemAction("Upload", caption ?? FileSystemResources.UploadFilesLabel, context,
                    (s, e) => FileExplorer.UploadAsync(context.SingleGroup.BaseDirectoryId, caption, multiSelect, showUploadForm, s.CancellationToken, s.SourceOrigin))
                {
                    NeedsNotificationsEnabled = true,
                    Category = FileSystemActionCategory.Modify,
                });
            }
        }

        protected async Task AddDownloadFile(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null)
        {
            if (await FileExplorer.CanDownloadAsync(context))
            {
                actions.Add(new FileSystemAction("Download", caption ?? FileSystemResources.DownloadLabel, context,
                    (s, e) => FileExplorer.DownloadAsync(context, s.CancellationToken, s.SourceOrigin))
                {
                    NeedsNotificationsEnabled = true,
                    NeedsInternetAccess = false,
                    Category = FileSystemActionCategory.Modify,
                });
            }
        }

        protected async Task AddCreateDirectory(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            string name = null,
            string createFolderErrorMessage = null,
            string duplicatedFolderErrorMessage = null)
        {
            if (FileExplorer.IsOnline && await FileExplorer.CanCreateDirectoryAsync(context))
            {
                actions.Add(new FileSystemAction("CreateDirectory", caption ?? FileSystemResources.CreateFolderLabel, context,
                    (s, e) => FileExplorer.CreateDirectoryAsync(caption, context, s.SourceOrigin, s.CancellationToken))
                {
                    Category = FileSystemActionCategory.Modify,
                });
            }
        }

        protected async Task AddDeleteDirectories(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string captionSingular = null,
            string captionPlural = null,
            string questionSingular = null,
            string questionPlural = null,
            bool needsInternetAccess = true,
            bool executeInParallel = true,
            Func<string, Task> afterDirectoryDeleted = null,
            Func<string, Task<bool>> dirPredicate = null)
        {
            if ((FileExplorer.IsOnline || !needsInternetAccess) &&
                context.IsSingleGroup &&
                context.IsMultiDirectory &&
                (dirPredicate == null || await context.Directories.AllAsync(d => dirPredicate(FileExplorer.GetDirectoryId(d.Item1, d.Item2)))) &&
                await context.Directories.AllAsync(d => FileSystem.CanDeleteDirectory(FileExplorer.GetDirectoryId(d.Item1, d.Item2), CancellationToken.None)))
            {
                var delete = new FileSystemAction("DeleteDirectory", context.IsSingleDirectory ? captionSingular ?? FileSystemResources.DeleteLabel : captionPlural ?? FileSystemResources.DeleteLabel, context,
                    (s, e) => FileExplorer.DeleteDirectoryAsync(context, questionSingular, questionPlural, afterDirectoryDeleted, s.CancellationToken))
                {
                    NeedsInternetAccess = needsInternetAccess,
                    Category = FileSystemActionCategory.Modify,
                    IsDestructive = true,
                };
                actions.Add(delete);
            }
        }

        protected async Task AddDeleteFiles(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            string questionSingular = null,
            string questionPlural = null,
            Func<string, Task<bool>> filePredicate = null)
        {
            if (FileExplorer.IsOnline &&
                context.IsSingleGroup &&
                context.IsMultiFile &&
                (filePredicate == null || await context.Files.AllAsync(f => filePredicate(FileExplorer.GetFileId(f.Item1, f.Item2)))) &&
                await context.Files.AllAsync(f => FileSystem.CanDeleteFile(FileSystem.GetFileId(f.Item1, f.Item2), CancellationToken.None)))
            {
                var delete = new FileSystemAction("DeleteFile", caption ?? FileSystemResources.DeleteLabel, context,
                    (s, e) => FileExplorer.DeleteFilesAsync(context, questionSingular, questionPlural, s.CancellationToken))
                {
                    Category = FileSystemActionCategory.Modify,
                    IsDestructive = true,
                };
                actions.Add(delete);
            }
        }

        protected async Task AddEmptyTrash(FileSystemActionContext context, List<FileSystemAction> actions)
        {
            if (context.IsEmptyGroup &&
                context.SingleGroup.BaseDirectoryId == await FileSystem.GetTrashId(context.SingleGroup.BaseDirectoryId, CancellationToken.None))
            {
                var emptyTrash = new FileSystemAction("EmptyTrash", FileSystemResources.EmptyTrashLabel, context,
                    (s, e) => FileExplorer.EmptyTrashAsync(context, s.CancellationToken))
                {
                    Category = FileSystemActionCategory.Modify,
                };
                actions.Add(emptyTrash);
            }
        }

        protected async Task AddCopy(FileSystemActionContext context,
            string targetDirectoryId,
            List<FileSystemAction> actions,
            Func<string, Task<bool>> dirPredicate = null)
        {
            if (FileExplorer.IsOnline)
            {
                if (context.Items.Count() > 0 &&
                    (dirPredicate == null || (await context.Directories.AllAsync(d => dirPredicate(FileExplorer.GetDirectoryId(d.Item1, d.Item2))) && await context.Files.AllAsync(async f => { return await dirPredicate(await FileSystem.GetFileParentIdAsync(FileSystem.GetFileId(f.Item1, f.Item2), CancellationToken.None)); }))) &&
                    await FileExplorer.CanCopyAsync(context))
                {
                    actions.Add(new FileSystemAction("Copy", FileSystemResources.CopyLabel, context,
                        async (s, e) =>
                        {
                            var pickedDirId = await AppService.PickFolderToCopyAsync(targetDirectoryId, s.Context, s.SourceOrigin);
                            var task = FileExplorer.CopyAsync(context, pickedDirId, s.CancellationToken, false, s.SourceOrigin);
                        })
                    {
                        NeedsNotificationsEnabled = true,
                        Category = FileSystemActionCategory.Copy,
                    });
                }
            }
        }

        protected async Task AddMove(FileSystemActionContext context,
            string targetDirectoryId,
            List<FileSystemAction> actions,
            Func<string, Task<bool>> dirPredicate = null)
        {
            if (FileExplorer.IsOnline)
            {
                if (context.Items.Count() > 0 &&
                    (dirPredicate == null || (await context.Directories.AllAsync(d => dirPredicate(FileExplorer.GetDirectoryId(d.Item1, d.Item2))) && await context.Files.AllAsync(async f => { return await dirPredicate(await FileSystem.GetFileParentIdAsync(FileExplorer.GetFileId(f.Item1, f.Item2), CancellationToken.None)); }))) &&
                    await FileExplorer.CanMoveAsync(context))
                {
                    actions.Add(new FileSystemAction("Move", FileSystemResources.MoveLabel, context,
                        async (s, e) =>
                        {
                            var pickedDirId = await AppService.PickFolderToMoveAsync(targetDirectoryId, s.Context, s.SourceOrigin);
                            var task = FileExplorer.CopyAsync(context, pickedDirId, s.CancellationToken, true, s.SourceOrigin);
                        })
                    {
                        NeedsNotificationsEnabled = true,
                        Category = FileSystemActionCategory.Copy,
                    });
                }
            }
        }

        protected Task AddDirectoryProperties(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            bool needsInternetAccess = true)
        {
            if ((FileExplorer.IsOnline || !needsInternetAccess) &&
                context.IsSingleDirectory)
            {
                actions.Add(new FileSystemAction("DirectoryProperties", caption ?? FileSystemResources.PropertiesLabel, context,
                    (s, e) => FileExplorer.DirectoryPropertiesAsync(context, caption, s.CancellationToken, s.SourceOrigin))
                {
                    NeedsInternetAccess = needsInternetAccess,
                    Category = FileSystemActionCategory.Properties,
                });
            }
            return Task.FromResult(true);
        }

        protected async Task AddFileProperties(FileSystemActionContext context,
            List<FileSystemAction> actions,
            string caption = null,
            string name = null,
            bool onlyInRoot = false,
            Func<string, Task<bool>> filePredicate = null)
        {
            if (FileExplorer.IsOnline && context.IsSingleFile)
            {
                var file = context.SingleFile;
                var dirId = context.SingleGroup.BaseDirectoryId;
                var fileId = FileExplorer.GetFileId(dirId, file.Id);
                if (filePredicate == null || await filePredicate(fileId))
                {
                    actions.Add(new FileSystemAction("FileProperties",
                        caption ?? FileSystemResources.PropertiesLabel,
                        context,
                        (s, e) => FileExplorer.FilePropertiesAsync(context, caption ?? FileSystemResources.PropertiesLabel, s.CancellationToken, s.SourceOrigin))
                    {
                        Category = FileSystemActionCategory.Properties,
                    });
                }
            }
        }

        protected async Task AddRefreshAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            bool needsInternetAccess = true,
            Func<string, Task<bool>> dirPredicate = null)
        {
            if ((FileExplorer.IsOnline || !needsInternetAccess) &&
                context.IsEmptyGroup)
            {
                if (dirPredicate == null || await dirPredicate(context.SingleGroup.BaseDirectoryId))
                {
                    var refresh = new FileSystemAction("Refresh", FileSystemResources.RefreshLabel, context,
                        (s, e) => FileSystem.RefreshAsync(context.SingleGroup.BaseDirectoryId))
                    {
                        NeedsInternetAccess = needsInternetAccess,
                        Category = FileSystemActionCategory.Refresh,
                    };
                    actions.Add(refresh);
                }
            }
        }

        protected async Task AddSearchAction(FileSystemActionContext context,
            List<FileSystemAction> actions,
            bool needsInternetAccess = true)
        {
            if ((FileExplorer.IsOnline || !needsInternetAccess) &&
                context.IsEmptyGroup)
            {
                if (await FileSystem.CanSearchAsync(context.SingleGroup.BaseDirectoryId, CancellationToken.None))
                {
                    var refresh = new FileSystemAction("Search", FileSystemResources.SearchLabel, context,
                        (s, e) => AppService.GoToSearch())
                    {
                        NeedsInternetAccess = needsInternetAccess,
                        Category = FileSystemActionCategory.Refresh,
                    };
                    actions.Add(refresh);
                }
            }
        }

        protected Task AddOnlineModeAction(FileSystemActionContext context,
            List<FileSystemAction> actions)
        {
            if (!FileExplorer.IsOnline &&
                context.IsEmptyGroup)
            {
                var refresh = new FileSystemAction("OnlineMode", FileSystemResources.OnlineModeLabel, context,
                    (s, e) => FileExplorer.Settings.IsOnline = true)
                {
                    Category = FileSystemActionCategory.Refresh,
                };
                actions.Add(refresh);
            }
            return Task.FromResult(true);
        }

        protected Task AddSortAction(FileSystemActionContext context, List<FileSystemAction> actions)
        {
            if (context.IsEmptyGroup)
            {
                var subActions = new List<FileSystemAction>();
                if (FileExplorer.CanSortByNameAsc())
                    subActions.Add(new FileSystemAction("SortByNameAsc", ApplicationResources.SortByNameAscLabel, context, (s, e) => FileExplorer.SortByName()));
                if (FileExplorer.CanSortByNameDesc())
                    subActions.Add(new FileSystemAction("SortByNameDesc", ApplicationResources.SortByNameDescLabel, context, (s, e) => FileExplorer.SortByNameDesc()));
                if (FileExplorer.CanSortBySizeDesc())
                    subActions.Add(new FileSystemAction("SortBySizeDesc", ApplicationResources.SortBySizeDescLabel, context, (s, e) => FileExplorer.SortBySizeDesc()));
                if (FileExplorer.CanSortBySizeAsc())
                    subActions.Add(new FileSystemAction("SortBySizeAsc", ApplicationResources.SortBySizeAscLabel, context, (s, e) => FileExplorer.SortBySize()));
                if (FileExplorer.CanSortByDateDesc())
                    subActions.Add(new FileSystemAction("SortByDateDesc", ApplicationResources.SortByDateDescLabel, context, (s, e) => FileExplorer.SortByDateDesc()));
                if (FileExplorer.CanSortByDateAsc())
                    subActions.Add(new FileSystemAction("SortByDateAsc", ApplicationResources.SortByDateAscLabel, context, (s, e) => FileExplorer.SortByDate()));
                if (subActions.Count > 0)
                    actions.Add(new FileSystemActionList("Sort", ApplicationResources.SortLabel) { Actions = subActions, Category = FileSystemActionCategory.Sort });
            }
            return Task.FromResult(true); ;
        }

        #endregion


        //public virtual async Task<List<string>> GetCachedNames(IFileSystemAsync fileSystem, string dir, Dictionary<string, List<string>> namesDict, CancellationToken cancellationToken)
        //{
        //    var usedNames = new List<string>();
        //    if (!AllowsDuplicatedNames)
        //    {
        //        if (namesDict.ContainsKey(dir))
        //        {
        //            usedNames = namesDict[dir];
        //        }
        //        else
        //        {
        //            usedNames.AddRange(await GetUsedNames(fileSystem, dir, cancellationToken));
        //            namesDict.Add(dir, usedNames);
        //        }
        //    }
        //    return usedNames;
        //}

        //public Task<List<string>> GetCachedNames(string dir, Dictionary<string, List<string>> namesDict, CancellationToken cancellationToken)
        //{
        //    return GetCachedNames(FileSystem, dir, namesDict, cancellationToken);
        //}

    }
}
