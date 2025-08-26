using System;
using Open.FileSystemAsync;
using Open.Mega;

namespace Open.FileExplorer
{
    public class MegaFile : FileSystemFile
    {
        //private string _contentType;

        internal MegaFile(Node file)
        {
            Node = file;
            Id = file.Id;
            Name = file.Name;
            Size = file.Size;
            //if (!string.IsNullOrWhiteSpace(file.ThumbnailLink) /*&& (file.MimeType == "image/jpeg" || file.MimeType == "image/png")*/)
            //    Thumbnail = file.ThumbnailLink;
            //if (!string.IsNullOrWhiteSpace(file.WebContentLink))
            //    Content = new Uri(file.WebContentLink);
            //if (!string.IsNullOrWhiteSpace(file.AlternateLink))
            //    Link = new Uri(file.AlternateLink);
            //if (!string.IsNullOrWhiteSpace(file.DownloadUrl))
            //    DownloadUri = new Uri(file.DownloadUrl);
            //_contentType = file.MimeType;
            //FileExtension = file.FileExtension;
            //Parents = file.Parents;
            //if (!string.IsNullOrWhiteSpace(file.CreatedDate))
            //    CreatedDate = DateTime.Parse(file.CreatedDate, CultureInfo.InvariantCulture.DateTimeFormat);
            //if (!string.IsNullOrWhiteSpace(file.ModifiedDate))
            //    ModifiedDate = DateTime.Parse(file.ModifiedDate, CultureInfo.InvariantCulture.DateTimeFormat);
            //Owner = new MegaPerson(owner);
            IsReadOnly = true;
        }

        //public override string ContentType
        //{
        //    get
        //    {
        //        return _contentType ?? base.ContentType;
        //    }
        //}

        //public override bool HasThumbnail
        //{
        //    get
        //    {
        //        return ContentType == "image/jpeg" || ContentType == "image/png" || ContentType == "image/bmp";
        //    }
        //}

        public double Width { get; private set; }
        public double Height { get; private set; }
        public Uri Content { get; private set; }
        public Uri DownloadUri { get; private set; }
        public string FileExtension { get; private set; }
        internal Node Node { get; private set; }
    }
}
