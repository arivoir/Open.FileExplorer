using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    internal class GlobalFileExplorerExtensions : FileExplorerExtensions
    {

        public GlobalFileExplorerExtensions(FileExplorerViewModel explorer)
            : base(explorer, null)
        {
        }

        public new GlobalFileSystem FileSystem
        {
            get
            {
                return base.FileSystem as GlobalFileSystem;
            }
        }

        public static IEnumerable<IProvider> Providers
        {
            get
            {
                //yield return new FacebookProvider();
                yield return new GoogleDriveProvider();
                yield return new DropBoxProvider();
                yield return new OneDriveProvider();
                yield return new GooglePhotosProvider();
                yield return new YouTubeProvider();
                yield return new FlickrProvider();
                yield return new TwitterProvider();
                yield return new InstagramProvider();
                yield return new BoxProvider();
                //yield return new CopyProvider();
                //yield return new BitcasaProvider();
                yield return new MegaProvider();
                //yield return new SugarSyncProvider();
                //yield return new CloudMeProvider();
                //yield return new HiDriveProvider();
                //yield return new FourSharedProvider();
                //yield return new OwnCloudProvider();
                //yield return new WebDavProvider();
                //yield return new SharepointProvider();
                //yield return new BaiduProvider();
                //yield return new KanboxProvider();
            }
        }

        public override async Task<string> GetBackgroundTemplateKey(string dirId)
        {
            dirId = Path.NormalizePath(dirId);
            string subPath, providerName;
            if (!string.IsNullOrWhiteSpace(dirId))
            {
                var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
                if (connection != null)
                {
                    var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                    return await extensions.GetBackgroundTemplateKey(dirId);
                }
            }
            return await base.GetBackgroundTemplateKey(dirId);
        }

        public override string GetEmptyDirectoryMessage(string dirId)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.GetEmptyDirectoryMessage(dirId);
            }
            else
            {
                return GlobalResources.NoLinkedAccountsMessage;
            }
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item is AccountDirectory)
            {
                return new AccountDirectoryViewModel(FileExplorer, item, dirId);
            }
            else
            {
                string subPath, providerName;
                var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
                if (connection != null)
                {
                    var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                    return extensions.CreateViewModel(dirId, item, file);
                }
                return base.CreateViewModel(dirId, item, file);
            }
        }

        public override FileSystemDirectory CopyDirectoryItem(string dirId, FileSystemDirectory directory, System.Collections.Generic.IEnumerable<string> usedNames)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.CopyDirectoryItem(subPath, directory, usedNames);
            }
            else
            {
                var original = directory as AccountDirectory;
                var copy = base.CopyDirectoryItem(dirId, directory, usedNames) as AccountDirectory;
                copy.UsedSize = original.UsedSize;
                copy.TotalSize = original.TotalSize;
                return copy;
            }
        }

        public override FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, System.Collections.Generic.IEnumerable<string> usedNames)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.CreateDirectoryItem(subPath, id, name, usedNames);
            }
            else
            {
                return new AccountDirectory();
            }
        }

        public override FileSystemFile CopyFileItem(string dirId, FileSystemFile file, System.Collections.Generic.IEnumerable<string> usedNames)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.CopyFileItem(subPath, file, usedNames);
            }
            else
            {
                return base.CopyFileItem(dirId, file, usedNames);
            }
        }

        public override FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, System.Collections.Generic.IEnumerable<string> usedNames)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.CreateFileItem(subPath, id, name, contentType, usedNames);
            }
            else
            {
                return base.CreateFileItem(dirId, id, name, contentType, usedNames);
            }
        }

        public IFileExplorerExtensions GetExplorerExtensions(string connectionId)
        {
            string subPath, providerName;
            var connection = (FileSystem as GlobalFileSystem).GetConnection(connectionId, out providerName, out subPath);
            if (connection != null && connection.FileSystem != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions;
            }
            return null;
        }

        public override bool FilesHaveSize(string dirId)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.FilesHaveSize(subPath);
            }
            return false;
        }

        public override bool UseFileExtension(string dirId)
        {
            string subPath, providerName;
            var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
            if (connection != null)
            {
                var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
                return extensions.UseFileExtension(subPath);
            }
            return true;
        }

        //public override Task<List<string>> GetCachedNames(IFileSystemAsync fileSystem, string dirId, Dictionary<string, List<string>> namesDict, CancellationToken cancellationToken)
        //{
        //    string subPath, providerName;
        //    var connection = FileSystem.GetConnection(dirId, out providerName, out subPath);
        //    if (connection != null)
        //    {
        //        var extensions = connection.Provider.GetExplorerExtensions(FileExplorer);
        //        return extensions.GetCachedNames(connection.FileSystem, subPath, namesDict, cancellationToken);
        //    }
        //    return base.GetCachedNames(fileSystem, dirId, namesDict, cancellationToken);
        //}

        #region actions

        public override async Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = new List<FileSystemAction>();

            if (context.IsSingleGroup &&
                string.IsNullOrWhiteSpace(context.SingleGroup.BaseDirectoryId))
            {
                #region open connection

                AddOpenDirectoryAction(context, actions);

                #endregion

                #region add account

                if (context.IsEmptyGroup && AppService.Settings.IsOnline)
                {
                    actions.Add(new FileSystemAction("AddAccount", GlobalResources.AddAccountLabel, context,
                        async (s, e) =>
                        {
                            await AppService.GoToConnections(s.SourceOrigin);
                        })
                    {
                        NeedsInternetAccess = false,
                        Category = FileSystemActionCategory.Modify,
                    });
                }

                #endregion

                #region pin/unpin to start

                if (context.IsSingleDirectory && AppService.AreTilesSupported)
                {
                    var directory = context.SingleDirectory as AccountDirectory;
                    if (directory.Provider != null)
                    {
                        var dirId = FileSystem.GetDirectoryId(context.SingleGroup.BaseDirectoryId, directory.Id);
                        var tiles = await AppService.GetTiles();
                        var tile = tiles.FirstOrDefault(t => t.DirId == dirId);
                        var pinned = tile != null;
                        var authenticate = new FileSystemAction("PinToStart", pinned ? GlobalResources.UnpinFromStart : GlobalResources.PinToStart, context,
                            async (a, e) =>
                            {
                                if (pinned)
                                {
                                    await AppService.RemoveTile(tile);
                                }
                                else
                                {
                                    tile = new TileInfo
                                    {
                                        Title = directory.Name,
                                        DirId = dirId,
                                    };
                                    await AppService.AddTile(tile, a.SourceOrigin);
                                }
                            })
                        {
                            Category = FileSystemActionCategory.Share,
                            NeedsInternetAccess = false,
                        };
                        actions.Add(authenticate);
                    }
                }

                #endregion

                #region remove account

                await AddDeleteDirectories(context,
                    actions,
                    captionSingular: GlobalResources.RemoveAccountLabel,
                    captionPlural: GlobalResources.RemoveAccountsLabel,
                    questionSingular: GlobalResources.RemoveAccountQuestion,
                    questionPlural: GlobalResources.RemoveAccountsQuestion,
                    needsInternetAccess: false,
                    executeInParallel: false,
                    afterDirectoryDeleted: async (dirId) =>
                    {
                        if (AppService.AreTilesSupported)
                        {
                            var tiles = await AppService.GetTiles();
                            var tile = tiles.FirstOrDefault(t => t.DirId == dirId);
                            if (tile != null)
                                await AppService.RemoveTile(tile);
                        }
                    });

                #endregion

                await AddDirectoryProperties(context,
                    actions);

                if (AppService.Settings.IsOnline)
                    await AddRefreshAction(context, actions, needsInternetAccess: false);
                await AddSearchAction(context, actions);
                await AddOnlineModeAction(context, actions);
            }
            else
            {
                #region get connection actions
                var connections = await FileSystem.GetDirectoriesAsync("", CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(targetDirectoryId) && connections.Count > 1)
                {
                    await AddCopy(context, targetDirectoryId, actions);
                    await AddMove(context, targetDirectoryId, actions);
                }
                var contextConnections = context.Groups.Select(g => Path.SplitPath(g.BaseDirectoryId).First()).Distinct();
                if (contextConnections.Count() == 1 && contextConnections.First() == Path.SplitPath(targetDirectoryId).First())
                {
                    var explorerExtensions = GetExplorerExtensions(context.Groups.First().BaseDirectoryId);
                    foreach (var action in await explorerExtensions.GetActions(context, targetDirectoryId))
                    {
                        if (actions.Where(a => a.DisplayName == action.DisplayName).Count() == 0)
                            actions.Add(action);
                    }
                }

                #endregion
            }

            return actions;
        }

        //protected async Task AddCopy(FileSystemActionContext context,
        //    string targetDirectoryId,
        //    List<FileSystemAction> actions,
        //    Func<string, Task<bool>> dirPredicate = null)
        //{
        //    if (AppService.Settings.IsOnline)
        //    {
        //        if (context.Items.Count() > 0 &&
        //            (dirPredicate == null || await context.Groups.AllAsync(g => dirPredicate(g.BaseDirectoryId))) &&
        //            await context.Files.AllAsync(f => FileSystem.CanOpenFileAsync(FileSystem.GetFileId(f.Item1, f.Item2), CancellationToken.None)))
        //        {
        //            actions.Add(new FileSystemAction("Copy", FileSystemResources.CopyLabel, context,
        //                async (s, e) =>
        //                {
        //                    var pickedDirId = await AppService.PickFolderToCopyAsync(targetDirectoryId, s.Context, s.SourceOrigin);
        //                    FileExplorer.PerformCopy(context, pickedDirId, s.CancellationToken, false, s.SourceOrigin);
        //                })
        //            {
        //                Category = FileSystemActionCategory.Copy,
        //            });
        //        }
        //    }
        //}

        //protected async Task AddMove(FileSystemActionContext context,
        //    string targetDirectoryId,
        //    List<FileSystemAction> actions,
        //    Func<string, Task<bool>> dirPredicate = null)
        //{
        //    if (AppService.Settings.IsOnline)
        //    {
        //        if (context.Items.Count() > 0 &&
        //            (dirPredicate == null || await context.Groups.AllAsync(g => dirPredicate(g.BaseDirectoryId))) &&
        //            await FileExplorer.CanMoveAsync(context))
        //        {
        //            actions.Add(new FileSystemAction("Move", FileSystemResources.MoveLabel, context,
        //                async (s, e) =>
        //                {
        //                    var pickedDirId = await AppService.PickFolderToMoveAsync(targetDirectoryId, s.Context, s.SourceOrigin);
        //                    FileExplorer.PerformCopy(context, pickedDirId, s.CancellationToken, true, s.SourceOrigin);
        //                })
        //            {
        //                Category = FileSystemActionCategory.Copy,
        //            });
        //        }
        //    }
        //}

        protected Task AddDirectoryProperties(FileSystemActionContext context, List<FileSystemAction> actions)
        {
            if (context.IsSingleDirectory)
            {
                actions.Add(new FileSystemAction("DirectoryProperties", FileSystemResources.PropertiesLabel, context,
                    async (s, e) =>
                    {
                        var directory = context.SingleDirectory;
                        var dirId = FileSystem.GetDirectoryId(context.SingleGroup.BaseDirectoryId, directory.Id);
                        await FileExplorer.DirectoryPropertiesAsync(context, FileSystemResources.PropertiesLabel, s.CancellationToken, s.SourceOrigin);
                        if (AppService.AreTilesSupported)
                        {
                            var tiles = await AppService.GetTiles();
                            var tile = tiles.FirstOrDefault(t => t.DirId == dirId);
                            if (tile != null)
                            {
                                tile.Title = context.SingleDirectory.Name;
                                await AppService.UpdateTile(tile);
                            }
                        }
                    })
                {
                    NeedsInternetAccess = false,
                    Category = FileSystemActionCategory.Properties,
                });
            }
            return Task.FromResult(true);
        }

        #endregion

    }
}