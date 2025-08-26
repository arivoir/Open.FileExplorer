using Open.FileSystemAsync;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public interface IFileSystemCurrency
    {
        /***********Current***********/
        Task<string> SetCurrentDirectoryAsync(string path, bool keepHistory = true, CancellationToken cancellationToken = default(CancellationToken));
        string GetCurrentDirectory();
        event EventHandler<CurrentDirectoryChangingEventArgs> CurrentDirectoryChanging;
        event EventHandler<CurrentDirectoryChangedEventArgs> CurrentDirectoryChanged;

        /***********History***********/
        int HistoryStackSize { get; set; }
        string[] GetBackwardPaths();
        string[] GetForwardPaths();
        void Back(int count);
        void Forward(int count);
        event EventHandler HistoryChanged;
        void ClearHistory();
    }

    public class CurrentDirectoryChangingEventArgs : AsyncEventArgs
    {
        public CurrentDirectoryChangingEventArgs(string currentDirId, string nextDirId)
        {
            CurrentDirId = currentDirId;
            NextDirId = nextDirId;
        }
        public string CurrentDirId { get; private set; }
        public string NextDirId { get; private set; }
        public bool Cancel { get; set; }
    }

    public class CurrentDirectoryChangedEventArgs : AsyncEventArgs
    {
        public CurrentDirectoryChangedEventArgs(string previousDirId, string currentDirId)
        {
            PreviousDirId = previousDirId;
            CurrentDirId = currentDirId;
        }
        public string PreviousDirId { get; private set; }
        public string CurrentDirId { get; private set; }
    }
}
