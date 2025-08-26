using System;
using System.Globalization;
using Open.Box;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class BoxDirectory : FileSystemDirectory
    {
        internal BoxDirectory(Item folder)
        {
            Id = folder.Id;
            Name = folder.Name;
            if (folder.SharedLink != null && !string.IsNullOrWhiteSpace(folder.SharedLink.Url))
                Link = new Uri(folder.SharedLink.Url);
            ParentDirId = folder.Parent != null ? folder.Parent.Id : "0";
            if (!string.IsNullOrWhiteSpace(folder.CreatedAt))
                CreatedDate = DateTime.Parse(folder.CreatedAt, CultureInfo.InvariantCulture.DateTimeFormat);
            Size = folder.Size;
            IsReadOnly = true;
        }

        public string ParentDirId { get; internal set; }
    }
}
