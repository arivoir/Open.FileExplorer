using System;
using System.Globalization;

using Open.Facebook;
using Open.FileSystem;

namespace Open.FileExplorer
{
    public class FacebookFile : FileSystemFile
    {
        public Uri Content { get; internal set; }
    }

    public class FacebookPhoto : FacebookFile
    {
        public FacebookPhoto()
        {
        }

        internal FacebookPhoto(Photo photo, User user, string albumId)
        {
            Id = photo.Id;
            Name = photo.Name;
            AlbumId = albumId;
            Thumbnail = photo.Picture;
            Link = !string.IsNullOrWhiteSpace(photo.Link) ? new Uri(photo.Link) : null;
            Content = !string.IsNullOrWhiteSpace(photo.Source) ? new Uri(photo.Source, UriKind.Absolute) : null;
            Width = photo.Width;
            Height = photo.Height;
            //if (photo.Place != null)
            //{
            //    Where = new GeoPosition(photo.Place.Location.Latitude, photo.Place.Location.Longitude);
            //}
            if (!string.IsNullOrWhiteSpace(photo.CreatedTime))
                CreatedDate = DateTime.Parse(photo.CreatedTime, CultureInfo.InvariantCulture.DateTimeFormat);
            Owner = new FacebookPerson(user);
            IsReadOnly = true;
        }

        public double Width { get; private set; }
        public double Height { get; private set; }

        public override string ContentType
        {
            get
            {
                return "image/jpeg";
            }
        }

        public string AlbumId { get; private set; }
    }
}
