using Open.FileExplorer.Strings;
using Open.FileSystemAsync;

namespace Open.FileExplorer.YouTube
{
    public class YouTubePlaylistViewModel : FileSystemDirectoryViewModel
    {
        #region fields

        private string _oldSummary;

        #endregion

        #region initialization

        public YouTubePlaylistViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        #region object model

        public string Description
        {
            get
            {
                return (Item as YoutubePlaylist).Description;
            }
            set
            {
                (Item as YoutubePlaylist).Description = value;
            }
        }

        public string Tags
        {
            get
            {
                var tags = (Item as YoutubePlaylist).Tags;
                return tags != null ? string.Join(",", tags) : "";
            }
            set
            {
                (Item as YoutubePlaylist).Tags = value.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public IEnumerable<YoutubePrivacyViewModel> PrivacyList
        {
            get
            {
                return new List<YoutubePrivacyViewModel>
                {
                    new YoutubePrivacyViewModel { Title = Strings.YouTubeResources.PublicLabel, Value = "public" },
                    new YoutubePrivacyViewModel { Title = Strings.YouTubeResources.UnlistedLabel, Value = "unlisted" },
                    new YoutubePrivacyViewModel { Title = Strings.YouTubeResources.PrivateLabel, Value = "private" },
                };
            }
        }

        public YoutubePrivacyViewModel PrivacyStatus
        {
            get
            {
                return PrivacyList.FirstOrDefault(p => p.Value == (Item as YoutubePlaylist).PrivacyStatus);
            }
            set
            {
                (Item as YoutubePlaylist).PrivacyStatus = value.Value;
            }
        }


        #endregion

        #region templates

        public override string FormTemplate
        {
            get
            {
                return "YouTubePlaylistFormTemplate";
            }
        }

        #endregion

        #region labels

        public string TitleLabel
        {
            get
            {
                return Strings.YouTubeResources.TitleLabel;
            }
        }

        public string DescriptionLabel
        {
            get
            {
                return Strings.YouTubeResources.DescriptionLabel;
            }
        }

        public string TagsLabel
        {
            get
            {
                return Strings.YouTubeResources.TagsLabel;
            }
        }

        public string PrivacyLabel
        {
            get
            {
                return Strings.YouTubeResources.PrivacyLabel;
            }
        }

        #endregion

        #region versions

        public override bool HasChanges()
        {
            return base.HasChanges() ||
                _oldSummary != (Item as YoutubePlaylist).Description;
        }

        public override void BeginChanging()
        {
            base.BeginChanging();
            _oldSummary = (Item as YoutubePlaylist).Description;
        }

        public override void UndoChanges()
        {
            base.UndoChanges();
            (Item as YoutubePlaylist).Description = _oldSummary;
        }

        #endregion
    }

    public class YoutubePrivacyViewModel : BaseViewModel
    {
        public string Title { get; set; }
        public string Value { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is YoutubePrivacyViewModel)
                return this.Value == (obj as YoutubePrivacyViewModel).Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
}
