using Open.FileSystemAsync;
using Open.Twitter;

namespace Open.FileExplorer
{
    public class TwitterPerson : FileSystemPerson
    {
        public TwitterPerson(User user)
        {
            Id = user.IdStr;
            Name = user.Name;
            ScreenName = user.ScreenName;
            Thumbnail = user.ProfileImageUrl;
        }

        public string ScreenName { get; private set; }
    }
}
