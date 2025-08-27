using Open.Box;
using Open.FileSystemAsync;

namespace Open.FileExplorer.Box
{
    public class BoxPerson : FileSystemPerson
    {
        public BoxPerson(User user)
        {
            Id = user.Id;
            Name = user.Name;
            Thumbnail = string.Format("https://www.box.com/api/avatar/large/{0}", user.Id);
        }
    }
}
