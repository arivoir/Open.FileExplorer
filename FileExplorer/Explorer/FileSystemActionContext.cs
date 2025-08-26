using System;
using System.Collections.Generic;
using System.Linq;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class FileSystemActionContext
    {
        public FileSystemActionContext()
        {
            Groups = new List<FileSystemActionContextGroup>();
        }

        public FileSystemActionContext(string baseDirectory)
        {
            Groups = new List<FileSystemActionContextGroup>()
            {
                new FileSystemActionContextGroup
                {
                    BaseDirectoryId = baseDirectory
                }
            };
        }

        public FileSystemActionContext(string baseDirectory, FileSystemFile[] files)
        {
            Groups = new List<FileSystemActionContextGroup>()
            {
                new FileSystemActionContextGroup
                {
                    BaseDirectoryId = baseDirectory,
                    Files = files
                }
            };
        }

        public FileSystemActionContext(string baseDirectory, FileSystemDirectory[] directories)
        {
            Groups = new List<FileSystemActionContextGroup>()
            {
                new FileSystemActionContextGroup
                {
                    BaseDirectoryId = baseDirectory,
                    Directories = directories
                }
            };
        }

        public FileSystemActionContext(string baseDirectory, FileSystemDirectory[] directories, FileSystemFile[] files)
        {
            Groups = new List<FileSystemActionContextGroup>()
            {
                new FileSystemActionContextGroup
                {
                    BaseDirectoryId = baseDirectory,
                    Directories = directories,
                    Files = files,
                }
            };
        }

        public FileSystemActionContext(string baseDirectory, FileSystemItem item)
        {
            if (item.IsDirectory)
                Groups = new List<FileSystemActionContextGroup>()
                {
                    new FileSystemActionContextGroup
                    {
                        BaseDirectoryId = baseDirectory,
                        Directories = new FileSystemDirectory[1]{ item as FileSystemDirectory},
                    }
                };
            else
                Groups = new List<FileSystemActionContextGroup>()
                {
                    new FileSystemActionContextGroup
                    {
                        BaseDirectoryId = baseDirectory,
                        Files = new FileSystemFile[1]{ item as FileSystemFile},
                    }
                };
        }

        public List<FileSystemActionContextGroup> Groups { get; set; }

        public bool IsSingleGroup
        {
            get
            {
                return Groups.Count() == 1;
            }
        }

        public bool IsSingleDirectory
        {
            get
            {
                return IsSingleGroup &&
                    SingleGroup.Files.Count() == 0 &&
                    SingleGroup.Directories.Count() == 1;
            }
        }

        public bool IsSingleFile
        {
            get
            {
                return IsSingleGroup &&
                    SingleGroup.Files.Count() == 1 &&
                    SingleGroup.Directories.Count() == 0;
            }
        }

        public FileSystemActionContextGroup SingleGroup
        {
            get
            {
                return Groups.FirstOrDefault();
            }
        }

        public FileSystemFile SingleFile
        {
            get
            {
                return IsSingleGroup &&
                    SingleGroup.Files.Count() == 1 ? SingleGroup.Files.First() as FileSystemFile : null;
            }
        }

        public FileSystemDirectory SingleDirectory
        {
            get
            {
                return IsSingleGroup &&
                    SingleGroup.Directories.Count() == 1 ? SingleGroup.Directories.First() as FileSystemDirectory : null;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Groups.Count() == 0;
            }
        }

        public bool IsEmptyGroup
        {
            get
            {
                return IsSingleGroup &&
                    SingleGroup.Directories.Count() == 0 &&
                    SingleGroup.Files.Count() == 0;
            }
        }

        public bool IsMultiDirectory
        {
            get
            {
                return Groups.SelectMany(g => g.Items).Count() > 0 &&
                    Groups.SelectMany(g => g.Items).All(item => item.IsDirectory);
            }
        }

        public bool IsMultiFile
        {
            get
            {
                return Groups.SelectMany(g => g.Items).Count() > 0 &&
                    Groups.SelectMany(g => g.Items).All(item => !item.IsDirectory);
            }
        }

        public IEnumerable<Tuple<string, string, FileSystemDirectory>> Directories
        {
            get
            {
                return Groups.SelectMany(g => g.Directories.Select(d => new Tuple<string, string, FileSystemDirectory>(g.BaseDirectoryId, d.Id, d as FileSystemDirectory))).ToArray();
            }
        }

        public IEnumerable<Tuple<string, string, FileSystemFile>> Files
        {
            get
            {
                return Groups.SelectMany(g => g.Files.Select(d => new Tuple<string, string, FileSystemFile>(g.BaseDirectoryId, d.Id, d as FileSystemFile))).ToArray();
            }
        }

        public IEnumerable<Tuple<string, string, FileSystemItem>> Items
        {
            get
            {
                return Groups.SelectMany(g => g.Items.Select(i => new Tuple<string, string, FileSystemItem>(g.BaseDirectoryId, i.Id, i))).ToArray();
            }
        }

    }

    public class FileSystemActionContextGroup
    {
        public FileSystemActionContextGroup()
        {
            BaseDirectoryId = "";
            Directories = new List<FileSystemItem>();
            Files = new List<FileSystemItem>();
        }

        public string BaseDirectoryId { get; set; }
        public IList<FileSystemItem> Directories { get; set; }
        public IList<FileSystemItem> Files { get; set; }
        public IList<FileSystemItem> Items
        {
            get
            {
                return Directories.Concat(Files).ToList();
            }
        }
    }
}
