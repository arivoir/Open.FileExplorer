using System;
using System.Collections.Generic;
using System.Linq;
using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class YouTubePlaylistViewModel : FileSystemDirectoryViewModel
    {
        #region ** fields

        private string _oldSummary;

        #endregion

        #region ** initialization

        public YouTubePlaylistViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        #region ** object model

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
                    new YoutubePrivacyViewModel { Title = YouTubeResources.PublicLabel, Value = "public" },
                    new YoutubePrivacyViewModel { Title = YouTubeResources.UnlistedLabel, Value = "unlisted" },
                    new YoutubePrivacyViewModel { Title = YouTubeResources.PrivateLabel, Value = "private" },
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

        #region ** templates

        public override string FormTemplate
        {
            get
            {
                return "YouTubePlaylistFormTemplate";
            }
        }

        #endregion

        #region ** labels

        public string TitleLabel
        {
            get
            {
                return YouTubeResources.TitleLabel;
            }
        }

        public string DescriptionLabel
        {
            get
            {
                return YouTubeResources.DescriptionLabel;
            }
        }

        public string TagsLabel
        {
            get
            {
                return YouTubeResources.TagsLabel;
            }
        }

        public string PrivacyLabel
        {
            get
            {
                return YouTubeResources.PrivacyLabel;
            }
        }

        #endregion

        #region ** versions

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
