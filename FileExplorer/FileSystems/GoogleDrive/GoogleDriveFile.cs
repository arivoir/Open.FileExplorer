using System;
using System.Globalization;
using Open.FileSystemAsync;
using Open.GoogleDrive;

namespace Open.FileExplorer
{
    public class GoogleDriveFile : FileSystemFile
    {
        internal GoogleDriveFile(File file, User owner)
        {
            Id = file.Id;
            Name = file.Name;
            Size = file.Size;
            if (!string.IsNullOrWhiteSpace(file.ThumbnailLink) /*&& (file.MimeType == "image/jpeg" || file.MimeType == "image/png")*/)
                Thumbnail = file.ThumbnailLink;
            if (!string.IsNullOrWhiteSpace(file.WebViewLink))
                Link = new Uri(file.WebViewLink);
            if (!string.IsNullOrWhiteSpace(file.WebContentLink))
                DownloadUri = new Uri(file.WebContentLink);
            _contentType = file.MimeType;
            FileExtension = file.FileExtension;
            Parents = file.Parents;
            if (!string.IsNullOrWhiteSpace(file.CreatedTime))
                CreatedDate = DateTime.Parse(file.CreatedTime, CultureInfo.InvariantCulture.DateTimeFormat);
            if (!string.IsNullOrWhiteSpace(file.ModifiedTime))
                ModifiedDate = DateTime.Parse(file.ModifiedTime, CultureInfo.InvariantCulture.DateTimeFormat);
            Owner = new GoogleDrivePerson(owner);
            IsReadOnly = true;
        }

        public Uri DownloadUri { get; private set; }
        public string FileExtension { get; private set; }
        public string[] Parents { get; private set; }
        public bool IsTrashed { get; internal set; }
    }
}
