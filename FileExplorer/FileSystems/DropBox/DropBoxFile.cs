using System;
using Open.DropBox;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class DropBoxFile : FileSystemFile
    {
        internal DropBoxFile(Item file)
        {
            Id = Open.FileSystemAsync.Path.GetFileName(file.PathLower);
            Name = file.Name;
            Size = file.Size;
            Path = file.PathDisplay;
            Link = new Uri(string.Format("https://www.dropbox.com/home{0}?preview={1}", DropBoxClient.GetValidPath(Open.FileSystemAsync.Path.GetParentPath(file.PathDisplay)), file.Name));
            if (!string.IsNullOrWhiteSpace(file.ServerModified))
                ModifiedDate = DateTime.Parse(file.ServerModified);
            IsReadOnly = true;
        }

        public string Path { get; private set; }
    }
}
