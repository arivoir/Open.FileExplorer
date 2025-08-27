using Open.FileSystemAsync;
using Open.GoogleDrive;

namespace Open.FileExplorer.GoogleDrive
{
    public class GoogleDrivePerson : FileSystemPerson
    {
        public GoogleDrivePerson(User user)
        {
            Id = user.PermissionId;
            Name = user.DisplayName;
            IsAuthenticatedUser = user.IsAuthenticatedUser;
            if (user.Picture != null)
                Thumbnail = user.Picture.Url;
            //https://lh4.googleusercontent.com/-FHD2BXXZ0s4/AAAAAAAAAAI/AAAAAAAAAAA/yabhCnvJX1M/s27-c/photo.jpg
        }
    }
}
