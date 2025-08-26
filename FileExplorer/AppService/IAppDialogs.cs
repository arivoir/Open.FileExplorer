using Open.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IAppDialogs
    {
        Task ShowErrorAsync(string message, string caption = null);
        Task<bool> ShowQuestionAsync(string message, string caption = null);
        Task<bool> ShowItemFormAsync(FileSystemItemViewModel itemViewModel,
            string caption,
            string positiveButton = null,
            string negativeButton = null,
            object placementTarget = null);
        Task<bool> ShowItemsFormAsync(IList<FileSystemItemViewModel> itemViewModels,
            string caption,
            string positiveButton = null,
            string negativeButton = null,
            object placementTarget = null);
        Task<int> ShowSelectAsync(string title, IList<string> options, object placementTarget = null);

        Task<bool> RequestNotificationsAuthorization();
        void Notify(string message, Dictionary<string, string> parameters, object placementTarget = null);
        void NotifyError(string message, object placementTarget = null);

        /*************** File dialogs ***************/

        bool CanSaveFile(string contentType);
        bool CanPickFiles(IEnumerable<string> contentTypes);

        Task SaveFileAsync(string suggestedFileName,
            string contentType,
            Stream stream,
            string defaultExtension,
            IDictionary<string, IList<string>> fileTypeChoices,
            IProgress<StreamProgress> progress, CancellationToken cancellationToken);
        Task<IEnumerable<IFileInfo>> PickFilesAsync(bool multiSelect = false, IEnumerable<string> contentTypes = null);
        Task<string> PickFolderToCopyAsync(string suggestedStartLocation, FileSystemActionContext context, object placementTarget = null);
        Task<string> PickFolderToMoveAsync(string suggestedStartLocation, FileSystemActionContext context, object placementTarget = null);
        Task<string> PickFolderToUploadAsync(string suggestedStartLocation, IEnumerable<string> contentTypes, object placementTarget = null);
        bool CanPickFolderToDownload();
        Task<string> PickFolderToDownloadAsync(IEnumerable<string> contentTypes, object placementTarget = null);
    }

    public enum PickFolderMode
    {
        Copy,
        Move,
        Upload,
        Download,
    }
}
