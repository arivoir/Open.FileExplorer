using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FlickrPhotoViewModel : FileSystemFileViewModel
    {
        #region ** initialization

        public FlickrPhotoViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        #region ** object model

        protected override bool NameIsRequired
        {
            get
            {
                return false;
            }
        }

        //public IEnumerable<KeyValuePair<string, int>> SafetyLevels
        //{
        //    get
        //    {
        //        return new List<KeyValuePair<string, int>>
        //        {
        //            new KeyValuePair<string,int>(FlickrResources.SafeLabel, 1),
        //            new KeyValuePair<string,int>(FlickrResources.ModerateLabel, 2),
        //            new KeyValuePair<string,int>(FlickrResources.RestrictedLabel, 3),
        //        };
        //    }
        //}

        //public KeyValuePair<string, int> SafetyLevel
        //{
        //    get
        //    {
        //        return SafetyLevels.FirstOrDefault(pair => pair.Value == (Data as FlickrPhoto).SafetyLevel);
        //    }
        //    set
        //    {
        //        (Data as FlickrPhoto).SafetyLevel = value.Value;
        //    }
        //}

        public bool IsFriend
        {
            get
            {
                return (Item as FlickrPhoto).IsFriend;
            }
            set
            {
                (Item as FlickrPhoto).IsFriend = value;
            }
        }

        public bool IsFamily
        {
            get
            {
                return (Item as FlickrPhoto).IsFamily;
            }
            set
            {
                (Item as FlickrPhoto).IsFamily = value;
            }
        }

        public bool IsPublic
        {
            get
            {
                return (Item as FlickrPhoto).IsPublic;
            }
            set
            {
                (Item as FlickrPhoto).IsPublic = value;
            }
        }

        public bool IsPrivate
        {
            get
            {
                return !(Item as FlickrPhoto).IsPublic;
            }
            set
            {
                (Item as FlickrPhoto).IsPublic = !value;
            }
        }

        #endregion

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                return "FlickrPhotoFormTemplate";
            }
        }

        public override string NewFormTemplate
        {
            get
            {
                return "FlickrNewPhotoFormTemplate";
            }
        }

        #endregion

        #region ** labels

        public string TitleLabel
        {
            get
            {
                return FlickrResources.TitleLabel;
            }
        }

        public string DescriptionLabel
        {
            get
            {
                return FlickrResources.DescriptionLabel;
            }
        }

        public string IsPublicLabel
        {
            get
            {
                return FlickrResources.IsPublicLabel;
            }
        }

        public string IsPrivateLabel
        {
            get
            {
                return FlickrResources.IsPrivateLabel;
            }
        }

        public string FriendsLabel
        {
            get
            {
                return FlickrResources.FriendsLabel;
            }
        }

        public string FamilyLabel
        {
            get
            {
                return FlickrResources.FamilyLabel;
            }
        }

        //public string HiddenLabel
        //{
        //    get
        //    {
        //        return FlickrResources.HiddenLabel;
        //    }
        //}

        #endregion
    }
}
