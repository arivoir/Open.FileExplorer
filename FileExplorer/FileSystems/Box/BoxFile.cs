using System;
using System.Globalization;
using Open.Box;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class BoxFile : FileSystemFile
    {
        internal BoxFile(Item file)
        {
            Id = file.Id;
            Name = file.Name;
            Size = file.Size;
            if (!string.IsNullOrWhiteSpace(file.CreatedAt))
                CreatedDate = DateTime.Parse(file.CreatedAt, CultureInfo.InvariantCulture.DateTimeFormat);
            Owner = new BoxPerson(file.OwnedBy);
            ParentDirId = file.Parent.Id;
            IsReadOnly = true;
        }

        public override bool HasThumbnail
        {
            get
            {
                return ContentType == "image/jpeg" || ContentType == "image/png";
            }
        }

        public string ParentDirId { get; private set; }
    }
}
