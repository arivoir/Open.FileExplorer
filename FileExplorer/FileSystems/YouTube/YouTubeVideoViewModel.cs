using System;
using System.Collections.Generic;
using System.Linq;
using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class YouTubeVideoViewModel : FileSystemFileViewModel
    {
        #region ** fields

        private string _oldDescription;
        private string[] _oldTags;
        private string _oldPrivacyStatus;
        private bool _oldEmbeddable;

        #endregion

        #region ** initialization

        public YouTubeVideoViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        #region ** object model

        public string Description
        {
            get
            {
                return (Item as YouTubeVideo).Description;
            }
            set
            {
                (Item as YouTubeVideo).Description = value;
            }
        }

        public string Tags
        {
            get
            {
                var tags = (Item as YouTubeVideo).Tags;
                return tags != null ? string.Join(",", tags) : "";
            }
            set
            {
                (Item as YouTubeVideo).Tags = value.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public bool Embeddable
        {
            get
            {
                return (Item as YouTubeVideo).Embeddable;
            }
            set
            {
                (Item as YouTubeVideo).Embeddable = value;
            }
        }

        public bool PublicStatsViewable
        {
            get
            {
                return (Item as YouTubeVideo).PublicStatsViewable;
            }
            set
            {
                (Item as YouTubeVideo).PublicStatsViewable = value;
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
                return PrivacyList.FirstOrDefault(p => p.Value == (Item as YouTubeVideo).PrivacyStatus);
            }
            set
            {
                (Item as YouTubeVideo).PrivacyStatus = value.Value;
            }
        }

        #endregion

        #region ** templates

        public override bool IsVideo => true;

        public override string ItemTemplate
        {
            get
            {
                return "PhotoTemplate";
            }
        }
        public override string FormTemplate
        {
            get
            {
                return "YouTubeVideoFormTemplate";
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

        public string CategoryLabel
        {
            get
            {
                return YouTubeResources.CategoryLabel;
            }
        }

        public string TagsLabel
        {
            get
            {
                return YouTubeResources.TagsLabel;
            }
        }

        public string WhereLabel
        {
            get
            {
                return YouTubeResources.WhereLabel;
            }
        }

        public string EmbeddableLabel
        {
            get
            {
                return YouTubeResources.EmbeddableLabel;
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
                _oldTags != (Item as YouTubeVideo).Tags ||
                _oldDescription != (Item as YouTubeVideo).Description ||
                _oldEmbeddable != (Item as YouTubeVideo).Embeddable ||
                _oldPrivacyStatus != (Item as YouTubeVideo).PrivacyStatus;
        }

        public override void BeginChanging()
        {
            base.BeginChanging();
            _oldTags = (Item as YouTubeVideo).Tags;
            _oldDescription = (Item as YouTubeVideo).Description;
            _oldEmbeddable = (Item as YouTubeVideo).Embeddable;
            _oldPrivacyStatus = (Item as YouTubeVideo).PrivacyStatus;
        }

        public override void UndoChanges()
        {
            base.UndoChanges();
            (Item as YouTubeVideo).Tags = _oldTags;
            (Item as YouTubeVideo).Description = _oldDescription;
            (Item as YouTubeVideo).PrivacyStatus = _oldPrivacyStatus;
            (Item as YouTubeVideo).Embeddable = _oldEmbeddable;
        }

        #endregion
    }
}
