using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public interface IAppSettings
    {
        string AppVersion { get; }
        UserSettings Settings { get; }
        Task<bool> IsNetworkAvailableAsync();

        //Task ClearCache();
        //Task<long> GetTotalUsedSizeAsync();

        bool SupportsChangingLanguage { get; }
        IEnumerable<CultureInfo> GetSupportedLanguages();
        CultureInfo GetCurrentLanguage();
        void SetCurrentLanguage(CultureInfo culture);
    }

    public abstract class UserSettings : BaseViewModel
    {
        public abstract bool IsOnline { get; set; }
        public abstract bool ShowPhotoLabels { get; set; }
        public abstract bool TransparentStartTiles { get; set; }
        public abstract int ExecutedTimes { get; set; }
        public abstract bool PromptRate { get; set; }
        public abstract int Passcode { get; set; }
        public abstract bool RequiresAuthentication { get; set; }
    }
}
