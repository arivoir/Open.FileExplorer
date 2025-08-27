using System;
using Open.FileSystemAsync;
using Open.YouTube;

namespace Open.FileExplorer
{
    public class YouTubeVideo : FileSystemFile
    {
        #region initialization

        public YouTubeVideo(string id, string name, string contentType)
        {
            Id = id;
            Name = name;
            _contentType = contentType;
            Embeddable = true;
            PrivacyStatus = "public";
            License = "youtube";
            PublicStatsViewable = true;
            Latitude = double.NaN;
            Longitude = double.NaN;
        }

        internal YouTubeVideo(PlaylistItem playlistItem)
        {
            Id = playlistItem.Snippet.ResourceId.VideoId;
            Name = playlistItem.Snippet.Title;
            Link = new Uri("http://www.youtube.com/watch?v=" + playlistItem.Snippet.ResourceId.VideoId);
            var thumbnail = playlistItem.Snippet.Thumbnails.Default;
            if (thumbnail != null)
            {
                Thumbnail = thumbnail.Url;
            }
            IsReadOnly = true;
        }

        internal YouTubeVideo(Video video)
        {
            Id = video.Id;
            Name = video.Snippet.Title;
            Link = new Uri("http://www.youtube.com/watch?v=" + video.Id);
            Description = video.Snippet.Description;
            CategoryId = video.Snippet.CategoryId;
            PrivacyStatus = video.Status.PrivacyStatus;
            Tags = video.Snippet.Tags;
            Embeddable = video.Status.Embeddable;
            License = video.Status.License;
            PublicStatsViewable = video.Status.PublicStatsViewable;
            if (!string.IsNullOrWhiteSpace(video.Snippet.PublishedAt))
                CreatedDate = DateTime.Parse(video.Snippet.PublishedAt);
            PublishAt = video.Status.PublishAt;
            if (video.RecordingDetails != null)
            {
                LocationDescription = video.RecordingDetails.LocationDescription;
                Latitude = video.RecordingDetails.Location.Latitude;
                Longitude = video.RecordingDetails.Location.Longitude;
                RecordingDate = video.RecordingDetails.RecordingDate;
            }
            else
            {
                Latitude = double.NaN;
                Longitude = double.NaN;
            }
            if (video.Snippet.Thumbnails != null)
            {
                var thumbnail = video.Snippet.Thumbnails.Default;
                if (thumbnail != null)
                {
                    Thumbnail = thumbnail.Url;
                }
            }
            IsReadOnly = true;
        }

        internal YouTubeVideo(SearchResult searchResult)
        {
            Id = searchResult.Id.VideoId;
            Name = searchResult.Snippet.Title;
            Link = new Uri("http://www.youtube.com/watch?v=" + searchResult.Id.VideoId);
            var thumbnail = searchResult.Snippet.Thumbnails.Default;
            if (thumbnail != null)
            {
                Thumbnail = thumbnail.Url;
            }
            Latitude = double.NaN;
            Longitude = double.NaN;
            IsReadOnly = true;
        }

        #endregion

        #region object model


        public string Description { get; set; }
        public string[] Tags { get; set; }
        public string CategoryId { get; set; }
        public string PrivacyStatus { get; set; }
        public bool Embeddable { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string License { get; set; }
        public bool PublicStatsViewable { get; set; }
        public string PublishAt { get; set; }
        public string LocationDescription { get; set; }
        public string RecordingDate { get; set; }

        public override string ContentType
        {
            get
            {
                return _contentType;
            }
        }

        #endregion
    }
}
