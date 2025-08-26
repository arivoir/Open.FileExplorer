using System;
using System.Globalization;
using System.Linq;
using Open.FileSystemAsync;
using Open.Twitter;

namespace Open.FileExplorer
{
    public class TwitterFile : FileSystemFile
    {
        internal TwitterFile(Tweet file)
        {
            Id = file.IdStr;
            Name = file.Text ?? "";
            var media = file.Entities != null && file.Entities.Media != null ? file.Entities.Media.FirstOrDefault() : null;
            if (media != null)
            {
                Thumbnail = media.MediaUrl + ":small";
                Link = new Uri(media.Url);
                Content = new Uri(media.MediaUrl + ":large");
                _contentType = media.Type == "photo" ? "image/jpeg" : "";
                Width = media.Sizes.Large.W;
                Height = media.Sizes.Large.H;
                DateTime createdDate;
                if (DateTime.TryParseExact(file.CreatedAt, "ddd MMM dd HH:mm:ss zz00 yyyy", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.None, out createdDate))
                {
                    CreatedDate = createdDate;
                }
                Owner = new TwitterPerson(file.User);
            }
            IsReadOnly = true;
        }

        public double Width { get; private set; }
        public double Height { get; private set; }
        public Uri Content { get; private set; }
        public override string ContentType
        {
            get
            {
                return _contentType;
            }
        }
    }
}
