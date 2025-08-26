using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class LocalFileSystemExtensions : FileExplorerExtensions
    {
        public LocalFileSystemExtensions(FileExplorerViewModel explorer) 
            : base(explorer, true)
        {
        }

        public override Task<string> GetBackgroundTemplateKey(string directoryId)
        {
            return base.GetBackgroundTemplateKey(directoryId);
        }
    }
}
