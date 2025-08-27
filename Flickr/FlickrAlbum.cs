using System;
using Open.FileSystemAsync;
using Open.Flickr;

namespace Open.FileExplorer
{
    public class FlickrAlbum : FileSystemDirectory
    {
        private string _albumUri = "http://www.flickr.com/photos/{0}/sets/{1}";

        internal FlickrAlbum(string userId, Photoset album)
        {
            Id = album.Id;
            Name = album.Title.Content;
            Link = new Uri(string.Format(_albumUri, userId, album.Id));
            IsReadOnly = true;
        }
    }
}
