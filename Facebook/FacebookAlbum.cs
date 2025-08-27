using System;
using Open.Facebook;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class FacebookAlbum : FileSystemDirectory
    {
        public FacebookAlbum(string id, string name, FacebookPermission permission)
        {
            Id = id;
            Name = name;
            Permission = permission;
        }

        internal FacebookAlbum(string id, string name)
        {
            Id = id;
            Name = name;
            IsSpecial = true;
            IsReadOnly = true;
        }

        internal FacebookAlbum(Album album)
        {
            Id = album.Id;
            Name = album.Name;
            Count = (int)album.Count;
            Thumbnail = album.Picture;
            CanUpload = album.CanUpload;
            Type = album.Type;
            CoverPhoto = album.CoverPhoto;
            if (album.Link != null)
                Link = new Uri(album.Link);
            if (!string.IsNullOrWhiteSpace(album.Privacy))
            {
                switch (album.Privacy.ToLower())
                {
                    case "everyone":
                        Permission = FacebookPermission.Public;
                        Permissions = "Public";
                        break;
                    case "friends":
                    case "custom":
                        Permission = FacebookPermission.Friends;
                        Permissions = "";// "Shared";
                        break;
                    default:
                        Permission = FacebookPermission.OnlyMe;
                        Permissions = "Private";
                        break;
                }
            }
            IsReadOnly = true;
        }

        public FacebookPermission Permission { get; set; }
        public bool CanUpload { get; private set; }
        public string CoverPhoto { get; private set; }
        public string Type { get; private set; }
    }

    public enum FacebookPermission
    {
        Public,
        Friends,
        OnlyMe
    }
}
