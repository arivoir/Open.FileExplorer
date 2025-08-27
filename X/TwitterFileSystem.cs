using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using Open.Twitter;
using System.Globalization;

namespace Open.FileExplorer.X
{
    public class TwitterFileSystem : AuthenticatedFileSystem, ISocialExtension
    {
        #region fields

        public static string ConsumerKey { get; set; }
        public static string ConsumerToken { get; set; }
        public static string RedirectUri { get; private set; }

        private User _user;

        #endregion

        #region initialization

        static TwitterFileSystem()
        {
            ConsumerKey = ConfigurationManager.AppSettings["TwitterConsumerKey"];
            ConsumerToken = ConfigurationManager.AppSettings["TwitterConsumerSecret"];
            RedirectUri = ConfigurationManager.AppSettings["TwitterRedirectUri"];
        }

        #endregion

        #region authentication

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            _user = ticket.Tag as User;
            return ticket;
        }

        private new async Task<string[]> GetAccessTokenAsync(bool promptForUserInteraction, CancellationToken cancellationToken)
        {
            var ticket = await AuthenticateAsync(null, promptForUserInteraction, cancellationToken);
            var credentials = ticket.AuthToken.Split('&');
            return credentials;
        }

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var requestToken = await TwitterClient.GetRequestTokenAsync(TwitterFileSystem.ConsumerKey, TwitterFileSystem.ConsumerToken, RedirectUri);
            if (requestToken.CallbackConfirmed)
            {
                var _authenticationUrl = new Uri(TwitterClient.GetAuthorizeUrl(TwitterFileSystem.ConsumerKey, TwitterFileSystem.ConsumerToken, requestToken.Token), UriKind.Absolute);
                var result = await authenticationBroker.WebAuthenticationBrokerAsync(_authenticationUrl, new Uri(RedirectUri));
                var fragments = UriEx.ProcessFragments(result.Query);
                //if (fragments.ContainsKey("oauth_token") && fragments.ContainsKey("oauth_verifier"))
                //{
                //    var token = await TwitterClient.GetAccessTokenAsync(FlickrFileSystem.ConsumerToken, FlickrFileSystem.ConsumerTokenSecret, fragments["oauth_token"], requestToken.TokenSecret, fragments["oauth_verifier"]);
                //    var client = new TwitterClient(ConsumerKey, ConsumerToken, token.Token, token.TokenSecret);
                //    var user = await client.GetUserAsync(CancellationToken.None);
                //    return new AuthenticatonTicket { AuthToken = token.Token + "&" + token.TokenSecret, Tag = user };
                //}
                //else
                {
                    throw new Exception("oauth_token or oauth_verifier not found.");
                }
            }
            else
            {
                throw new Exception("Request token callback not confirmed.");
            }
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var credentials = refreshToken.Split('&');
                var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
                var user = await client.GetUserAsync(cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = user };
            }
            catch (Exception exc)
            {
                throw ProcessException(exc);
            }
        }

        #endregion

        #region get info

        protected override DirPathMode DirPathMode
        {
            get
            {
                return DirPathMode.DirIdAsId;
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
            return "";
        }

        protected override bool CacheDirectoriesMetadata(string dirId)
        {
            return false;
        }

        protected override Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IDataCollection<FileSystemFile>>(new CursorList<FileSystemFile>(
                async (pageSize, after, sd, fe, ct) =>
                {
                    try
                    {
                        string[] credentials = await GetAccessTokenAsync(true, ct);
                        var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
                        var tweets = await client.GetUserTimelineAsync(pageSize, !string.IsNullOrWhiteSpace(after) ? long.Parse(after) : (long?)null, ct);
                        var nextMaxId = tweets.Count() > 0 ? (tweets.Min(t => t.Id) - 1).ToString() : null;
                        var twitterFiles = ProcessItemsList(tweets);
                        return new Tuple<string, IReadOnlyList<FileSystemFile>>(nextMaxId, twitterFiles);
                    }
                    catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
                }));
        }

        private IReadOnlyList<FileSystemFile> ProcessItemsList(IList<Tweet> items)
        {
            var res = new List<FileSystemFile>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item.Entities != null && item.Entities.Media != null)
                    {
                        var i = ConvertItem(item);
                        res.Add(i);
                    }
                }
            }
            return res;
        }

        private TwitterFile ConvertItem(Tweet item)
        {
            var f = new TwitterFile(item);
            return f;
        }

        #endregion

        #region upload

        protected override string[] GetAcceptedFileTypesOverride(string dirId, bool includeSubDirectories)
        {
            return new string[] { "image/jpeg", "image/png", "image/gif", "image/tiff" };
        }

        protected override Task<bool> CanWriteFileAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> WriteFileAsyncOverride(string dirId, FileSystemFile file, Stream fileStream, IProgress<StreamProgress> progress, CancellationToken cancellationToken)
        {
            var albumId = dirId;
            string[] credentials = await GetAccessTokenAsync(true, cancellationToken);
            var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
            var photo = await client.AddTweetWithMediaAsync(file.Name, fileStream, null, progress, cancellationToken);
            return new TwitterFile(photo);
        }

        #endregion

        #region download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            string[] credentials = await GetAccessTokenAsync(true, cancellationToken);
            var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
            var file = await GetFileAsync(fileId, false, cancellationToken) as TwitterFile;
            return await client.DownloadFileAsync(file.Content, cancellationToken);
        }

        #endregion

        #region delete

        protected override Task<bool> CanDeleteFileOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<FileSystemFile> DeleteFileAsyncOverride(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            string[] credentials = await GetAccessTokenAsync(true, cancellationToken);
            var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
            await client.DeleteTweet(fileId, cancellationToken);
            return null;
        }

        #endregion

        #region search

        protected override Task<bool> CanSearchAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<IDataCollection<FileSystemSearchItem>> SearchAsyncOverride(string dirId, string query, CancellationToken cancellationToken)
        {
            string[] credentials = await GetAccessTokenAsync(true, cancellationToken);
            var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
            var result = await client.SearchAsync(string.Format("from:@{0} {1}", _user.ScreenName, query), cancellationToken: cancellationToken);
            var searchResult = new List<FileSystemSearchItem>();
            if (result.Statuses != null)
            {
                foreach (var item in result.Statuses)
                {
                    if (item.Entities != null &&
                        item.Entities.Media != null &&
                        item.Entities.Media.Count() > 0)
                    {
                        var f = ConvertItem(item);
                        searchResult.Add(new FileSystemSearchItem { DirectoryId = "", Item = f });
                    }
                }
            }
            return searchResult.AsDataCollection();
        }

        #endregion

        #region social extension

        public event EventHandler CommentsChanged;

        public Task<FileSystemPerson> GetCurrentUserAsync(string dirId)
        {
            return Task.FromResult<FileSystemPerson>(new TwitterPerson(_user));
        }

        public bool CanAddComment(string path)
        {
            return true;
        }

        public async Task<IDataCollection<FileSystemComment>> GetCommentsAsync(string fileId)
        {
            try
            {
                string[] credentials = await GetAccessTokenAsync(true, CancellationToken.None);
                var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
                Tweet[] relatedTweets = null;
                relatedTweets = await client.GetMentionsAsync(fileId, cancellationToken: CancellationToken.None);
                relatedTweets = relatedTweets.Where(rt => rt.InReplyToStatusIdStr == fileId).ToArray();
                var comments = new List<FileSystemComment>();
                foreach (var mention in relatedTweets)
                {
                    var comment = new FileSystemComment();
                    comment.Id = mention.IdStr;
                    comment.From = new TwitterPerson(mention.User);
                    comment.Message = mention.Text;
                    DateTime createdDate;
                    if (DateTime.TryParseExact(mention.CreatedAt, "ddd MMM dd HH:mm:ss zz00 yyyy", CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.None, out createdDate))
                    {
                        comment.CreatedTime = createdDate;
                    }
                    comments.Add(comment);
                }
                return comments.AsDataCollection();
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        public async Task AddCommentAsync(string fileId, string message)
        {
            string[] credentials = await GetAccessTokenAsync(true, CancellationToken.None);
            var client = new TwitterClient(ConsumerKey, ConsumerToken, credentials[0], credentials[1]);
            var tweet = await client.AddTweetAsync(message, fileId);
            RaiseCommentsChanged();
        }

        private void RaiseCommentsChanged()
        {
            if (CommentsChanged != null)
                CommentsChanged(this, new EventArgs());
        }

        public bool CanThumbUp(string fileId)
        {
            return false;
        }

        public Task AddThumbUp(string fileId)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task RemoveThumbUp(string fileId)
        {
            return Task.FromException<bool>(new NotImplementedException());
        }

        public Task<IDataCollection<FileSystemPerson>> GetThumbsUpAsync(string path)
        {
            return Task.FromException<IDataCollection<FileSystemPerson>>(new NotImplementedException());
        }

        #endregion

        #region implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        private static Exception ProcessException(Exception exc)
        {
            var twitterExc = exc as TwitterException;
            if (twitterExc != null)
            {
                if (twitterExc.Errors != null && twitterExc.Errors.Errors != null)
                {
                    var error = twitterExc.Errors.Errors.FirstOrDefault();
                    if (error != null && error.Code == 89)
                    {
                        return new AccessDeniedException();
                    }
                }
            }
            return exc;
        }

        #endregion
    }
}
