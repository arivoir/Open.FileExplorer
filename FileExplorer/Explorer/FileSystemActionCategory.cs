using System;

namespace Open.FileExplorer
{
    public class FileSystemActionCategory : IComparable
    {
        public FileSystemActionCategory(string name)
            : this(name, 3)
        {
        }

        private FileSystemActionCategory(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public string Name { get; private set; }
        internal int Order { get; private set; }

        public static readonly FileSystemActionCategory Open = new FileSystemActionCategory("Open", 1);
        public static readonly FileSystemActionCategory Share = new FileSystemActionCategory("Share", 2);
        public static readonly FileSystemActionCategory Modify = new FileSystemActionCategory("Modify", 3);
        public static readonly FileSystemActionCategory Copy = new FileSystemActionCategory("Copy", 4);
        public static readonly FileSystemActionCategory Properties = new FileSystemActionCategory("Properties", 5);
        public static readonly FileSystemActionCategory Sort = new FileSystemActionCategory("Sort", 6);
        public static readonly FileSystemActionCategory Refresh = new FileSystemActionCategory("Refresh", 7);


        public int CompareTo(object obj)
        {
            if (obj is FileSystemActionCategory)
                return Order.CompareTo((obj as FileSystemActionCategory).Order);
            return 1;
        }
    }
}
