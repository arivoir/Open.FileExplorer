using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Open.FileSystemAsync;
using Open.WebDav;

namespace Open.FileExplorer
{
    public class WebDavFile : FileSystemFile
    {
        private new string _contentType;

        internal WebDavFile(string id, string name, string contentType)
        {
            Id = id;
            Name = name;
            _contentType = contentType;
            IsReadOnly = true;
        }

        internal WebDavFile(XElement file)
        {
            //XElement = file;
            var name = WebDavFileSystem.GetName(file);
            Id = name;
            Name = name;
            var contentLength = file.Descendants(WebDavClient.GetContentLength).FirstOrDefault();
            if (contentLength != null)
                Size = long.Parse(contentLength.Value);
            var contentType = file.Descendants(WebDavClient.GetContentType).FirstOrDefault();
            if (contentType != null && !string.IsNullOrWhiteSpace(contentType.Value))
                _contentType = contentType.Value;
            else
                _contentType = MimeType.GetContentTypeFromExtension(Path.GetExtension(name));
            var creationDate = file.Descendants(WebDavClient.CreationDate).FirstOrDefault();
            if (creationDate != null)
                CreatedDate = DateTime.Parse(creationDate.Value, CultureInfo.InvariantCulture.DateTimeFormat);

            var getLastModified = file.Descendants(WebDavClient.GetLastModified).FirstOrDefault();
            if (getLastModified != null)
                ModifiedDate = DateTime.Parse(getLastModified.Value, CultureInfo.InvariantCulture.DateTimeFormat);

            //var contentType = file.Descendants(WebDavClient.GetContent).FirstOrDefault();
            //if (contentType != null)
            //    _contentType = contentType.Value;
            //Id = file.Id;
            //Name = file.Title;
            //Size = file.FileSize;
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
            //CreatedDate = DateTime.Parse(file.CreatedDate);
            //Owner = new WebDavPerson(owner);
            IsReadOnly = true;
        }

        public override string ContentType
        {
            get
            {
                return _contentType ?? base.ContentType;
            }
        }

        internal void SetContentType(string contentType)
        {
            _contentType = contentType;
        }

        //public override bool HasThumbnail
        //{
        //    get
        //    {
        //        return ContentType == "image/jpeg" || ContentType == "image/png" || ContentType == "image/bmp";
        //    }
        //}

        //public double Width { get; private set; }
        //public double Height { get; private set; }
        //public Uri Content { get; private set; }
        //public Uri DownloadUri { get; private set; }
        //public string FileExtension { get; private set; }
        //public Parent[] Parents { get; private set; }

        //public XElement XElement { get; private set; }
    }
}
