using System.Collections.Generic;
using System.Linq;
using Open.FileSystem;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FacebookAlbumViewModel : FileSystemDirectoryViewModel
    {
        #region ** initialization

        public FacebookAlbumViewModel() : base(null, null, null) { }

        public FacebookAlbumViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        #region ** object model

        public IEnumerable<FacebookPermissionViewModel> PermissionsList
        {
            get
            {
                return new List<FacebookPermissionViewModel>
                {
                    new FacebookPermissionViewModel { Title = FacebookResources.PublicLabel, Value = FacebookPermission.Public },
                    new FacebookPermissionViewModel { Title = FacebookResources.FriendsLabel, Value = FacebookPermission.Friends },
                    new FacebookPermissionViewModel { Title = FacebookResources.OnlyMeLabel, Value = FacebookPermission.OnlyMe },
                };
            }
        }

        public FacebookPermissionViewModel Permission
        {
            get
            {
                return PermissionsList.FirstOrDefault(p => p.Value == (Item as FacebookAlbum).Permission);
            }
            set
            {
                (Item as FacebookAlbum).Permission = value.Value;
            }
        }

        public override string Icon
        {
            get
            {
                var directory = Item as FileSystemDirectory;
                if (directory.Id == FacebookFileSystem.PhotosOfYou)
                {
                    return "PicturesOfMeIcon";
                }
                else if (directory.Id == FacebookFileSystem.YourPhotos)
                {
                    return "MyPicturesIcon";
                }
                else if (directory.Id == FacebookFileSystem.Albums)
                {
                    return "MyAlbumsIcon";
                }
                else if (directory.Id == FacebookFileSystem.Videos)
                {
                    return "MyVideosIcon";
                }
                return base.Icon;
            }
        }

        #endregion

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                var directory = Item as FileSystemDirectory;
                if (directory.IsSpecial)
                    return base.FormTemplate;
                return "FacebookAlbumFormTemplate";
            }
        }

        #endregion

        #region ** labels

        public string TitleLabel
        {
            get
            {
                return FacebookResources.TitleLabel;
            }
        }

        public string SecurityLabel
        {
            get
            {
                return FacebookResources.SecurityLabel;
            }
        }

        #endregion
    }

    public class FacebookPermissionViewModel : BaseViewModel
    {
        public string Title { get; set; }
        public FacebookPermission Value { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is FacebookPermissionViewModel)
                return this.Value == (obj as FacebookPermissionViewModel).Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

}
