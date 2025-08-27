using Open.FileSystemAsync;
using Open.YouTube;

namespace Open.FileExplorer.YouTube
{
    public class YoutubePlaylist : FileSystemDirectory
    {
        #region initialization

        public YoutubePlaylist(string id, string name, string privacyStatus)
        {
            Id = id;
            Name = name;
            PrivacyStatus = privacyStatus;
        }

        internal YoutubePlaylist(Playlist playlist)
        {
            Id = playlist.Id;
            Name = playlist.Snippet.Title;
            Description = playlist.Snippet.Description;
            Tags = playlist.Snippet.Tags;
            if (playlist.Status != null)
            {
                PrivacyStatus = playlist.Status.PrivacyStatus;
            }
            IsReadOnly = true;
        }

        #endregion

        #region object model

        public string Description { get; set; }
        public string[] Tags { get; set; }
        public string PrivacyStatus { get; set; }

        #endregion
    }
}
