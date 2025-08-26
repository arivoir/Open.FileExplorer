using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class SettingsViewModel : BaseViewModel
    {
        #region ** fields

        protected TaskCommand _clearCacheCommand;
        private List<LanguageViewModel> _languages;

        private bool? _isOnline;
        private bool _isOnlineEnabled = true;
        private bool? _showPhotoLabels;
        private bool? _transparentStartTiles;
        private bool _transparentStartTilesEnabled = true;
        private static bool? _isPasscodeRequired;
        private Task<long> _totalSpaceTask = null;

        #endregion

        #region ** initialization

        public SettingsViewModel(FileExplorerViewModel fileExplorer)
        {
            FileExplorer = fileExplorer;
            _clearCacheCommand = new TaskCommand(ClearCache);
        }

        #endregion

        #region ** object model

        public FileExplorerViewModel FileExplorer { get; private set; }

        public static IAppService AppService { get; set; }

        public bool IsOnlineEnabled
        {
            get
            {
                return _isOnlineEnabled;
            }
        }

        public bool IsOnline
        {
            get
            {
                if (!_isOnline.HasValue)
                {
                    _isOnline = AppService.Settings.IsOnline;
                }
                return _isOnline ?? false;
            }
            set
            {
                try
                {
                    _isOnlineEnabled = false;
                    OnPropertyChanged("IsOnlineEnabled");
                    SetIsOnlineAsync(value).ContinueWith(t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            _isOnline = value;
                            OnPropertyChanged("IsOnline");
                        }
                        _isOnlineEnabled = true;
                        OnPropertyChanged("IsOnlineEnabled");
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                catch
                {
                    _isOnlineEnabled = true;
                    OnPropertyChanged("IsOnlineEnabled");
                }
            }
        }

        private async Task SetIsOnlineAsync(bool value)
        {
            AppService.Settings.IsOnline = value;
            await FileExplorer.SetIsOnlineAsync(value);
        }

        public bool ShowPhotoLabels
        {
            get
            {
                if (!_showPhotoLabels.HasValue)
                {
                    _showPhotoLabels = AppService.Settings.ShowPhotoLabels;
                }
                return _showPhotoLabels ?? false;
            }
            set
            {
                AppService.Settings.ShowPhotoLabels = value;
                _showPhotoLabels = value;
                OnPropertyChanged();
            }
        }

        public bool TransparentStartTilesEnabled
        {
            get
            {
                return _transparentStartTilesEnabled;
            }
        }

        public bool TransparentStartTiles
        {
            get
            {
                if (!_transparentStartTiles.HasValue)
                {
                    _transparentStartTiles = AppService.Settings.TransparentStartTiles;
                }
                return _transparentStartTiles ?? false;
            }
            set
            {
                try
                {
                    _transparentStartTilesEnabled = false;
                    OnPropertyChanged("TransparentStartTilesEnabled");
                    SetTransparentStartTiles(value).ContinueWith(t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            _transparentStartTiles = value;
                            _transparentStartTilesEnabled = true;
                            OnPropertyChanged("TransparentStartTilesEnabled");
                            OnPropertyChanged("ShowPhotoLabels");
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                catch
                {
                    _transparentStartTilesEnabled = true;
                    OnPropertyChanged("TransparentStartTilesEnabled");
                }
            }
        }

        private async Task SetTransparentStartTiles(bool value)
        {
            AppService.Settings.TransparentStartTiles = value;
            foreach (var tile in await AppService.GetTiles())
            {
                await AppService.UpdateTile(tile);
            }
        }

        #region ** passcode

        public static bool IsPasscodeRequired
        {
            get
            {
                if (!_isPasscodeRequired.HasValue)
                {
                    _isPasscodeRequired = AppService.Settings.Passcode != -1;
                }
                return _isPasscodeRequired ?? false;
            }
        }

        public string PasscodeStatus
        {
            get
            {
                return IsPasscodeRequired ? SettingsResources.ActiveLabel : SettingsResources.InactiveLabel;
            }
        }


        public static int StaticPasscode
        {
            get
            {
                return AppService.Settings.Passcode;
            }
            private set
            {
                _isPasscodeRequired = null;
                AppService.Settings.Passcode = value;
            }
        }

        public int Passcode
        {
            get
            {
                return StaticPasscode;
            }
            private set
            {
                StaticPasscode = value;
                OnPropertyChanged();
                OnPropertyChanged("IsPasscodeRequired");
                OnPropertyChanged("PasscodeStatus");
            }
        }

        private static bool _passcodeChecked = false;

        public static bool NeedEnterPasscode
        {
            get
            {
                return IsPasscodeRequired && !_passcodeChecked;
            }
        }

        public void InvalidatePasscode()
        {
            _passcodeChecked = false;
        }

        public static bool CheckPasscode(int passcode)
        {
            if (!IsPasscodeRequired || StaticPasscode == passcode)
            {
                _passcodeChecked = true;
                return true;
            }
            return false;
        }

        public bool SetPasscode(int passcode)
        {
            if (passcode >= 0 && passcode < 10000)
            {
                _passcodeChecked = true;
                Passcode = passcode;
                return true;
            }
            return false;
        }

        public bool RemovePasscode(int passcode)
        {
            if (CheckPasscode(passcode))
            {
                Passcode = -1;
                return true;
            }
            return false;
        }

        public bool RequiresAuthentication
        {
            get
            {
                return AppService.Settings.RequiresAuthentication;
            }
            set
            {
                AppService.Settings.RequiresAuthentication = value;
            }
        }

        #endregion

        public long? TotalSpace
        {
            get
            {
                if (_totalSpaceTask == null)
                {
                    _totalSpaceTask = FileExplorer.GetTotalUsedSizeAsync();
                    OnPropertyChanged("UsedSpaceMessage");
                    _totalSpaceTask.ContinueWith(t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            OnPropertyChanged("TotalSpace");
                            OnPropertyChanged("UsedSpaceMessage");
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
                return _totalSpaceTask.Status == TaskStatus.RanToCompletion ? _totalSpaceTask.Result : (long?)null;
            }
        }

        public string UsedSpaceMessage
        {
            get
            {
                if (TotalSpace.HasValue)
                {
                    var totalUsedSpace = FileExplorerViewModel.ToSizeString(TotalSpace.Value);
                    return string.Format(SettingsResources.UsedSpaceMessage, totalUsedSpace);
                }
                else
                {
                    return SettingsResources.CalculatingMessage;
                }
            }
        }

        public bool LanguageVisible
        {
            get
            {
                return AppService.SupportsChangingLanguage;
            }
        }

        public List<LanguageViewModel> LanguagesList
        {
            get
            {
                if (_languages == null)
                {
                    _languages = new List<LanguageViewModel>();
                    foreach (var language in AppService.GetSupportedLanguages())
                    {
                        _languages.Add(new LanguageViewModel(language));

                    }
                }
                return _languages;
            }
        }

        public LanguageViewModel CurrentLanguage
        {
            get
            {
                var current = AppService.GetCurrentLanguage();
                if (current != null)
                {
                    return _languages.First(l => l.CultureInfo.TwoLetterISOLanguageName == current.TwoLetterISOLanguageName);
                }
                return _languages[0];
            }
            set
            {
                var culture = value.CultureInfo;
                var currentCulture = AppService.GetCurrentLanguage();
                if (currentCulture == null ||
                    culture.TwoLetterISOLanguageName != currentCulture.TwoLetterISOLanguageName)
                {
                    AppService.SetCurrentLanguage(culture);
                    OnPropertyChanged("SettingsLabel");
                    OnPropertyChanged("TransparentStartTilesLabel");
                    OnPropertyChanged("PhotoLabelsLabel");
                    OnPropertyChanged("PhotoLabelsMessage");
                    OnPropertyChanged("PasscodeLabel");
                    OnPropertyChanged("PasscodeMessage");
                    OnPropertyChanged("RequestCodeLabel");
                    OnPropertyChanged("ChangeCodeLabel");
                    OnPropertyChanged("ClearCacheLabel");
                    OnPropertyChanged("CacheMessage");
                    OnPropertyChanged("LanguageLabel");
                    OnPropertyChanged("LanguagesList");
                    OnPropertyChanged("UsedSpaceMessage");
                    OnPropertyChanged("GeneralLabel");
                    OnPropertyChanged("CacheLabel");
                    OnPropertyChanged("OnlineModeLabel");
                    OnPropertyChanged("CurrentLanguage");
                }
            }
        }

        #endregion

        #region ** labels

        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string SettingsLabel
        {
            get
            {
                return SettingsResources.SettingsLabel;
            }
        }

        public string TransparentStartTilesLabel
        {
            get
            {
                return SettingsResources.TransparentStartTilesLabel;
            }
        }

        public string PhotoLabelsLabel
        {
            get
            {
                return SettingsResources.PhotoLabelsLabel;
            }
        }

        public string PhotoLabelsMessage
        {
            get
            {
                return SettingsResources.PhotoLabelsMessage;
            }
        }

        public string PasscodeLabel
        {
            get
            {
                return SettingsResources.PasscodeLabel;
            }
        }

        public string PasscodeMessage
        {
            get
            {
                return SettingsResources.PasscodeMessage;
            }
        }

        public string RequestCodeLabel
        {
            get
            {
                return SettingsResources.RequestCodeLabel;
            }
        }

        public string ChangeCodeLabel
        {
            get
            {
                return SettingsResources.ChangeCodeLabel;
            }
        }

        public string ClearCacheLabel
        {
            get
            {
                return SettingsResources.ClearCacheLabel;
            }
        }

        public string CacheMessage
        {
            get
            {
                return SettingsResources.CacheMessage;
            }
        }

        public string LanguageLabel
        {
            get
            {
                return SettingsResources.LanguageLabel;
            }
        }

        public string OnlineModeLabel
        {
            get
            {
                return FileSystemResources.OnlineModeLabel;
            }
        }

        public string GeneralLabel
        {
            get
            {
                return SettingsResources.GeneralLabel;
            }
        }

        public string CacheLabel
        {
            get
            {
                return SettingsResources.CacheLabel;
            }
        }
        #endregion

        #region ** implementation


        public TaskCommand ClearCacheCommand
        {
            get
            {
                return _clearCacheCommand;
            }
        }

        protected async Task ClearCache(object param)
        {
            await FileExplorer.ClearCache();
            InvalidateTotalSize();
        }

        public void InvalidateTotalSize()
        {
            _totalSpaceTask = null;
            OnPropertyChanged("TotalSpace");
            OnPropertyChanged("UsedSpaceMessage");
        }

        #endregion
    }

    public class LanguageViewModel
    {
        public LanguageViewModel()
        {
        }

        public LanguageViewModel(CultureInfo cultureInfo)
        {
            CultureInfo = cultureInfo;
            Name = cultureInfo.NativeName;
        }

        public string Name { get; set; }
        public CultureInfo CultureInfo { get; set; }
    }
}
