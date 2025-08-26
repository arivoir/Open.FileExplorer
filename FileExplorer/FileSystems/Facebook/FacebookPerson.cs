using Open.Facebook;
using Open.FileSystem;

namespace Open.FileExplorer
{
    public class FacebookPerson : FileSystemPerson
    {
        public FacebookPerson(User user)
        {
            Id = user.Id;
            Name = user.Name;
            Thumbnail = string.Format("https://graph.facebook.com/{0}/picture?type=small", user.Id);
        }
    }
}
