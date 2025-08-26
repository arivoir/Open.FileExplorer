using System;
using Open.FileSystemAsync;
using Open.Flickr;

namespace Open.FileExplorer
{
    public class FlickrPhoto : FileSystemFile
    {
        private string _urlPattern = "https://farm{0}.staticflickr.com/{1}/{2}_{3}_{4}.jpg";
        private string _photoUri = "https://www.flickr.com/photos/{0}/{1}";

        public FlickrPhoto()
        {
        }

        internal FlickrPhoto(Photo photo)
        {
            Id = photo.Id;
            Name = photo.Title;
            Thumbnail = string.Format(_urlPattern, photo.Farm, photo.Server, photo.Id, photo.Secret, "m");
            Link = new Uri(string.Format(_photoUri, photo.Owner, photo.Id));
            Content = new Uri(string.Format(_urlPattern, photo.Farm, photo.Server, photo.Id, photo.Secret, "b"));
            IsPublic = photo.IsPublic == 1;
            IsFriend = photo.IsFriend == 1;
            IsFamily = photo.IsFamily == 1;
            if (!double.IsNaN(photo.Latitude) &&
                photo.Latitude != 0 &&
                !double.IsNaN(photo.Longitude) &&
                photo.Longitude != 0)
            {
                Where = new GeoPosition(photo.Latitude, photo.Longitude);
            }
            if (!string.IsNullOrWhiteSpace(photo.DateUpload))
                CreatedDate = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(long.Parse(photo.DateUpload));
            Owner = new FlickrPerson(photo.Farm, photo.Server, photo.Owner, photo.OwnerName);
            IsReadOnly = true;
        }

        internal FlickrPhoto(Photo2 photo)
        {
            Id = photo.Id;
            Name = photo.Title.Content;
            Thumbnail = string.Format(_urlPattern, photo.Farm, photo.Server, photo.Id, photo.Secret, "t");
            Link = new Uri(string.Format(_photoUri, photo.Owner, photo.Id));
            Content = new Uri(string.Format(_urlPattern, photo.Farm, photo.Server, photo.Id, photo.Secret, "b"));
            IsPublic = photo.Visibility.IsPublic == 1;
            IsFriend = photo.Visibility.IsFriend == 1;
            IsFamily = photo.Visibility.IsFamily == 1;
            if (!string.IsNullOrWhiteSpace(photo.DateUploaded))
                CreatedDate = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(long.Parse(photo.DateUploaded));
            Owner = new FlickrPerson(photo.Owner.IconFarm, photo.Owner.IconServer, photo.Owner.Id, photo.Owner.RealName);
            IsReadOnly = true;
        }

        public double Width { get; private set; }
        public double Height { get; private set; }
        public Uri Content { get; private set; }
        public string Description { get; private set; }

        public bool IsPublic { get; set; }
        public bool IsFriend { get; set; }
        public bool IsFamily { get; set; }
        public bool IsHidden { get; set; }
        public int SafetyLevel { get; set; }
        public override string ContentType
        {
            get
            {
                return "image/jpeg";
            }
        }
    }
}
