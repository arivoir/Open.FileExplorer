using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class CloudMeExplorerExtensions : ProviderFileExplorerExtensions
    {
        public CloudMeExplorerExtensions(FileExplorerViewModel explorer, IProvider provider)
            : base(explorer, provider)
        {
        }

        public override FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item.IsDirectory)
            {
                return new CloudMeDirectoryViewModel(FileExplorer, dirId, item);
            }
            return base.CreateViewModel(dirId, item, file);
        }
    }
}
