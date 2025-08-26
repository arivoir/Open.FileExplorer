using System;
using System.Globalization;
using Open.FileSystemAsync;
using Open.GoogleDrive;

namespace Open.FileExplorer
{
    public class GoogleDriveDirectory : FileSystemDirectory
    {
        internal GoogleDriveDirectory(File file)
        {
            Id = file.Id;
            Name = file.Name;
            Parents = file.Parents;
            Link = new Uri(file.WebViewLink);
            Shared = file.Shared;
            Permissions = file.Shared ? "Shared" : "";
            if (!string.IsNullOrWhiteSpace(file.CreatedTime))
                CreatedDate = DateTime.Parse(file.CreatedTime, CultureInfo.InvariantCulture.DateTimeFormat);
            if (!string.IsNullOrWhiteSpace(file.ModifiedTime))
                ModifiedDate = DateTime.Parse(file.ModifiedTime, CultureInfo.InvariantCulture.DateTimeFormat);
            IsReadOnly = true;
        }

        internal GoogleDriveDirectory(string id, string name)
        {
            Id = id;
            Name = name;
            IsSpecial = true;
            IsReadOnly = true;
        }

        internal string[] Parents { get; private set; }
        internal bool IsTrashed { get; set; }
        public bool Shared { get; internal set; }
    }
}
