using Open.FileSystemAsync;
using Open.Instagram;

namespace Open.FileExplorer.Instagram
{
    public class InstagramPerson : FileSystemPerson
    {
        public InstagramPerson(User user)
        {
            Id = user.Id;
            Name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
            Thumbnail = user.ProfilePicture;
        }
    }
}
