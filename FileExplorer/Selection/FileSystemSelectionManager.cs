using System;
using System.Linq;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class FileSystemSelectionManager : IFileSystemSelectionManager
    {
        public FileSystemSelectionManager()
        {
            Selection = new FileSystemActionContext();
        }

        public FileSystemActionContext Selection { get; set; }

        public event EventHandler SelectionChanged;

        public void AddItem(string dirId, FileSystemItem item)
        {
            var group = Selection.Groups.FirstOrDefault(g => g.BaseDirectoryId == dirId);
            if (group == null)
            {
                group = new FileSystemActionContextGroup() { BaseDirectoryId = dirId };
                Selection.Groups.Add(group);
            }
            if (item.IsDirectory)
            {
                group.Directories.Add(item);
            }
            else
            {
                group.Files.Add(item);
            }
            RaiseSelectionChanged();
        }

        public void RemoveItem(string dirId, FileSystemItem item)
        {
            var group = Selection.Groups.FirstOrDefault(g => g.BaseDirectoryId == dirId);
            if (group != null)
            {
                if (item.IsDirectory)
                {
                    group.Directories.Remove(item);
                }
                else
                {
                    group.Files.Remove(item);
                }
                if (group.Items.Count == 0)
                {
                    Selection.Groups.Remove(group);
                }
            }
            RaiseSelectionChanged();
        }

        public bool Contains(string baseDirectory, FileSystemItem item)
        {
            var group = Selection.Groups.FirstOrDefault(g => g.BaseDirectoryId == baseDirectory);
            if (group != null)
            {
                return group.Items.Contains(item);
            }
            return false;
        }

        public void Clear()
        {
            Selection = new FileSystemActionContext();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            if (SelectionChanged != null)
                SelectionChanged(this, new EventArgs());
        }
    }
}
