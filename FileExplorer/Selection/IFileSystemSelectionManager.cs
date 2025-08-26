using System;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IFileSystemSelectionManager
    {
        FileSystemActionContext Selection { get; }
        event EventHandler SelectionChanged;
        void RemoveItem(string dirId, FileSystemItem basketItem);
        void AddItem(string currentDirectoryId, FileSystemItem fileSystemItem);
        bool Contains(string baseDirectory, FileSystemItem itemViewModel);
        void Clear();

    }
}
