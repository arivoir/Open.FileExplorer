using Open.FileSystemAsync;

namespace Open.FileExplorer.OneDrive
{
    public class OneDrivePerson : FileSystemPerson
    {
        public OneDrivePerson(Open.OneDrive.Identity user)
        {
            Id = user.Id;
            Name = user.DisplayName;
        }
    }
}
