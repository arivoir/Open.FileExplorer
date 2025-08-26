using Open.FileSystemAsync;
using Open.YouTube;

namespace Open.FileExplorer
{
    public class YouTubeSubscription : FileSystemDirectory
    {
        internal YouTubeSubscription(Subscription subscription)
        {
            Id = subscription.Snippet.ResourceId.ChannelId;
            Name = subscription.Snippet.Title;
            ChannelId = subscription.Snippet.ResourceId.ChannelId;
            ETag = subscription.ETag;
            IsReadOnly = true;
        }

        public string ChannelId { get; private set; }
        public string ETag { get; private set; }
    }
}
