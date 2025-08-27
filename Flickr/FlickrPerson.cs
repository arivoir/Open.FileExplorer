using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class FlickrPerson : FileSystemPerson
    {
        public FlickrPerson(int iconFarm, string iconServer, string userId, string userName)
        {
            Id = userId;
            Name = userName;
            Thumbnail = GetUserProfileUrl(iconFarm, iconServer, userId);
        }

        private static string GetUserProfileUrl(int iconFarm, string iconServer, string userId)
        {
            return iconFarm > 0 ? string.Format("http://farm{0}.staticflickr.com/{1}/buddyicons/{2}.jpg", iconFarm, iconServer, userId) : "http://www.flickr.com/images/buddyicon.jpg";
        }
    }
}
