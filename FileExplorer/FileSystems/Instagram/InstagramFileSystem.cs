using C1.DataCollection;
using Open.FileSystemAsync;
using Open.Instagram;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class InstagramFileSystem : AuthenticatedFileSystem, ISocialExtension
    {
        #region fields

        public static string ClientId { get; private set; }
        public static string RedirectUri { get; private set; }
        private User _user;

        #endregion

        #region initialization

        static InstagramFileSystem()
        {
            ClientId = ConfigurationManager.AppSettings["InstagramKey"];
            RedirectUri = ConfigurationManager.AppSettings["InstagramRedirectUri"];
        }

        #endregion

        #region authentication

        protected async override Task<AuthenticatonTicket> AuthenticateAsync(IEnumerable<string> scopes = null, bool promptForUserInteraction = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            var ticket = await base.AuthenticateAsync(scopes, promptForUserInteraction, cancellationToken);
            _user = ticket.Tag as User;
            return ticket;
        }

        public override async Task<AuthenticatonTicket> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var client = new InstagramClient(refreshToken);
                var user = await client.GetUser(cancellationToken);
                return new AuthenticatonTicket { AuthToken = refreshToken, Tag = user, GrantedScopes = new string[] { "basic", "comments", "likes" } };

            }
            catch (Exception exc)
            {
                throw ProcessException(exc);
            }
        }

        public override string[] GetScopes(string dirId)
        {
            return new string[] { "basic", "comments", "likes" };
        }

        public override async Task<AuthenticatonTicket> LogInAsync(IAuthenticationBroker authenticationBroker, string connectionString, string[] scopes, bool requestingDeniedScope, CancellationToken cancellationToken)
        {
            var scopesString = string.Join(" ", scopes);
            var authenticationUrl = new Uri(InstagramClient.GetRequestUrl(InstagramFileSystem.ClientId, scopesString, RedirectUri), UriKind.Absolute);
            var result = await authenticationBroker.WebAuthenticationBrokerAsync(authenticationUrl,
                new Uri(RedirectUri));

            var fragments = UriEx.ProcessFragments(result.Fragment);
            string accessToken;
            if (fragments.TryGetValue("access_token", out accessToken))
            {
                var client = new InstagramClient(accessToken);
                var user = await client.GetUser(CancellationToken.None);
                return new AuthenticatonTicket { AuthToken = accessToken, Tag = user, GrantedScopes = new string[] { "basic", "comments", "likes" } };
            }
            else
            {
                throw new Exception("access_token not found.");
            }
        }

        #endregion

        #region object model

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
                async (pageSize, maxId, sd, fe, ct) =>
                {
                    var client = new InstagramClient(await GetAccessTokenAsync(true, ct));
                    var result = await client.GetMediaAsync(maxId: maxId, cancellationToken: ct);
                    return new Tuple<string, IReadOnlyList<FileSystemFile>>(result.Pagination != null ? result.Pagination.NextMaxId : null, ProcessItemsList(result.Data).Cast<FileSystemFile>().ToList());
                }));
        }

        private List<FileSystemItem> ProcessItemsList(IList<Item> items)
        {
            var res = new List<FileSystemItem>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    var i = ConvertItem(item);
                    res.Add(i);
                }
            }
            return res;
        }

        private FileSystemItem ConvertItem(Item item)
        {
            var f = new InstagramFile(item);
            return f;
        }

        #endregion

        #region download

        protected override Task<bool> CanOpenFileAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task<Stream> OpenFileReadAsyncOverride(string fileId, CancellationToken cancellationToken)
        {
            var client = new InstagramClient(await GetAccessTokenAsync(true, cancellationToken));
            var file = await GetFileAsync(fileId, false, cancellationToken) as InstagramFile;
            return await client.DownloadFileAsync(file.Content, cancellationToken);
        }

        #endregion

        #region social extension

        public event EventHandler CommentsChanged;

        public Task<FileSystemPerson> GetCurrentUserAsync(string dirId)
        {
            return Task.FromException<FileSystemPerson>(new NotImplementedException());
        }

        public bool CanAddComment(string fileId)
        {
            return false;
        }

        public async Task<IDataCollection<FileSystemComment>> GetCommentsAsync(string fileId)
        {
            try
            {
                var client = new InstagramClient(await GetAccessTokenAsync(true, CancellationToken.None));
                var result = await client.GetCommentsAsync(fileId, CancellationToken.None);
                var comments = new List<FileSystemComment>();
                foreach (var commentData in result)
                {
                    var comment = new FileSystemComment();
                    comment.Id = commentData.Id;
                    comment.From = new InstagramPerson(commentData.From);
                    comment.Message = commentData.Text;
                    comment.CreatedTime = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(long.Parse(commentData.CreatedTime));
                    comments.Add(comment);
                }
                return comments.AsDataCollection();
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        public async Task AddCommentAsync(string fileId, string message)
        {
            var client = new InstagramClient(await GetAccessTokenAsync(true, CancellationToken.None));
            await client.AddCommentAsync(fileId, message, CancellationToken.None);
            RaiseCommentsChanged();
        }

        private void RaiseCommentsChanged()
        {
            if (CommentsChanged != null)
                CommentsChanged(this, new EventArgs());
        }

        public bool CanThumbUp(string fileId)
        {
            return true;
        }

        public async Task AddThumbUp(string fileId)
        {
            var client = new InstagramClient(await GetAccessTokenAsync(true, CancellationToken.None));
            await client.AddLikeAsync(fileId, CancellationToken.None);
        }

        public async Task RemoveThumbUp(string fileId)
        {
            var client = new InstagramClient(await GetAccessTokenAsync(true, CancellationToken.None));
            await client.RemoveLikeAsync(fileId, CancellationToken.None);
        }

        public async Task<IDataCollection<FileSystemPerson>> GetThumbsUpAsync(string fileId)
        {
            var client = new InstagramClient(await GetAccessTokenAsync(true, CancellationToken.None));
            var result = await client.GetLikesAsync(fileId, CancellationToken.None);
            var people = new List<FileSystemPerson>();
            foreach (var likeData in result)
            {
                var person = new FileSystemPerson
                {
                    Id = likeData.Id,
                    Name = string.IsNullOrWhiteSpace(likeData.FullName) ? likeData.UserName : likeData.FullName,
                    IsAuthenticatedUser = (likeData.Id == _user.Id)
                };
                people.Add(person);
            }
            return people.AsDataCollection();
        }

        #endregion

        #region implementation

        protected override Task<Exception> ProcessExceptionAsync(Exception exc)
        {
            return Task.FromResult(ProcessException(exc));
        }

        private static Exception ProcessException(Exception exc)
        {
            var instExc = exc as InstagramException;
            if (instExc != null)
            {
                if (instExc.ErrorType == "OAuthAccessTokenException" ||
                    instExc.ErrorType == "OAuthAccessTokenError")
                {
                    return new AccessDeniedException();
                }
            }
            return exc;
        }

        #endregion
    }
}
