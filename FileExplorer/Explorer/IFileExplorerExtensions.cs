using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IFileExplorerExtensions
    {
        FileExplorerViewModel FileExplorer { get; }
        Task<IEnumerable<FileSystemAction>> GetActions(FileSystemActionContext context, string targetDirId);
        //Task<List<string>> GetCachedNames(IFileSystemAsync fileSystem, string dirId, Dictionary<string, List<string>> namesDict, CancellationToken cancellationToken);
        Task<string> GetBackgroundTemplateKey(string directoryId);
        string GetEmptyDirectoryMessage(string directoryId);
        string GetNotCachedDirectoryMessage(string directoryId);
        bool FilesHaveSize(string dirId);
        bool UseFileExtension(string dirId);
        FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null);
        FileSystemDirectory CreateDirectoryItem(string dirId, string id, string name, IEnumerable<string> usedNames);
        FileSystemDirectory CopyDirectoryItem(string dirId, FileSystemDirectory directory, IEnumerable<string> usedNames);
        FileSystemFile CreateFileItem(string dirId, string id, string name, string contentType, IEnumerable<string> usedNames);
        FileSystemFile CopyFileItem(string dirId, FileSystemFile file, IEnumerable<string> usedNames);
    }
}
