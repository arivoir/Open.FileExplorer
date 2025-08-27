using System;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class FileSystemCurrency : IFileSystemCurrency
    {
        #region fields

        private string _currentDirId = null;
        private HardStack<string> _backwardDirectories = new HardStack<string>();
        private HardStack<string> _forwardDirectories = new HardStack<string>();

        #endregion

        #region initialization

        public FileSystemCurrency(FileExplorerViewModel explorer)
        {
            FileExplorer = explorer;
        }

        #endregion

        #region object model

        public FileExplorerViewModel FileExplorer { get; private set; }

        public event EventHandler<CurrentDirectoryChangingEventArgs> CurrentDirectoryChanging;
        public event EventHandler<CurrentDirectoryChangedEventArgs> CurrentDirectoryChanged;
        public event EventHandler HistoryChanged;

        public int HistoryStackSize
        {
            get
            {
                return _backwardDirectories.Maximum;
            }
            set
            {
                _backwardDirectories.Maximum = value;
            }
        }

        #endregion

        #region current directory

        public virtual string GetCurrentDirectory()
        {
            return _currentDirId;
        }

        public virtual async Task<string> SetCurrentDirectoryAsync(string dirId, bool keepHistory = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (dirId == null)
                throw new ArgumentNullException("dirId can not be null.");

            if (await FileExplorer.ExistsDirectoryAsync(dirId, cancellationToken))
            {
                await SetCurrentDirectoryOverride(dirId, keepHistory);
                return dirId;
            }
            return null;
        }

        private async Task SetCurrentDirectoryOverride(string dirId, bool keepHistory)
        {
            if (_currentDirId != dirId)
            {
                await RaiseCurrentDirectoryChanging(_currentDirId, dirId);
                if (keepHistory)
                {
                    _backwardDirectories.Push(_currentDirId);
                    _forwardDirectories.Clear();
                }
                var previous = _currentDirId;
                _currentDirId = dirId;
                await RaiseCurrentDirectoryChanged(previous, _currentDirId);
                if (keepHistory)
                {
                    RaiseHistoryChanged();
                }
            }
        }

        public virtual string[] GetBackwardPaths()
        {
            return _backwardDirectories.ToArray();
        }

        public virtual string[] GetForwardPaths()
        {
            return _forwardDirectories.ToArray();
        }

        public virtual async void Back(int count)
        {
            if (count < 0)
            {
                Forward(-count);
            }
            else if (count > 0)
            {
                if (_backwardDirectories.Count > 0)
                {
                    var previousDirId = _currentDirId;
                    int i = 0;
                    while (i < count)
                    {
                        var previous = _backwardDirectories.Pop();
                        if (await FileExplorer.ExistsDirectoryAsync(previous, CancellationToken.None))
                        {
                            _forwardDirectories.Push(_currentDirId);
                            _currentDirId = previous;
                            i++;
                        }
                    }
                    await RaiseCurrentDirectoryChanged(previousDirId, _currentDirId);
                    RaiseHistoryChanged();
                }
            }
        }

        public virtual async void Forward(int count)
        {
            if (count < 0)
            {
                Back(-count);
            }
            else if (count > 0)
            {
                if (_forwardDirectories.Count > 0)
                {
                    var previousDirId = _currentDirId;
                    for (int i = 0; i < count; i++)
                    {
                        var next = _forwardDirectories.Pop();
                        _backwardDirectories.Push(_currentDirId);
                        _currentDirId = next;
                    }
                    await RaiseCurrentDirectoryChanged(previousDirId, _currentDirId);
                    RaiseHistoryChanged();
                }
            }
        }

        public void ClearHistory()
        {
            _backwardDirectories.Clear();
            _forwardDirectories.Clear();
            RaiseHistoryChanged();
        }

        protected async Task RaiseCurrentDirectoryChanging(string currentDirId, string nextDirId)
        {
            if (CurrentDirectoryChanging != null)
            {
                var args = new CurrentDirectoryChangingEventArgs(currentDirId, nextDirId);
                CurrentDirectoryChanging(this, args);
                await args.WaitDeferralsAsync();
                if (args.Cancel)
                    throw new OperationCanceledException();
            }
        }

        protected async Task RaiseCurrentDirectoryChanged(string previousDirId, string currentDirId)
        {
            if (CurrentDirectoryChanged != null)
            {
                var args = new CurrentDirectoryChangedEventArgs(previousDirId, currentDirId);
                CurrentDirectoryChanged(this, args);
                await args.WaitDeferralsAsync();
            }
        }

        protected void RaiseHistoryChanged()
        {
            if (HistoryChanged != null)
            {
                HistoryChanged(this, new EventArgs());
            }
        }

        #endregion
    }
}
