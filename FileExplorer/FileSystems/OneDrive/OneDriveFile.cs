using Open.FileSystemAsync;
using System;
using System.Globalization;

namespace Open.FileExplorer
{
    public class OneDriveFile : FileSystemFile
    {
        #region ** initialization

        public OneDriveFile(string id, string name, string contentType)
        {
            Id = id;
            Name = name;
            _contentType = contentType;
        }

        internal OneDriveFile(Open.OneDrive.Item file, string contentType = null, string parentDirId = null)
        {
            Id = file.Id;
            Name = file.Name;
            _contentType = contentType;
            if (parentDirId != null)
            {
                ParentDirId = parentDirId;
            }
            else if (file.ParentReference != null)
            {
                ParentDirId = OneDriveFileSystem.GetDirPath(file.ParentReference.Path, file.ParentReference.Id);
            }
            Description = file.Description;
            if (file.File != null)
            {
                _contentType = file.File.MimeType;
            }
            else
            {
                _contentType = MimeType.GetContentTypeFromExtension(Path.GetExtension(file.Name));
            }
            if (file.Thumbnails != null && file.Thumbnails.Count > 0)
            {
                var thumbnail = file.Thumbnails[0];
                Thumbnail = thumbnail.Medium.Url;
                Width = thumbnail.Medium.Width;
                Height = thumbnail.Medium.Height;
            }
            Size = file.Size;
            if(file.Video != null)
            {
                Length = TimeSpan.FromMilliseconds(file.Video.Duration);
            }
            //Content = !string.IsNullOrWhiteSpace(file.Source) ? new Uri(file.Source) : null;
            Link = new Uri("https://onedrive.live.com/redir?resid=" + file.Id);
            if (!string.IsNullOrWhiteSpace(file.CreatedDateTime))
                CreatedDate = DateTime.Parse(file.CreatedDateTime, CultureInfo.InvariantCulture.DateTimeFormat);
            if (!string.IsNullOrWhiteSpace(file.LastModifiedDateTime))
                ModifiedDate = DateTime.Parse(file.LastModifiedDateTime, CultureInfo.InvariantCulture.DateTimeFormat);
            if (file.CreatedBy != null && file.CreatedBy.User != null)
                Owner = new OneDrivePerson(file.CreatedBy.User);
            IsReadOnly = true;
        }

        #endregion

        #region ** object model

        public string Url { get; private set; }
        public string Description { get; set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public Uri Content { get; private set; }
        public string ParentDirId { get; private set; }
        public TimeSpan? Length { get; private set; }

        #endregion
    }
}
