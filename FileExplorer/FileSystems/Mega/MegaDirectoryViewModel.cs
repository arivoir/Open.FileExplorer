using Open.FileSystemAsync;
using Open.Mega;

namespace Open.FileExplorer
{
    public class MegaDirectoryViewModel : FileSystemDirectoryViewModel
    {
        #region initialization

        public MegaDirectoryViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        public override string Icon
        {
            get
            {
                var megaDirectory = Item as MegaDirectory;
                if (megaDirectory != null)
                {
                    if (megaDirectory.Node.Type == NodeType.Root)
                    {
                        return "MegaIcon";
                    }
                    if (megaDirectory.Node.Type == NodeType.Inbox)
                    {
                        return "InboxIcon";
                    }
                    if (megaDirectory.Node.Type == NodeType.Trash)
                    {
                        return "TrashIcon";
                    }
                }
                return base.Icon;
            }
        }
    }
}
