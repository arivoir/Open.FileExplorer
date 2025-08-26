using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using Open.YouTube;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class YouTubeFileSystem : AuthenticatedFileSystem
    {
        #region ** fields

        public static string ClientId { get; private set; }
        public static string ClientSecret { get; private set; }
        public static string RedirectUri { get; private set; }

        private static string _channelParts = "snippet,contentDetails";
        private static string _playlistParts = "snippet,status";
        private static string _playlistsParts = "snippet";
        private static string _playlistItemsParts = "snippet";
        private static string _subscriptionPart = "snippet";
        private static string _videoParts = "snippet,status,recordingDetails";
        private static string _playlistsFields = "nextPageToken,items(id,snippet(title))";
        private static string _subscriptionsFields = "nextPageToken,items(snippet(title,resourceId/channelId))";
        private static string _channelFields = "items/contentDetails/relatedPlaylists/uploads";
        private static string _playlistItemFields = "nextPageToken,items(snippet(title,thumbnails/default/url,resourceId/videoId))";
        private static string _searchFields = "nextPageToken,items(id/videoId,snippet(title,thumbnails/default/url))";
        private Channel _channel;

        #endregion

        #region ** initialization

        static YouTubeFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["GoogleId"];
            ClientSecret = ConfigurationManager.AppSettings["GoogleSecret"];
            RedirectUri = ConfigurationManager.AppSettings["GoogleRedirectUri"];
        }

        #endregion

        #region ** authentication

        public override string[] GetScopes(string dirId)
        {
            return new string[] { YouTubeClient.Scope };
        }


        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            _channel = ticket.Tag as Channel;
            if (_channel == null)
            {
                var client = new YouTubeClient(ticket.AuthToken);
                var channels = await client.GetChannelsAsync(_channelParts, true, _channelFields, cancellationToken);
                _channel = channels.Items.FirstOrDefault();
            }
            return ticket;
        }

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var callbackUrl = RedirectUri;
            var authenticationUrl = new Uri(YouTubeClient.GetRequestUrl(YouTubeFileSystem.ClientId, callbackUrl), UriKind.Absolute);
            var result = await authenticationBroker.WebAuthenticationBrokerAsync(authenticationUrl, new Uri(callbackUrl));

            var fragments = UriEx.ProcessFragments(result.Query);
            string code;
            if (fragments.TryGetValue("code", out code))
            {
                var token = await YouTubeClient.ExchangeCodeForAccessTokenAsync(code, YouTubeFileSystem.ClientId, YouTubeFileSystem.ClientSecret, callbackUrl);
                var client = new YouTubeClient(token.AccessToken);
                var channels = await client.GetChannelsAsync(_channelParts, true, _channelFields, CancellationToken.None);
                var channel = channels.Items.FirstOrDefault();
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn), Tag = channel, GrantedScopes = new string[] { YouTubeClient.Scope } };
            }
            else if (fragments.TryGetValue("error", out code) && code == "access_denied")
            {
                throw new OperationCanceledException();
            }
            else
            {
                throw new Exception("code not found.");
            }
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var token = await YouTubeClient.RefreshAccessTokenAsync(refreshToken, ClientId, ClientSecret, cancellationToken);
                var client = new YouTubeClient(token.AccessToken);
                var channels = await client.GetChannelsAsync(_channelParts, true, _channelFields, cancellationToken);
                var channel = channels.Items.FirstOrDefault();
                return new AuthenticatonTicket { AuthToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpirationTime = DateTime.Now + TimeSpan.FromSeconds(token.ExpiresIn), Tag = channel, GrantedScopes = new string[] { YouTubeClient.Scope } };
            }
            catch (Exception exc)
            {
                throw ProcessOAuthException(exc);
            }
        }

        #endregion

        #region ** get info

        protected override DirPathMode DirPathMode
        {
            get
            {
                return DirPathMode.FullPathAsId;
            }
        }

        protected override UniqueFileNameMode UniqueFileNameMode
        {
            get
            {
                return UniqueFileNameMode.DirName_FileId_Extension;
            }
        }

        protected override string GetDirectoryParentId(FileSystemDirectory directory)
        {
            return "";
        }

        protected override string GetFileParentId(FileSystemFile file)
        {
            return base.GetFileParentId(file);
        }

        protected override Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            IDataCollection<FileSystemDirectory> result;
            if (string.IsNullOrWhiteSpace(dirId))
            {
                var playlistsCollection = new CursorList<FileSystemDirectory>(
                    async (pageSize, pageToken, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new YouTubeClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.GetPlaylistsAsync(_playlistsParts, true, _playlistsFields, pageToken, pageSize);
                            var directories = new List<FileSystemDirectory>();
                            foreach (var playlist in r.Items)
                            {
                                var p = new YoutubePlaylist(playlist);
                                directories.Add(p);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(r.NextPageToken, directories);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    })
                { PageSize = 50 };
                var subscriptionsCollection = new CursorList<FileSystemDirectory>(
                    async (pageSize, pageToken, sd, fe, ct) =>
                    {
                        try
                        {
                            var client = new YouTubeClient(await GetAccessTokenAsync(true, ct));
                            var r = await client.GetSubscriptionsAsync(_subscriptionPart, true, fields: _subscriptionsFields, pageToken: pageToken, maxResults: pageSize);
                            var directories = new List<FileSystemDirectory>();
                            foreach (var subscription in r.Items)
                            {
                                var s = new YouTubeSubscription(subscription);
                                directories.Add(s);
                            }
                            return new Tuple<string, IReadOnlyList<FileSystemDirectory>>(r.NextPageToken, directories);
                        }
                        catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                    })
                { PageSize = 50 };
                result = new YouTubeDirectories(new IDataCollection<FileSystemDirectory>[] { playlistsCollection, subscriptionsCollection });
            }
            else
            {
                result = new EmptyCollectionView<FileSystemDirectory>();
            }
            return Task.FromResult(result);
        }

        protected override async Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(await GetAccessTokenAsync(true, cancellationToken)))
            {
                if (string.IsNullOrWhiteSpace(dirId))
                {
                    return new CursorList<FileSystemFile>(
                        async (pageSize, pageToken, sd, fe, ct) =>
                        {
                            try
                            {
                                var client = new YouTubeClient(await GetAccessTokenAsync(true, ct));
                                var result = await client.GetPlaylistItemsAsync(_channel.ContentDetails.RelatedPlaylists.Uploads, _playlistItemsParts, fields: _playlistItemFields, pageToken: pageToken, maxResults: pageSize);
                                var files = ConvertVideos(result.Items);
                                return new Tuple<string, IReadOnlyList<FileSystemFile>>(result.NextPageToken, files);
                            }
                            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                        })
                    { PageSize = 50 };
                }
                else
                {
                    var dir = await GetDirectoryAsync(dirId, false, cancellationToken);
                    if (dir is YouTubeSubscription)
                    {
                        return new CursorList<FileSystemFile>(
                            async (pageSize, pageToken, sd, fe, ct) =>
                            {
                                try
                                {
                                    var subscription = await GetDirectoryAsync(dirId, false, ct) as YouTubeSubscription;
                                    var client = new YouTubeClient(await GetAccessTokenAsync(true, ct));
                                    var channel = await client.GetChannelAsync(_channelParts, subscription.ChannelId);
                                    var result = await client.GetPlaylistItemsAsync(channel.ContentDetails.RelatedPlaylists.Uploads, _playlistItemsParts, fields: _playlistItemFields, pageToken: pageToken, maxResults: pageSize);
                                    var videos = new List<YouTubeVideo>();
                                    foreach (var video in result.Items)
                                    {
                                        var videoPath = Path.Combine(dirId, video.Snippet.ResourceId.VideoId);
                                        var v = new YouTubeVideo(video);
                                        videos.Add(v);
                                    }
                                    return new Tuple<string, IReadOnlyList<FileSystemFile>>(result.NextPageToken, videos.Cast<FileSystemFile>().ToList());
                                }
                                catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                            })
                        { PageSize = 50 };
                    }
                    else if (dir is YoutubePlaylist)
                    {
                        var playlist = dir as YoutubePlaylist;
                        return new CursorList<FileSystemFile>(
                            async (pageSize, pageToken, sd, fe, ct) =>
                            {
                                try
                                {
                                    var client = new YouTubeClient(await GetAccessTokenAsync(true, ct));
                                    var result = await client.GetPlaylistItemsAsync(dirId, _playlistItemsParts, _playlistItemFields, pageToken, pageSize);
                                    var videos = new List<FileSystemFile>();
                                    foreach (var video in result.Items)
                                    {
                                        var photoPath = Path.Combine(dirId, video.Snippet.ResourceId.VideoId);
                                        var v = new YouTubeVideo(video);
                                        videos.Add(v);
                                    }
                                    return new Tuple<string, IReadOnlyList<FileSystemFile>>(result.NextPageToken, videos);
                                }
                                catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                            })
                        { PageSize = 50 };
                    }
                }
            }
            return new EmptyCollectionView<FileSystemFile>();
        }

        protected override async Task<FileSystemDirectory> GetDirectoryAsyncOverride(string dirId, bool full, CancellationToken cancellationToken)
        {
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var v = await client.GetPlaylistAsync(_playlistParts, dirId);
            if (v != null)
                return new YoutubePlaylist(v);
            return null;
        }

        protected override async Task<FileSystemFile> GetFileAsyncOverride(string fileId, bool full, CancellationToken cancellationToken)
        {
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var v = await client.GetVideoAsync(Path.GetFileName(fileId), _videoParts);
            return new YouTubeVideo(v);
        }

        private List<FileSystemFile> ConvertVideos(IList<PlaylistItem> videos)
        {
            var files = new List<FileSystemFile>();
            foreach (var video in videos)
            {
                var filePath = video.Snippet.ResourceId.VideoId;
                var v = new YouTubeVideo(video);
                files.Add(v);
            }
            return files;
        }

        #endregion

        #region ** upload

        protected override string[] GetAcceptedFileTypesOverride(string dirId, bool includeSubDirectories)
        {
            return new string[] { "video/*", "application/octet-stream" };
        }

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(dirId));
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string path, FileSystemFile item, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var video = item as YouTubeVideo;
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var uploadToken = await client.InsertVideo(_videoParts, video.Name, video.Description, video.CategoryId, video.Tags, video.License, video.Embeddable, video.PrivacyStatus, video.PublicStatsViewable, video.PublishAt, video.LocationDescription, video.Latitude, video.Longitude, video.RecordingDate);

            var v = await client.UploadVideo(item.ContentType, fileStream, uploadToken, progress, cancellationToken);

            if (!string.IsNullOrWhiteSpace(path))
            {
                //await client.AddVideoToPlaylist(uploadedVideo.Id, path);
            }
            return new YouTubeVideo(v);
        }

        #endregion

        #region ** create

        protected override Task<bool> CanCreateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.IsNullOrWhiteSpace(dirId));
        }

        protected override async Task<FileSystemDirectory> CreateDirectoryAsyncOverride(string dirId, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var playlist = item as YoutubePlaylist;
            var result = await client.InsertPlaylistAsync(_playlistParts, playlist.Name, playlist.Description, playlist.PrivacyStatus, playlist.Tags);
            var newPlaylist = new YoutubePlaylist(result);
            return newPlaylist;
        }

        #endregion

        #region ** move

        protected override Task<bool> CanMoveFileOverride(string sourceFileId, string targetDirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        //protected override async Task<FileSystemFile> MoveFileAsyncOverride(string sourceFilePath, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        //{
        //    return null;
        //}

        #endregion

        #region ** update

        protected override async Task<bool> CanUpdateDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            var playlist = await GetDirectoryAsync(dirId, false, cancellationToken) as YoutubePlaylist;
            return playlist != null;
        }

        protected override async Task<FileSystemDirectory> UpdateDirectoryAsyncOverride(string path, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            var playlist = item as YoutubePlaylist;
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var updated = await client.UpdatePlaylistAsync(playlist.Id, _playlistParts, playlist.Name, playlist.Description, playlist.Tags, playlist.PrivacyStatus);
            return new YoutubePlaylist(updated);
        }

        protected override async Task<bool> CanUpdateFileOverride(string fileId, CancellationToken cancellationToken)
        {
            var folderId = await GetFileParentIdAsync(fileId, cancellationToken);
            if (string.IsNullOrWhiteSpace(folderId))
                return true;
            var playlist = await GetDirectoryAsync(folderId, false, cancellationToken) as YoutubePlaylist;
            return playlist != null;
        }

        protected override async Task<FileSystemFile> UpdateFileAsyncOverride(string fileId, FileSystemFile item, CancellationToken cancellationToken)
        {
            var video = await GetFileAsync(fileId, false, cancellationToken);
            var newVideo = item as YouTubeVideo;
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.UpdateVideoAsync(_videoParts, video.Id, newVideo.Name, newVideo.Description, newVideo.CategoryId, newVideo.Tags, newVideo.License, newVideo.Embeddable, newVideo.PrivacyStatus, newVideo.PublicStatsViewable, newVideo.PublishAt, newVideo.LocationDescription, newVideo.Latitude, newVideo.Longitude, newVideo.RecordingDate);
            return new YouTubeVideo(result);
        }

        #endregion

        #region ** delete

        protected override async Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            var folderId = await GetFileParentIdAsync(fileId, cancellationToken);
            return string.IsNullOrWhiteSpace(folderId);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            await client.DeleteVideoAsync(Path.GetFileName(fileId));
            return null;
        }

        protected override Task<bool> CanDeleteDirectoryOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemDirectory> DeleteDirectoryAsyncOverride(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            await client.DeletePlaylistAsync(dirId);
            return null;
        }

        #endregion

        #region ** search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            var client = new YouTubeClient(await GetAccessTokenAsync(true, cancellationToken));
            var result = await client.SearchAsync("snippet", "video", true, q: query, fields: _searchFields);
            var searchResult = new List<FileSystemSearchItem>();
            foreach (var video in result.Items)
            {
                var v = new YouTubeVideo(video);
                searchResult.Add(new FileSystemSearchItem { DirectoryId = "", Item = v });
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region ** implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            var yExc = exc as YouTubeException;
            if (yExc != null)
            {
                if (yExc.Error.Code == 401)
                {
                    return Task.FromResult<Exception>(new AccessDeniedException());
                }
            }
            return Task.FromResult(ProcessOAuthException(exc));
        }

        #endregion
    }

    internal class YouTubeDirectories : C1SequenceDataCollection<FileSystemDirectory>
    {
        public YouTubeDirectories(IEnumerable<IDataCollection<FileSystemDirectory>> sources)
        {
            foreach (var source in sources)
            {
                Collections.Add(source);
            }
        }

        public override bool CanInsert(int index, FileSystemDirectory item)
        {
            return true;
        }

        public override Task<int> InsertAsync(int index, FileSystemDirectory item, CancellationToken cancellationToken)
        {
            return (Collections[0] as IDataCollection<FileSystemDirectory>).AddAsync(item, cancellationToken);
        }
    }
}
