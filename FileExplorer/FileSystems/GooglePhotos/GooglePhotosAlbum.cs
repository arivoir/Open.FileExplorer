using System;
using Open.FileSystemAsync;
using Open.GooglePhotos;

namespace Open.FileExplorer
{
    public class GooglePhotosAlbum : FileSystemDirectory
    {
        #region ** initialization

        public GooglePhotosAlbum(string id, string name)
        {
            Id = id;
            Name = name;
        }

        internal GooglePhotosAlbum(Album album)
        {
            Id = album.Id;
            Name = album.Title ?? "";
            //Description = album.Description;
            //Location = album.Location;
            //Access = album.Access;
            //Uri = album.Uri;
            Thumbnail = album.CoverPhotoBaseUrl != null ? album.CoverPhotoBaseUrl : null;
            //ETag = album.ETag;
            //Link = new Uri(album.Link);
            //Date = album.Date;
            //switch (album.Access)
            //{
            //    case Access.Public:
            //        Permissions = "Public";
            //        break;
            //    case Access.Private:
            //        Permissions = "Shared";
            //        break;
            //    case Access.Protected:
            //        Permissions = "Private";
            //        break;
            //}
            IsReadOnly = true;
        }

        #endregion

        #region ** object model

        public string Uri { get; private set; }
        public string ETag { get; private set; }
        public string Keywords { get; private set; }
        public DateTime Date { get; private set; }
        public string Description { get; set; }
        public string Location { get; set; }
        //public Access Access { get; set; }

        #endregion
    }
}
