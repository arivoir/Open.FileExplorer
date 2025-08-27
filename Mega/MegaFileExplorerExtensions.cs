using Open.FileSystemAsync;
using Open.Mega;

namespace Open.FileExplorer.Mega
{
    internal class MegaFileExplorerExtensions : ProviderFileExplorerExtensions
    {
        public MegaFileExplorerExtensions(FileExplorerViewModel explorer, MegaProvider provider)
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

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                return new MegaDirectoryViewModel(FileExplorer, dirId, item);
            }
            return base.CreateViewModel(dirId, item, file);
        }

        public override async Task<string> GetBackgroundTemplateKey(string dirId)
        {
            var trashId = await FileSystem.GetTrashId(dirId, CancellationToken.None);
            if (trashId != null)
            {
                if (await FileSystem.IsSubDirectory(dirId, trashId, CancellationToken.None))
                {
                    return "TrashIcon";
                }
                else
                {
                    var fullPath = await FileSystem.GetFullPathAsync(dirId, CancellationToken.None);
                    foreach (var dirPath in FileSystemAsync.Path.DecomposePath(fullPath))
                    {
                        var parentDirId = FileSystem.GetDirectoryId(FileSystemAsync.Path.GetParentPath(dirPath), System.IO.Path.GetFileName(dirPath));
                        var dir = await FileSystem.GetDirectoryAsync(parentDirId, false, CancellationToken.None) as MegaDirectory;
                        if (dir != null && dir.Node.Type == NodeType.Inbox)
                        {
                            return "InboxIcon";
                        }
                    }
                }
            }
            return await base.GetBackgroundTemplateKey(dirId);
        }
    }
}