using System;
using System.Globalization;
using Open.Facebook;

namespace Open.FileExplorer
{
    public class FacebookVideo : FacebookFile
    {
        internal FacebookVideo(Video video, User user, string albumId)
        {
            //id,name,picture,source
            Id = video.Id;
            Name = video.Description;
            AlbumId = albumId;
            Thumbnail = video.Picture;
            Link = !string.IsNullOrWhiteSpace(video.PermalinkUrl) ? new Uri("http://www.facebook.com" + video.PermalinkUrl) : null;
            Content = !string.IsNullOrWhiteSpace(video.Source) ? new Uri(video.Source, UriKind.Absolute) : null;
            //if (video.Place != null)
            //{
            //    Where = new GeoPosition(video.Place.Location.Latitude, video.Place.Location.Longitude);
            //}
            if (!string.IsNullOrWhiteSpace(video.CreatedTime))
                CreatedDate = DateTime.Parse(video.CreatedTime, CultureInfo.InvariantCulture.DateTimeFormat);
            Owner = new FacebookPerson(user);
            IsReadOnly = true;
        }

        public override string ContentType
        {
            get
            {
                return "video/mp4";
            }
        }

        public string AlbumId { get; private set; }
    }
}
