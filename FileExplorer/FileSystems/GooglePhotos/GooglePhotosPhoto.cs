using System;
using System.Linq;
using Open.FileSystemAsync;
using Open.GooglePhotos;

namespace Open.FileExplorer
{
    public class GooglePhotosPhoto : FileSystemFile
    {
        #region ** initialization

        public GooglePhotosPhoto(string id, string name, string contentType)
        {
            Id = id;
            Name = name;
            _contentType = contentType;
        }

        internal GooglePhotosPhoto(MediaItem mediaItem, string albumId = null)
        {
            Id = mediaItem.Id;
            Name = mediaItem.Filename;
            AlbumId = albumId;
            Summary = mediaItem.Description;
            Link = new Uri(mediaItem.ProductUrl);
            _contentType = mediaItem.MimeType;
            if (mediaItem.BaseUrl != null)
                Content = new Uri(mediaItem.BaseUrl);
            Width = mediaItem.MediaMetadata.Width;
            Height = mediaItem.MediaMetadata.Height;
            CreatedDate = DateTime.Parse(mediaItem.MediaMetadata.CreationTime);
            if (mediaItem.ContributorInfo != null)
                Owner = new GooglePhotosPerson(mediaItem.ContributorInfo);
            IsReadOnly = true;
        }

        #endregion

        #region ** object model

        public string Uri { get; private set; }
        public string ETag { get; private set; }
        public string Summary { get; set; }
        public string Keywords { get; private set; }
        //public Access Access { get; private set; }
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

        public string AlbumId { get; private set; }

        #endregion
    }
}
