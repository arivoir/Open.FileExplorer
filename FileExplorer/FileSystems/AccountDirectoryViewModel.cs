
using Open.FileSystemAsync;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class AccountDirectoryViewModel : FileSystemDirectoryViewModel
    {
        #region initialization

        public AccountDirectoryViewModel() : base(null, null, null) { }

        public AccountDirectoryViewModel(FileExplorerViewModel fileExplorer, FileSystemItem item, string dirId)
            : base(fileExplorer, dirId, item)
        {
        }

        #endregion

        #region object model

        public string Color
        {
            get
            {
                var provider = (Item as AccountDirectory).Provider;
                if (provider != null)
                {
                    return provider.Color;
                }
                else
                {
                    return "#00000000";//Transparent
                }
            }
        }

        public override string Icon
        {
            get
            {
                var provider = (Item as AccountDirectory).Provider;
                if (provider != null)
                    return provider.IconResourceKey;
                else
                    return "WoopitiIcon";
            }
        }

        public long? UsedSize
        {
            get
            {
                var globalDir = Item as AccountDirectory;
                if (globalDir != null)
                    return globalDir.UsedSize;
                return null;
            }
        }

        public long? TotalSize
        {
            get
            {
                var globalDir = Item as AccountDirectory;
                if (globalDir != null)
                    return globalDir.TotalSize;
                return null;
            }
        }

        public long? AvailableSize
        {
            get
            {
                var globalDir = Item as AccountDirectory;
                if (globalDir != null)
                    return globalDir.AvailableSize;
                return null;
            }
        }

        public override bool SizeVisible
        {
            get
            {
                return !SizeDetailsVisible && base.SizeVisible;
            }
        }

        public bool SizeDetailsVisible
        {
            get
            {
                return UsedSize.HasValue && TotalSize.HasValue;
            }
        }


        public string UsedSizeText
        {
            get
            {
                return string.Format(SettingsResources.UsedSpaceMessage, UsedSize.HasValue ? FileExplorerViewModel.ToSizeString(UsedSize.Value) : "");
            }
        }

        public string TotalSizeText
        {
            get
            {
                return TotalSize.HasValue ? FileExplorerViewModel.ToSizeString(TotalSize.Value) : "";
            }
        }

        public string AvailableSizeText
        {
            get
            {
                return AvailableSize.HasValue ? FileExplorerViewModel.ToSizeString(AvailableSize.Value) : "";
            }
        }
        #endregion

        #region templates

        public override string ItemTemplate
        {
            get
            {
                return "ProviderTemplate";
            }
        }

        public override string ListItemTemplate
        {
            get
            {
                return "ProviderListTemplate";
            }
        }

        public override string SmallItemTemplate
        {
            get
            {
                return "SmallProviderItemTemplate";
            }
        }

        public override string FormTemplate
        {
            get
            {
                return "AccountDirectoryFormTemplate";
            }
        }

        #endregion

    }
}
