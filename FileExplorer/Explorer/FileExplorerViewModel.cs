//#define HOLD_MEMORY
using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class FileExplorerViewModel : BaseViewModel
    {
        #region fields

        private int _selectedCount;
        private FileSystemItemViewModel _lastSelectedItem;
        private FileSystemItemViewModel _lastTwoSelectedItem;
        private FileSystemItemViewModel _lastThreeSelectedItem;
        private TaskCommand _goBack;
        private Dictionary<string, Transaction> _dirTransactions = new Dictionary<string, Transaction>();
#if HOLD_MEMORY
        private Dictionary<string, Dictionary<string, FileSystemItemViewModel>> _fileViews = new Dictionary<string,Dictionary<string, FileSystemItemViewModel>>();
#else
        private Dictionary<string, WeakDictionary<string, FileSystemItemViewModel>> _fileViews = new Dictionary<string, WeakDictionary<string, FileSystemItemViewModel>>();
#endif

        private const string FilesDirectory = "files";
        private const string ThumbnailsDirectory = "thumbnails";
        private const string MetadataDirectory = "metadata";


        #endregion

        #region initialization

        public FileExplorerViewModel(IAppService appService, IFileSystemAsync fileSystem = null, IFileSystemCurrency currencyManager = null)
        {
            ShowFiles = true;
            AppService = appService;
            FileSystem = fileSystem ?? new GlobalFileSystem(AppService);
            Cache = new FileSystemCache(AppService.GetTemporaryStorage(), MetadataDirectory, FilesDirectory, ThumbnailsDirectory);
            TransactionManager = new TransactionManager();
            Extensions = new GlobalFileExplorerExtensions(this);
            CurrentDirectory = null;
            CurrencyManager = new FileSystemCurrency(this);
            CurrencyManager.HistoryStackSize = 3;
            //CurrencyManager.CurrentDirectoryChanging += OnCurrentDirectoryChanging;
            CurrencyManager.CurrentDirectoryChanged += OnCurrentDirectoryChanged;
            SelectionManager = new FileSystemSelectionManager();
            SelectionManager.SelectionChanged += OnSelectionChanged;
            SelectedItems = new ObservableCollection<FileSystemItemViewModel>();
            _goBack = new TaskCommand(GoBack, CanGoBack);
            TransactionManager.TransactionChanged += OnTransactionChanged;
            TransactionsViewModel = CreateTransactionsViewModel();
            StatusNotificationDelay = TimeSpan.FromMilliseconds(700);
            ThumbnailMargin = 2;
            ConnectionsViewModel = CreateConnectionsViewModel();
            SettingsViewModel.AppService = appService;
            Settings = new SettingsViewModel(this);
        }

        #endregion

        #region object model

        public TransactionManager TransactionManager { get; private set; }
        public TransactionsViewModel TransactionsViewModel { get; private set; }

        public IFileSystemAsync FileSystem { get; protected set; }

        public FileSystemCache Cache { get; private set; }

        public bool IsOnline { get; set; }

        public IFileExplorerExtensions Extensions { get; set; }

        public IFileSystemCurrency CurrencyManager { get; set; }

        public IAppService AppService { get; private set; }

        public string CurrentDirectory { get; set; }

        public FileSystemSelectionManager SelectionManager { get; set; }

        public ConnectionsViewModel ConnectionsViewModel { get; private set; }
        public SettingsViewModel Settings { get; private set; }

        public event EventHandler<ChangingDirectoryEventArgs> Entering;
        public event EventHandler<ChangingDirectoryEventArgs> Exiting;
        public event EventHandler<AsyncEventArgs> Loading;
        public event EventHandler<AsyncEventArgs> Unloading;
        public event EventHandler<AsyncEventArgs> SelectionChanged;
        public event EventHandler<ScrollItemIntoViewAsyncEventArgs> ScrollItemIntoView;
        public ObservableCollection<FileSystemItemViewModel> SelectedItems { get; set; }

        public string BackgroundTemplate
        {
            get
            {
                var currentDirectory = CurrencyManager.GetCurrentDirectory();
                return Extensions.GetBackgroundTemplateKey(CurrentDirectory).Result;
            }
        }

        public string Message { get; protected set; }
        public new Exception Error { get; set; }

        public bool ItemsVisible { get; set; }

        public bool ShowFiles { get; set; }
        public int ThumbnailSize { get; set; }
        public int ThumbnailMargin { get; set; }
        public double ScreenWidth { get; set; }
        public double ScreenHeight { get; set; }


        #endregion

        #region lifecycle

        public bool IsStarting { get; private set; }
        public bool IsStarted { get; private set; }

        public async Task StartAsync(string dirId, string fileId, CancellationToken cancellationToken)
        {
            try
            {
                if (!IsStarted)
                {
                    IsStarting = true;
                    await UpdateStatus();
                    if (AppService.Settings.IsOnline && !await AppService.IsNetworkAvailableAsync())
                    {
                        await AppService.ShowErrorAsync(ApplicationResources.OfflineModeMessage);
                        AppService.Settings.IsOnline = false;
                    }
                    await SetIsOnlineAsync(AppService.Settings.IsOnline);
                    FileSystem.Refreshed += OnFileSystemRefreshed;
                    await LoadAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(fileId))
                    {
                        dirId = await FileSystem.GetFileParentIdAsync(fileId, cancellationToken);
                    }
                    if (await FileSystem.CheckAccessAsync(dirId, true, cancellationToken))
                    {
                        await SetDirectoryAsync(dirId);
                    }
                    IsStarting = false;
                    IsStarted = true;
                    await RefreshAsync(dirId);
                    await UpdateStatus();
                }
                if (!string.IsNullOrWhiteSpace(fileId))
                {
                    await ScrollItemIntoViewAsync(fileId);
                }
            }
            catch (Exception exc)
            {
                Error = exc;
                await BeforeEnteringAsync();
                await OnEnteringAsync(null, dirId);
            }
        }

        public async Task<List<FileSystemDirectory>> LoadAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var directories = await FileSystem.GetDirectoriesAsync("", cancellationToken);
            if (directories.NeedsLoadAsync())
                await directories.LoadAsync();
            return directories.ToList();
        }

        public void Stop()
        {
            FileSystem.Refreshed -= OnFileSystemRefreshed;
            IsStarted = false;
        }

        public async Task StartUpAsync(List<AccountDirectory> accounts)
        {
            await (FileSystem as GlobalFileSystem).AddConnections(accounts);
        }

        private Task CurrentExiting { get; set; }
        private Task CurrentEntering { get; set; }

        private async void OnFileSystemRefreshed(object sender, RefreshedEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                await RefreshAsync(e.DirId);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async Task RefreshAsync(string dirId)
        {
            if (IsStarted)
            {
                SetRaiseNotifications(false);
                await OnUnloadingAsync();
                RemoveDirectoryInfo(dirId);
                RemoveDirectoryTransaction(dirId);
                Error = null;
                var retries = 1;
                beggining:
                try
                {
                    if (await FileSystem.CheckAccessAsync(dirId, true, CancellationToken.None))
                    {
                        SetItems(dirId, await GetItemsAsync(dirId));
                    }
                    else
                    {
                        Error = new Exception("Cannot access directory");
                    }
                }
                catch (AggregateException exc)
                {
                    if (exc.InnerExceptions.Any(ie => ie is AccessDeniedException))
                    {
                        if (retries > 0)
                        {
                            await FileSystem.InvalidateAccessAsync(dirId, CancellationToken.None);
                            retries--;
                            goto beggining;
                        }
                        Error = exc.InnerExceptions.First(ie => ie is AccessDeniedException);
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (AccessDeniedException exc)
                {
                    if (retries > 0)
                    {
                        await FileSystem.InvalidateAccessAsync(dirId, CancellationToken.None);
                        retries--;
                        goto beggining;
                    }
                    Error = exc;
                }
                catch (Exception exc)
                {
                    Error = exc;
                }
                await BeforeEnteringAsync();
                await OnLoadingAsync();
            }
        }

        private async void OnCurrentDirectoryChanged(object sender, CurrentDirectoryChangedEventArgs e)
        {
            if (!IsStarted)
            {
                CurrentDirectory = e.CurrentDirId;
                return;
            }
            var deferal = e.GetDeferral();
            try
            {
                _goBack.OnCanExecuteChanged();

                var oldDirectory = CurrentDirectory;
                var newDirectory = CurrencyManager.GetCurrentDirectory();
                CurrentDirectory = newDirectory;
                Error = null;
                try
                {
                    Task exitingTask = CurrentExiting;
                    Task enteringTask = CurrentEntering;
                    //waits for the entering animation
                    if (enteringTask != null)
                        await enteringTask;
                    //if no exiting animation executing creates a new one.
                    if (exitingTask == null)
                    {
                        exitingTask = OnExitingAsync(oldDirectory, newDirectory, null);
                        CurrentExiting = exitingTask;
                        //await exitingTask;
                    }
                    //waits for exiting and GetItemsAsync() in parallel.
                    var getFileViewCollectionTask = GetItemsAsync(CurrentDirectory);
                    await Task.WhenAll(exitingTask, getFileViewCollectionTask);
                    //If the directory wasn't changed while executing, sets the visual elements.
                    if (CurrencyManager.GetCurrentDirectory() != newDirectory)
                    {
                        return;
                    }
                    SetItems(CurrentDirectory, getFileViewCollectionTask.Result);
                }
                catch (Exception exc)
                {
                    Error = exc;
                    SetItems(CurrentDirectory, new EmptyCollectionView<FileSystemItem>());
                }
                if (CurrentExiting != null)
                    await CurrentExiting;
                CurrentExiting = null;
                await BeforeEnteringAsync();
                //execute the entering animation.
                CurrentEntering = OnEnteringAsync(oldDirectory, newDirectory);
                await CurrentEntering;
                CurrentEntering = null;
            }
            finally
            {
                deferal.Complete();
            }
        }

        protected virtual async Task BeforeEnteringAsync()
        {
            var oldMessage = Message;
            var oldItemsVisible = ItemsVisible;
            if (Error != null)
            {
                if (Error is CacheNotAvailableException || (Error is AggregateException && (Error as AggregateException).InnerException is CacheNotAvailableException))
                {
                    Message = Extensions.GetNotCachedDirectoryMessage(CurrentDirectory);
                }
                else
                {
                    Message = ApplicationResources.LoadingDataError;
                }
                ItemsVisible = false;
            }
            else
            {
                var itemsCount = Items.Count;
                ItemsVisible = itemsCount > 0;
                if (itemsCount == 0)
                {
                    Message = await GetEmptyDirectoryMessage();
                }
                else
                {
                    Message = "";
                }
            }
            if (oldMessage != Message)
                OnPropertyChanged("Message");
            if (oldItemsVisible != ItemsVisible)
                OnPropertyChanged("ItemsVisible");
        }

        #endregion

        #region populate items

        public FileViewCollection Items { get; private set; }

        protected void SetItems(string dirId, IDataCollection<FileSystemItem> items)
        {
            if (Items != null)
            {
                Items.CollectionChanged -= OnFileViewCollectionChanged;
                Items.Items.CollectionChanged -= OnCollectionChanged;
            }
            items.CollectionChanged += OnCollectionChanged; //Attach this event before creating the collection view so it can remove items from the cache properly.
            Items = CreateFileViewCollection(dirId, items);
            Items.CollectionChanged += OnFileViewCollectionChanged;
            OnPropertyChanged("Items");
        }

        private void SetRaiseNotifications(bool raiseNotifications)
        {
            if (Items != null)
                Items.IsEnabled = raiseNotifications;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    {
                        var dirInfo = GetDirectoryInfo(CurrentDirectory);
                        foreach (FileSystemItem oldItem in e.OldItems)
                        {
                            if (oldItem != null)
                            {
                                var itemId = GetItemId(CurrentDirectory, oldItem);
                                dirInfo.Remove(itemId);
                            }
                        }
                    }
                    break;
            }
        }

        private void OnFileViewCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var task = BeforeEnteringAsync();
        }

        private Transaction GetDownloadPicturesTransaction(string dirId)
        {
            Transaction transaction;
            if (!_dirTransactions.TryGetValue(dirId, out transaction) || transaction.IsCompleted)
            {
                transaction = TransactionManager.CreateTransaction();
                _dirTransactions[dirId] = transaction;
            }
            return transaction;
        }


        private void RemoveDirectoryTransaction(string dirId)
        {
            Transaction transaction;
            if (string.IsNullOrWhiteSpace(dirId))
            {
                foreach (var pair in _dirTransactions)
                {
                    pair.Value.Dispose();
                }
                _dirTransactions.Clear();
            }
            else if (_dirTransactions.TryGetValue(dirId, out transaction))
            {
                transaction.Dispose();
                _dirTransactions.Remove(dirId);
            }
        }

#if HOLD_MEMORY
        private Dictionary<string, FileSystemItemViewModel> GetDirectoryInfo(string dirId)
#else
        private WeakDictionary<string, FileSystemItemViewModel> GetDirectoryInfo(string dirId)
#endif
        {
#if HOLD_MEMORY
            Dictionary<string, FileSystemItemViewModel> dirInfo;
#else
            WeakDictionary<string, FileSystemItemViewModel> dirInfo;
#endif
            if (!_fileViews.TryGetValue(dirId, out dirInfo))
            {
#if HOLD_MEMORY
                dirInfo = new Tuple<Transaction, Dictionary<string, FileSystemItemViewModel>>(new Dictionary<string, FileSystemItemViewModel>());
#else
                dirInfo = new WeakDictionary<string, FileSystemItemViewModel>();
#endif
                _fileViews[dirId] = dirInfo;
            }

            return dirInfo;
        }
        private void RemoveDirectoryInfo(string dirId)
        {
#if HOLD_MEMORY
            Dictionary<string, FileSystemItemViewModel> dirInfo;
#else
            WeakDictionary<string, FileSystemItemViewModel> dirInfo;
#endif
            if (string.IsNullOrWhiteSpace(dirId))
            {
                _fileViews.Clear();
            }
            else if (_fileViews.TryGetValue(dirId, out dirInfo))
            {
                _fileViews.Remove(dirId);
            }
        }

        public FileSystemItemViewModel GetViewModel(string dirId, FileSystemItem item)
        {
            var dirInfo = GetDirectoryInfo(dirId);
            var fileId = GetItemId(dirId, item);
            FileSystemItemViewModel ivm;
            if (!dirInfo.TryGetValue(fileId, out ivm))
            {
                ivm = CreateViewModel(dirId, item);
                dirInfo[fileId] = ivm;
            }
            return ivm;
        }

        private async Task<IDataCollection<FileSystemItem>> GetItemsAsync(string dirId, int toIndex = 50, CancellationToken cancellationToken = default(CancellationToken))
        {
            IDataCollection<FileSystemItem> items = null;
            await TransactionManager.RunAsync(OperationKind.DownloadData, dirId, cancellationToken, async ct =>
            {
                if (ShowFiles)
                {
                    var getDirsTask = GetDirectoriesAsync(dirId, cancellationToken);
                    var getFilesTask = GetFilesAsync(dirId, cancellationToken);
                    await Task.WhenAll(getDirsTask, getFilesTask);
                    var dirs = await getDirsTask;
                    var files = await getFilesTask;
                    var sequence = new C1SequenceDataCollection<FileSystemItem>();
                    sequence.Collections.Add(dirs);
                    sequence.Collections.Add(files);
                    items = sequence;
                }
                else
                {
                    items = await FileSystem.GetDirectoriesAsync(dirId, cancellationToken);
                }
                await items.LoadAsync(toIndex: toIndex);
            });
            return items;
        }

        protected virtual FileViewCollection CreateFileViewCollection(string dirId, IDataCollection<FileSystemItem> items)
        {
            return new FileViewCollection(this, dirId, items, "ItemTemplate");
        }
        private string GetItemId(string dirId, FileSystemItem item)
        {
            return item.IsDirectory ? FileSystem.GetDirectoryId(dirId, item.Id) : FileSystem.GetFileId(dirId, item.Id);
        }

        public virtual FileSystemItemViewModel CreateViewModel(string dirId, FileSystemItem item, IFileInfo file = null)
        {
            if (item == null)
                return null;
            var vm = Extensions.CreateViewModel(dirId, item, file);
            vm.GetDownloadPicturesTransaction = () => GetDownloadPicturesTransaction(dirId);
            return vm;
        }

        protected virtual TransactionsViewModel CreateTransactionsViewModel()
        {
            return new TransactionsViewModel(this);
        }

        #endregion

        #region async events

        private SemaphoreSlim _enterExitSemaphore = new SemaphoreSlim(1);
        private bool isIn = true;
        protected virtual async Task OnExitingAsync(string oldDirectory, string newDirectory, object placementTarget)
        {
            if (Exiting != null)
            {
                try
                {
                    await _enterExitSemaphore.WaitAsync();
                    if (isIn)
                    {
                        var forward = IsOnline ? await FileSystem.IsSubDirectory(newDirectory, oldDirectory, CancellationToken.None) : Path.IsSubDirectory(newDirectory, oldDirectory);
                        var e = new ChangingDirectoryEventArgs(oldDirectory, newDirectory, forward, placementTarget);
                        Exiting(this, e);
                        await e.WaitDeferralsAsync();
                        isIn = false;
                    }
                }
                finally
                {
                    _enterExitSemaphore.Release();
                }
            }
        }

        protected virtual async Task OnEnteringAsync(string oldDirectory, string newDirectory)
        {
            if (Entering != null)
            {
                try
                {
                    await _enterExitSemaphore.WaitAsync();
                    if (!isIn)
                    {

                        var forward = IsOnline ? await FileSystem.IsSubDirectory(newDirectory, oldDirectory, CancellationToken.None) : Path.IsSubDirectory(newDirectory, oldDirectory);
                        var e = new ChangingDirectoryEventArgs(oldDirectory, newDirectory, forward, null);
                        Entering(this, e);
                        await e.WaitDeferralsAsync();
                        isIn = true;
                    }
                }
                finally
                {
                    _enterExitSemaphore.Release();
                }
            }
        }

        protected virtual async Task OnUnloadingAsync()
        {
            if (Unloading != null)
            {
                var e = new AsyncEventArgs();
                Unloading(this, e);
                await e.WaitDeferralsAsync();
            }
        }

        protected virtual async Task OnLoadingAsync()
        {
            if (Loading != null)
            {
                var e = new AsyncEventArgs();
                Loading(this, e);
                await e.WaitDeferralsAsync();
            }
        }

        internal async Task ScrollItemIntoViewAsync(string itemId)
        {
            if (ScrollItemIntoView != null)
            {
                var e = new ScrollItemIntoViewAsyncEventArgs(itemId);
                ScrollItemIntoView(this, e);
                await e.WaitDeferralsAsync();
            }

        }

        #endregion

        #region selection

        public bool IsItemSelected(FileSystemItemViewModel item)
        {
            return SelectionManager.Contains(CurrentDirectory, item.Item);
        }

        public IList<FileSystemItemViewModel> GetCurrentDirectorySelectedItems()
        {
            return Items.GetLoadedItems().Select(info => info.Item).Where(item => SelectionManager.Contains(item.ParentDirId, item.Item)).ToList();
        }

        private bool _updatingSelectionManager = false;
        public void SetCurrentDirectorySelection(IList<FileSystemItemViewModel> selectedItems)
        {
            //if (!IsRefreshing)
            {
                var currentGroup = SelectionManager.Selection.Groups.FirstOrDefault(g => g.BaseDirectoryId == CurrentDirectory);
                _updatingSelectionManager = true;
                if (currentGroup != null)
                {
                    foreach (var item in currentGroup.Items)
                    {
                        if (!selectedItems.Any(s => s.Item == item))
                        {
                            SelectionManager.RemoveItem(CurrentDirectory, item);
                        }
                    }
                }
                foreach (var selectedItem in selectedItems)
                {
                    if (currentGroup == null || !currentGroup.Items.Contains(selectedItem.Item))
                    {
                        SelectionManager.AddItem(CurrentDirectory, selectedItem.Item);
                    }
                }
                _updatingSelectionManager = false;
                UpdateSelectionList();
                RaiseSelectionChanged();
            }
        }

        protected void Select(FileSystemItemViewModel item)
        {
            SelectionManager.AddItem(CurrentDirectory, item.Item);
        }

        protected void Unselect(FileSystemItemViewModel item)
        {
            SelectionManager.RemoveItem(CurrentDirectory, item.Item);
        }

        public void ClearSelection()
        {
            SelectionManager.Clear();
        }

        public int SelectedCount
        {
            get
            {
                return _selectedCount;
            }
        }

        public string SelectedMessage
        {
            get
            {
                if (SelectedCount > 0)
                {
                    if (SelectedCount == 1)
                    {
                        return string.Format(ApplicationResources.SelectedItemMessage, SelectedCount);
                    }
                    else
                    {
                        return string.Format(ApplicationResources.SelectedItemsMessage, SelectedCount);
                    }
                }
                return "";
            }
        }

        public FileSystemItemViewModel LastSelectedItem
        {
            get
            {
                return _lastSelectedItem;
            }
        }

        public FileSystemItemViewModel LastTwoSelectedItem
        {
            get
            {
                return _lastTwoSelectedItem;
            }
        }

        public FileSystemItemViewModel LastThreeSelectedItem
        {
            get
            {
                return _lastThreeSelectedItem;
            }
        }

        private void RaiseSelectionChanged()
        {
            if (!_updatingSelectionManager && SelectionChanged != null)
                SelectionChanged(this, new AsyncEventArgs());
        }

        private void UpdateSelectionList()
        {
            foreach (var selectedItem in SelectedItems.ToArray())
            {
                if (!SelectionManager.Contains(selectedItem.ParentDirId, selectedItem.Item))
                {
                    SelectedItems.Remove(selectedItem);
                }
            }
            var selection = SelectionManager.Selection;
            foreach (var group in selection.Groups)
            {
                foreach (var item in group.Items)
                {
                    if (!SelectedItems.Any(s => s.Item == item))
                    {
                        var itemVM = GetViewModel(group.BaseDirectoryId, item);
                        itemVM.PropertyChanged += OnItemPropertyChanged;
                        SelectedItems.Add(itemVM);
                    }
                }
            }
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "SmallItemTemplate")
            {
                var itemVM = sender as FileSystemItemViewModel;
                if (itemVM != null)
                {
                    var index = SelectedItems.IndexOf(itemVM);
                    if (index >= 0)
                    {
                        SelectedItems[index] = itemVM;
                    }
                }
            }
        }

        void OnSelectionChanged(object sender, EventArgs e)
        {
            UpdateSelectionList();
            var selectedItems = SelectedItems.Reverse().ToList();
            _selectedCount = selectedItems.Count;
            _lastSelectedItem = selectedItems.Count > 0 ? selectedItems[0] : null;
            _lastTwoSelectedItem = selectedItems.Count > 1 ? selectedItems[1] : null;
            _lastThreeSelectedItem = selectedItems.Count > 2 ? selectedItems[2] : null;
            RaiseSelectionChanged();
            OnPropertyChanged("SelectedItems");
            OnPropertyChanged("SelectedCount");
            OnPropertyChanged("SelectedMessage");
            OnPropertyChanged("LastSelectedItem");
            OnPropertyChanged("LastTwoSelectedItem");
            OnPropertyChanged("LastThreeSelectedItem");
        }


        #endregion

        #region  ** actions

        public virtual async Task ExecDefaultAction(FileSystemItemViewModel item, object originalSource)
        {
            var file = item as FileSystemFileViewModel;
            if (file != null && file.IsDownloadingContent)
            {
                if (await AppService.ShowQuestionAsync(string.Format(ProgressResources.StopDownloadingFileQuestion, file.Name)))
                    file.StopDownload();
            }
            else if (Extensions != null)
            {
                var context = new FileSystemActionContext(CurrentDirectory, item.Item);
                var defaultAction = (await GetActualFileActions(context, CurrentDirectory)).FirstOrDefault(fea => fea.IsDefault);
                if (defaultAction != null)
                {
                    try
                    {
                        await defaultAction.ExecuteActionAsync(AppService, originalSource);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception)
                    {
                        Debug.Assert(false);
                    }
                }
            }
        }

        public async Task<bool> ExecuteAction(FileSystemAction action, object originalSource)
        {
            try
            {
                await action.ExecuteActionAsync(AppService, originalSource);
                return true;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                Debug.Assert(false);
            }
            return false;
        }

        protected virtual async Task<List<FileSystemAction>> GetActualFileActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var extensions = IsOnline ? Extensions : new FileExplorerExtensions(this, true);
            return (await extensions.GetActions(context, targetDirectoryId)).OrderBy(act => act.Category).ToList();
        }

        public async Task<List<FileSystemAction>> GetActionsAsync()
        {
            if (IsStarted)
                return (await GetActualFileActions(new FileSystemActionContext(CurrentDirectory), CurrentDirectory)).OrderBy(act => act.Category).ToList();
            else
                return new List<FileSystemAction>();
        }

        public async Task<List<FileSystemAction>> GetActionsAsync(FileSystemItemViewModel item)
        {
            return await GetActionsAsync(CurrentDirectory, item);
        }

        public async Task<List<FileSystemAction>> GetActionsAsync(string baseDirectory, FileSystemItemViewModel item)
        {
            if (!IsStarted)
                return new List<FileSystemAction>();
            var context = new FileSystemActionContext(baseDirectory, item.Item);
            var actions = (await GetActualFileActions(context, baseDirectory)).OrderBy(act => act.Category).ToList();
            var isSelected = IsItemSelected(item);
            actions.Add(new FileSystemAction("Select", isSelected ? ApplicationResources.UnselectLabel : ApplicationResources.SelectLabel, context, (a1, args) =>
            {
                if (isSelected)
                {
                    Unselect(item);
                }
                else
                {
                    Select(item);
                }
            })
            { NeedsInternetAccess = false });
            return actions;
        }

        public async Task<List<FileSystemAction>> GetSelectionActions()
        {
            var actions = (await GetActualFileActions(SelectionManager.Selection, CurrentDirectory)).Where(act => act.Category != FileSystemActionCategory.Open).OrderBy(act => act.Category).ToList();
            actions.Add(new FileSystemAction("ClearSelection", ApplicationResources.ClearSelectionLabel, SelectionManager.Selection, (a1, args) =>
            {
                ClearSelection();
            })
            { NeedsInternetAccess = false });
            return actions;
        }

        #endregion

        #region transactions

        public string Status { get; private set; } = "";

        public double? StatusProgress { get; private set; } = null;

        public TimeSpan StatusNotificationDelay { get; private set; }

        private async void OnTransactionChanged(object sender, TransactionEventArgs e)
        {
            try
            {
                await UpdateStatus();
            }
            catch { }
        }

        private async Task UpdateStatus()
        {
            try
            {
                await Task.Delay(300);//Avoids changing the status for small operations like reading images from the cache.
                await _semaphore.WaitAsync();

                string status = "";
                double? progress = null;
                if (!IsStarted)
                {
                    status = ProgressResources.InitializingMessage;
                }
                else
                {
                    status = TransactionViewModel.GetPendingActionsMessage(Transactions);
                    progress = TransactionViewModel.GetPendingActionsProgress(Transactions);
                }

                if (status != Status || progress != StatusProgress)
                {
                    var showStatus = Status == "";
                    var hideStatus = status == "";
                    if (hideStatus)
                    {
                        Status = "";
                        StatusProgress = null;
                        await OnStatusHidden();
                    }
                    else
                    {
                        Status = status;
                        StatusProgress = progress;
                        if (showStatus)
                        {
                            await OnStatusShown();
                            await Task.Delay(StatusNotificationDelay);
                        }
                        else
                        {
                            await OnStatusChanged();
                        }
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public IEnumerable<Operation> Operations
        {
            get
            {
                return Transactions.SelectMany(t => t.Operations);
            }
        }

        public IList<Transaction> Transactions
        {
            get
            {
                return TransactionManager.Transactions;
            }
        }


        public event EventHandler<AsyncEventArgs> StatusShown;
        public event EventHandler<AsyncEventArgs> StatusHidden;
        public event EventHandler<AsyncEventArgs> StatusChanged;

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private async Task OnStatusShown()
        {
            var e = new AsyncEventArgs();
            if (StatusShown != null)
                StatusShown(this, e);
            await e.WaitDeferralsAsync();
        }

        private async Task OnStatusHidden()
        {
            var e = new AsyncEventArgs();
            if (StatusHidden != null)
                StatusHidden(this, e);
            await e.WaitDeferralsAsync();
        }

        private async Task OnStatusChanged()
        {
            var e = new AsyncEventArgs();
            if (StatusChanged != null)
                StatusChanged(this, e);
            await e.WaitDeferralsAsync();
        }

        #endregion

        #region navigation

        public TaskCommand GoBackCommand
        {
            get
            {
                return _goBack;
            }
        }

        private bool CanGoBack(object obj)
        {
            return CurrencyManager.GetBackwardPaths().Length > 0 ||
                !string.IsNullOrWhiteSpace(CurrencyManager.GetCurrentDirectory());
        }

        private async Task GoBack(object obj)
        {
            if (CurrencyManager.GetBackwardPaths().Length > 0)
                CurrencyManager.Back(1);
            else
                await CurrencyManager.SetCurrentDirectoryAsync("", false);
        }

        public bool GoBack(bool canGoBack)
        {
            if (CurrencyManager.GetBackwardPaths().Count() > 0)
            {
                CurrencyManager.Back(1);
                return true;
            }
            else if (!canGoBack && CurrencyManager.GetCurrentDirectory() != "")
            {
                CurrencyManager.SetCurrentDirectoryAsync("", keepHistory: false);
                return true;
            }
            return false;
        }

        public async Task GoUpAsync()
        {
            var current = CurrencyManager.GetCurrentDirectory();
            var parent = await FileSystem.GetDirectoryParentIdAsync(current, CancellationToken.None);
            await CurrencyManager.SetCurrentDirectoryAsync(parent, false);
        }


        public async Task SetDirectoryAsync(string dirId)
        {
            if (CurrentDirectory != dirId)
            {
                await CurrencyManager.SetCurrentDirectoryAsync(dirId, false);
            }
        }

        public async Task RefreshAsync()
        {
            await FileSystem.RefreshAsync(CurrentDirectory);
        }

        #endregion

        #region public methods

        protected virtual Task<string> GetEmptyDirectoryMessage()
        {
            return Task.FromResult(Extensions.GetEmptyDirectoryMessage(CurrentDirectory));
        }

        public virtual Task<List<Tuple<string, FileSystemDirectory>>> GetCurrentFullPath(CancellationToken cancellationToken, bool includeRoot = false)
        {
            return GetFullPathAsync(CurrentDirectory, cancellationToken, includeRoot);
        }

        internal async Task<List<Tuple<string, FileSystemDirectory>>> GetFullPathAsync(string dirId, CancellationToken cancellationToken, bool includeRoot = false)
        {
            var fullPath = IsOnline ? await FileSystem.GetFullPathAsync(dirId, cancellationToken) : dirId;
            var directoriesPaths = Path.DecomposePath(fullPath);
            var directories = new List<Tuple<string, FileSystemDirectory>>();
            if (includeRoot)
                directories.Add(new Tuple<string, FileSystemDirectory>("", new FileSystemDirectory() { Name = GetRootName() }));
            foreach (var directoryPath in directoriesPaths)
            {
                var subDirId = FileSystem.GetDirectoryId(Path.GetParentPath(directoryPath), Path.GetFileName(directoryPath));
                var directory = await FileSystem.GetDirectoryAsync(subDirId, false, cancellationToken);
                directories.Add(new Tuple<string, FileSystemDirectory>(subDirId, directory));
            }
            return directories;
        }

        protected virtual string GetRootName()
        {
            return ApplicationResources.ApplicationName;
        }

        public async Task<string> GetBackgroundTemplateKey(string dirId)
        {
            return await Extensions.GetBackgroundTemplateKey(dirId);
        }

        public async Task<AccountDirectory> GetProviderDirectory(string dirId = null)
        {
            var currentProviderId = Open.FileSystemAsync.Path.SplitPath(dirId == null ? CurrentDirectory : dirId).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(currentProviderId))
                return await FileSystem.GetDirectoryAsync(currentProviderId, false, CancellationToken.None) as AccountDirectory;
            return null;
        }

        #endregion

        #region cache


        internal Task<bool> ExistsDirectoryAsync(string dirId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                return FileSystem.ExistsDirectoryAsync(dirId, cancellationToken);
            }
            else
            {
                return Task.FromResult(true);
            }
        }


        internal string GetDirectoryId(string parentDirId, string localDirId)
        {
            if (IsOnline)
                return FileSystem.GetDirectoryId(parentDirId, localDirId);
            else
                return Path.Combine(parentDirId, localDirId);
        }

        internal string GetFileId(string parentDirId, string localFileId)
        {
            if (IsOnline)
                return FileSystem.GetFileId(parentDirId, localFileId);
            else
                return Path.Combine(parentDirId, localFileId);
        }

        private async Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsync(string dirId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                var directories = await FileSystem.GetDirectoriesAsync(dirId, cancellationToken);
                var path = await FileSystem.GetFullPathAsync(dirId, cancellationToken);
                await Cache.SaveDirectoriesAsync(path, directories);
                return directories;
            }
            else
            {
                return await Cache.GetDirectoriesAsync(dirId);
            }
        }

        private async Task<IDataCollection<FileSystemFile>> GetFilesAsync(string dirId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                var files = await FileSystem.GetFilesAsync(dirId, cancellationToken);
                var path = await FileSystem.GetFullPathAsync(dirId, cancellationToken);
                await Cache.SaveFilesAsync(path, files);
                return files;
            }
            else
            {
                return await Cache.GetFilesAsync(dirId);
            }
        }

        internal async Task<bool> CanOpenFileThumbnailFromCacheAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                fileId = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
            }
            return await Cache.ContainsSavedThumbnailAsync(fileId);
        }

        internal async Task<bool> CanOpenFileFromCacheAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                fileId = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
            }
            return await Cache.ContainsSavedFileAsync(fileId);
        }

        internal async Task<Stream> OpenFileFromCacheAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                fileId = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
            }
            return await Cache.TryGetSavedFileAsync(fileId);
        }

        internal async Task<Stream> OpenFileThumbnailFromCacheAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                fileId = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
            }
            return await Cache.TryGetSavedThumbnailAsync(fileId);
        }

        internal Task<bool> CanOpenDirectoryThumbnailAsync(string dirId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                return FileSystem.CanOpenDirectoryThumbnailAsync(dirId, cancellationToken);
            }
            else
            {
                //return CanOpenDirectoryThumbnailFromCacheAsync(dirId, cancellationToken);
                return Task.FromResult(false);
            }
        }

        internal Task<bool> CanOpenFileThumbnailAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                return FileSystem.CanOpenFileThumbnailAsync(fileId, cancellationToken);
            }
            else
            {
                return CanOpenFileThumbnailFromCacheAsync(fileId, cancellationToken);
            }
        }

        public Task<bool> CanOpenFileAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                return FileSystem.CanOpenFileAsync(fileId, cancellationToken);
            }
            else
            {
                return CanOpenFileFromCacheAsync(fileId, cancellationToken);
            }
        }

        internal async Task<Stream> OpenFileAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                var stream = await FileSystem.OpenFileAsync(fileId, cancellationToken);
                var filePath = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
                return await Cache.WatchFileStreamAsync(filePath, stream);
            }
            else
            {
                return await OpenFileFromCacheAsync(fileId, cancellationToken);
            }
        }

        internal async Task<Stream> OpenDirectoryThumbnailAsync(string dirId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                var stream = await FileSystem.OpenDirectoryThumbnailAsync(dirId, cancellationToken);
                //var filePath = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
                //return await Cache.WatchFileThumbnailStreamAsync(filePath, stream);
                return stream;
            }
            else
            {
                return await OpenFileThumbnailFromCacheAsync(dirId, cancellationToken);
            }
        }

        internal async Task<Stream> OpenFileThumbnailAsync(string fileId, CancellationToken cancellationToken)
        {
            if (IsOnline)
            {
                var stream = await FileSystem.OpenFileThumbnailAsync(fileId, cancellationToken);
                var filePath = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
                return await Cache.WatchFileThumbnailStreamAsync(filePath, stream);
            }
            else
            {
                return await OpenFileThumbnailFromCacheAsync(fileId, cancellationToken);
            }
        }


        private async Task DeleteDirectoryAsync(string dirId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var dirPath = await FileSystem.GetFullPathAsync(dirId, cancellationToken);
            await FileSystem.DeleteDirectoryAsync(dirId, sendToTrash, cancellationToken);
            await Cache.DeleteFolderAsync(dirPath);
        }

        private async Task DeleteFileAsync(string fileId, bool sendToTrash, CancellationToken cancellationToken)
        {
            var filePath = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
            await FileSystem.DeleteFileAsync(fileId, sendToTrash, cancellationToken);
            await Cache.DeleteFileAsync(filePath);
        }


        private async Task MoveFileAsync(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            //This must be before moving the file in order to avoid getting the thumbnail before it is moved.
            var filePath = await FileSystem.GetFullFilePathAsync(sourceFileId, cancellationToken);
            var targetPath = await FileSystem.GetFullPathAsync(targetDirId, cancellationToken);
            var targetFilePath = Path.Combine(targetPath, Path.GetFileName(sourceFileId));
            if (filePath != targetFilePath)
                await Cache.MoveFileAsync(filePath, targetFilePath);
            try
            {
                await FileSystem.MoveFileAsync(sourceFileId, targetDirId, file, cancellationToken);
            }
            catch
            {
                if (filePath != targetFilePath)
                    await Cache.MoveFileAsync(targetFilePath, filePath);
                throw;
            }
        }

        private async Task MoveDirectoryAsync(string sourceDirId, string targetDirId, FileSystemDirectory directory, CancellationToken cancellationToken)
        {
            //This must be before moving the file in order to avoid getting the thumbnail before it is moved.
            var sourceDirPath = await FileSystem.GetFullPathAsync(sourceDirId, cancellationToken);
            var targetDirPath = await FileSystem.GetFullPathAsync(targetDirId, cancellationToken);
            var targetSubDirPath = Path.Combine(targetDirPath, Path.GetFileName(sourceDirId));
            if (sourceDirPath != targetSubDirPath)
                await Cache.MoveDirectoryAsync(sourceDirPath, targetSubDirPath);
            try
            {
                await FileSystem.MoveDirectoryAsync(sourceDirId, targetDirId, directory, cancellationToken);
            }
            catch
            {
                if (sourceDirPath != targetSubDirPath)
                    await Cache.MoveDirectoryAsync(targetSubDirPath, sourceDirPath);
                throw;
            }
        }

        private async Task CopyFileAsync(string sourceFileId, string targetDirId, FileSystemFile file, CancellationToken cancellationToken)
        {
            //This must be before moving the file in order to avoid getting the thumbnail before it is moved.
            var filePath = await FileSystem.GetFullFilePathAsync(sourceFileId, cancellationToken);
            var targetPath = await FileSystem.GetFullPathAsync(targetDirId, cancellationToken);
            var targetFilePath = Path.Combine(targetPath, Path.GetFileName(sourceFileId));
            if (filePath != targetFilePath)
                await Cache.CopyFileAsync(filePath, targetFilePath);
            try
            {
                await FileSystem.CopyFileAsync(sourceFileId, targetDirId, file, cancellationToken);
            }
            catch
            {
                if (filePath != targetFilePath)
                    await Cache.DeleteFileAsync(targetFilePath);
                throw;
            }
        }

        private async Task CopyDirectoryAsync(string targetDirId, string sourceDirId, FileSystemDirectory file, CancellationToken cancellationToken)
        {
            await FileSystem.CopyDirectoryAsync(sourceDirId, targetDirId, file, cancellationToken);
        }

        private async Task UpdateFileAsync(string fileId, FileSystemFile file, CancellationToken cancellationToken)
        {
            var filePath = await FileSystem.GetFullFilePathAsync(fileId, cancellationToken);
            var targetFilePath = Path.Combine(Path.GetParentPath(filePath), file.Name);
            if (filePath != targetFilePath)
                await Cache.CopyFileAsync(filePath, targetFilePath);
            try
            {
                var updatedFile = await FileSystem.UpdateFileAsync(fileId, file, cancellationToken);
                var updatedFilePath = Path.Combine(Path.GetParentPath(filePath), updatedFile.Id);
                if (updatedFilePath != targetFilePath)
                    await Cache.DeleteFileAsync(targetFilePath);
                else
                    await Cache.DeleteFileAsync(filePath);
            }
            catch
            {
                if (filePath != targetFilePath)
                    await Cache.DeleteFileAsync(targetFilePath);
                throw;
            }
        }

        internal async Task SetIsOnlineAsync(bool value)
        {
            IsOnline = value;
            CurrencyManager.ClearHistory();
            if (IsOnline)
            {
                var currentDir = FileSystem.GetDirectoryId(Path.GetParentPath(CurrentDirectory), Path.GetFileName(CurrentDirectory));
                var dir = await CurrencyManager.SetCurrentDirectoryAsync(currentDir);
            }
            else
            {
                var currentDir = await FileSystem.GetFullPathAsync(CurrentDirectory, CancellationToken.None);
                var dir = await CurrencyManager.SetCurrentDirectoryAsync(currentDir);
            }
        }

        public async Task ClearCache()
        {
            await Cache.ClearCache();
            await ClearTempFolder();
        }

        public async Task ClearTempFolder()
        {
            try
            {
                var storage = AppService.GetTemporaryStorage();
                await storage.DeleteDirectoryAsync("temp");
            }
            catch { }
        }

        public async Task<long> GetTotalUsedSizeAsync()
        {
            var cache = Cache;
            long space = 0;
            var getMetadataUsedSpaceTask = cache.GetMetadataUsedSpaceAsync();
            var getThumbnailsUsedSpaceTask = cache.GetThumbnailsUsedSpaceAsync();
            var getFilesUsedSpaceTask = cache.GetFilesUsedSpaceAsync();
            await Task.WhenAll(new Task[] { getMetadataUsedSpaceTask, getThumbnailsUsedSpaceTask, getFilesUsedSpaceTask });
            space += await getMetadataUsedSpaceTask;
            space += await getThumbnailsUsedSpaceTask;
            space += await getFilesUsedSpaceTask;
            var storage = AppService.GetTemporaryStorage();
            space += await storage.GetFolderSizeAsync("temp");
            return space;
        }

        #endregion

        #region providers

        protected virtual ConnectionsViewModel CreateConnectionsViewModel()
        {
            return new ConnectionsViewModel(this);
        }

        public async Task AddProvider(ProviderViewModel providerViewModel)
        {
            if (await AppService.IsNetworkAvailableAsync())
            {
                try
                {
                    var dir = await (FileSystem as GlobalFileSystem).AddConnection(providerViewModel.Provider, CancellationToken.None);
                    if (dir != null)
                    {
                        await AppService.GoToMain();
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    await AppService.ShowErrorAsync(GlobalResources.AddAccountError);
                }
            }
            else
            {
                await AppService.ShowErrorAsync(FileSystemResources.NetworkUnavailableMessage);
            }
        }

        #endregion

        #region operations

        #region open

        public async Task OpenDirectoryAsync(string dirId, string dirName, object placementTarget, CancellationToken cancellationToken)
        {
            var retries = 1;
            beggining:
            try
            {
                if (dirId != CurrentDirectory)
                {
                    bool accessGranted = false;
                    var exitingTask = OnExitingAsync(CurrentDirectory, dirId, placementTarget);
                    using (var txn = TransactionManager.CreateTransaction(1))
                    {
                        var operation = txn.Enqueue(OperationKind.OpenDirectory, dirName, cancellationToken, async (ct) =>
                        {
                            if (IsOnline)
                                accessGranted = await FileSystem.CheckAccessAsync(dirId, true, ct);
                            else
                                accessGranted = true;
                        });
                        await Task.WhenAll(exitingTask, operation.RunAsync());
                    }
                    if (accessGranted)
                    {
                        var currentDir = await CurrencyManager.SetCurrentDirectoryAsync(dirId, true);
                        if (currentDir != dirId)
                            await OnEnteringAsync(CurrentDirectory, CurrentDirectory);
                    }
                    else
                    {
                        await OnEnteringAsync(CurrentDirectory, CurrentDirectory);
                        AppService.NotifyError(FileSystemResources.AccessDeniedMessage);
                    }
                }
            }
            catch (AccessDeniedException)
            {
                if (retries > 0)
                {
                    await FileSystem.InvalidateAccessAsync(dirId, cancellationToken);
                    retries--;
                    goto beggining;
                }
                else
                {
                    await OnEnteringAsync(CurrentDirectory, CurrentDirectory);
                    AppService.NotifyError(FileSystemResources.AccessDeniedMessage);
                }

            }
            catch (OperationCanceledException)
            {
                await OnEnteringAsync(CurrentDirectory, CurrentDirectory);
            }
            catch (Exception)
            {
                await OnEnteringAsync(CurrentDirectory, CurrentDirectory);
                AppService.NotifyError(FileSystemResources.AccessDeniedMessage);
            }
        }

        public async Task<bool> CanOpenFile(string fileId, string contentType)
        {
            return (await AppService.CanOpenAlbum(CancellationToken.None) && contentType != null && MimeType.Parse(contentType).Type == "image" && (await FileSystem.CanOpenFileAsync(fileId, CancellationToken.None))) ||
                await AppService.CanOpenFile(contentType, fileId, CancellationToken.None);
        }

        public async Task OpenFile(string dirId, string fileId, FileSystemFile file, CancellationToken cancellationToken, object placementTarget = null)
        {
            if (await AppService.CanOpenAlbum(cancellationToken) &&
                MimeType.Parse(file.ContentType).Type == "image" &&
                (await FileSystem.CanOpenFileAsync(fileId, cancellationToken)))
            {
                await AppService.OpenAlbum(dirId, file, placementTarget, cancellationToken);
            }
            else
            {
                await AppService.OpenFile(file.ContentType, fileId, placementTarget, cancellationToken);
            }
        }

        public async Task NavigateTo(string dirId, string fileId)
        {
            if (dirId == null)
                dirId = await FileSystem.GetFileParentIdAsync(fileId, CancellationToken.None);
            await AppService.GoToMain(dirId);
            await SetDirectoryAsync(dirId);
            if (fileId != null)
                await ScrollItemIntoViewAsync(fileId);
        }

        #endregion

        internal async Task ShareFileAsync(string dirId, FileSystemFile file, string fileId, object placementTarget, CancellationToken cancellationToken)
        {
            var packages = new List<SharedSource>();
            var safeFileName = GetSafeName(dirId, file);
            if (AppService.CanShareLink())
            {
                var link = file.Link;
                if (link == null && await FileSystem.CanGetFileLinkAsync(fileId, cancellationToken))
                {
                    link = await FileSystem.GetFileLinkAsync(fileId, cancellationToken);
                }
                if (link != null)
                    packages.Add(new SharedLink(safeFileName, link));
            }
            if (AppService.CanShareFile() && await FileSystem.CanOpenFileAsync(fileId, cancellationToken))
            {
                packages.Add(new SharedFile(safeFileName,
                    async () =>
                    {
                        var isoPath = await DownloadFileToCacheAsync(fileId, false, CancellationToken.None);
                        return await Cache.Storage.TryGetFileAsync(isoPath);
                    }));
            }
            await AppService.ShareLinkAsync(packages, placementTarget);
        }

        private string GetSafeName(string dirId, FileSystemFile file)
        {
            var safeFileName = string.IsNullOrWhiteSpace(file.Name) ? FileSystemResources.UnknownFileLabel : file.Name;
            if (!Extensions.UseFileExtension(dirId) || Path.HasExtension(safeFileName))
            {
                return safeFileName;
            }
            else
            {
                string extension = null;
                if (file != null && !string.IsNullOrWhiteSpace(file.ContentType))
                {
                    extension = MimeType.GetExtensionsFromContentType(file.ContentType).FirstOrDefault();
                }
                return safeFileName + (string.IsNullOrWhiteSpace(extension) ? "" : extension);
            }
        }

        #region create directory


        public async Task<bool> CanCreateDirectoryAsync(FileSystemActionContext context)
        {
            return context.IsEmptyGroup && await FileSystem.CanCreateDirectory(context.SingleGroup.BaseDirectoryId, CancellationToken.None);
        }

        public async Task CreateDirectoryAsync(string caption, FileSystemActionContext context, object placementTarget, CancellationToken cancellationToken)
        {
            var dirId = context.SingleGroup.BaseDirectoryId;
            var dir = Extensions.CreateDirectoryItem(dirId, "", "", null);
            var dirVM = CreateViewModel(dirId, dir);
            beggining:
            if (await AppService.ShowItemFormAsync(dirVM,
                caption ?? FileSystemResources.CreateFolderLabel,
                positiveButton: FileSystemResources.CreateLabel,
                negativeButton: ApplicationResources.CancelLabel,
                placementTarget: placementTarget))
            {
                dirVM.Validate();
                if (dirVM.HasErrors)
                    goto beggining;
                var createdDir = await CreateDirectoryTransactionAsync(dirId, dirVM, cancellationToken);
                if (createdDir == null)
                    goto beggining;
                if (CurrentDirectory == dirId)
                {
                    var createdDirId = FileSystem.GetDirectoryId(dirId, createdDir.Id);
                    await ScrollItemIntoViewAsync(createdDirId);
                }
            }
        }

        protected async Task<FileSystemDirectory> CreateDirectoryTransactionAsync(string dirId, FileSystemItemViewModel dirVM, CancellationToken cancellationToken)
        {
            FileSystemDirectory createdDir = null;
            var dir = dirVM.Item as FileSystemDirectory;
            try
            {
                await TransactionManager.RunAsync(OperationKind.CreateDirectory, dir.Name, cancellationToken, async ct =>
                {
                    await FileSystem.CheckAccessAsync(dirId, true, cancellationToken);
                    createdDir = await FileSystem.CreateDirectoryAsync(dirId, dir, cancellationToken);
                });
            }
            catch (ArgumentNullException argExc)
            {
                dirVM.SetError(new ValidationError(FileSystemResources.RequiredLabel), argExc.Message/*ParamName*/);
            }
            catch (DuplicatedItemException)
            {
                dirVM.SetError(new ValidationError(FileSystemResources.DuplicatedFolderError), "Name");
            }
            catch (AccessDeniedException)
            {
                await FileSystem.InvalidateAccessAsync(dirId, cancellationToken);
                dirVM.SetError(new ValidationError(FileSystemResources.CreateFolderError), "");
            }
            catch (Exception)
            {
                dirVM.SetError(new ValidationError(FileSystemResources.CreateFolderError), "");
            }
            return createdDir;

        }
        #endregion

        #region download


        public async Task<bool> CanDownloadAsync(FileSystemActionContext context)
        {
            if (AppService.CanPickFolderToDownload())
            {
                return context.Items.Count() > 0 && await context.Files.AllAsync(async file =>
                {
                    var fileId = FileSystem.GetFileId(file.Item1, file.Item2);
                    if (IsOnline)
                    {
                        return await FileSystem.CanOpenFileAsync(fileId, CancellationToken.None);
                    }
                    else
                    {
                        return await Cache.ContainsSavedFileAsync(fileId);
                    }
                });
            }
            else
            {
                if (!context.IsSingleFile)
                    return false;
                var file = context.Items.First();
                var fileId = FileSystem.GetFileId(file.Item1, file.Item2);
                return AppService.CanSaveFile(context.SingleFile.ContentType) && await FileSystem.CanOpenFileAsync(fileId, CancellationToken.None);
            }
        }

        public async Task DownloadAsync(FileSystemActionContext context, CancellationToken cancellationToken, object placementTarget = null)
        {
            if (!IsOnline)
            {
                if (!await AppService.ShowQuestionAsync(/*TODO*/"In offline mode the files from the cache will be copied. Do you want to continue?"))
                    return;
            }
            if (AppService.CanPickFolderToDownload())
            {
                var pickedDirId = await AppService.PickFolderToDownloadAsync(context.Files.Select(f => f.Item3.ContentType), placementTarget);

                var storage = await AppService.GetPublicStorage(pickedDirId);
                var localFileSystem = new LocalFileSystem(storage);

                using (var txn = TransactionManager.CreateTransaction(4))
                {
                    bool keepExistingFiles = false;
                    var collisions = await TransactionManager.RunAsync(txn, OperationKind.DownloadData, cancellationToken,
                        (ct) => DetermineCollisions(context.Items, localFileSystem, "", ct));
                    if (collisions.Count > 0)
                    {
                        #region message
                        string message;
                        if (collisions.Count() == 1 && collisions.First().Item3.IsDirectory)
                        {
                            message = FileSystemResources.DuplicatedFolderError;
                        }
                        else if (collisions.Count() == 1)
                        {
                            message = FileSystemResources.DuplicatedFileError;
                        }
                        else
                        {
                            message = string.Format(FileSystemResources.DuplicatedItemsError, collisions.Count());
                        }
                        #endregion
                        var selected = await AppService.ShowSelectAsync(message.ToString(), new string[] { FileSystemResources.KeepExistingFilesMessage, FileSystemResources.RenameFilesMessage });
                        keepExistingFiles = selected == 0;
                    }
                    var items = await TransactionManager.RunAsync(txn, OperationKind.DownloadData, cancellationToken,
                        (ct) => CreateDownloadPlan(keepExistingFiles, localFileSystem, "", context.Items, ct));

                    var files = items.Where(i => !i.Item.IsDirectory).ToList();
                    var downloadableFiles = (await files.WhereAsync(async f => await FileSystem.CanOpenFileAsync(f.ItemId, cancellationToken))).ToList();
                    var nonDownloadableFilesCount = files.Count - downloadableFiles.Count;
                    string sizeString = "";
                    string question = "";
                    if (nonDownloadableFilesCount > 0)
                    {
                        if (nonDownloadableFilesCount > 1)
                            question += string.Format(FileSystemResources.DownloadFilesForbidden, nonDownloadableFilesCount);
                        else
                            question += string.Format(FileSystemResources.DownloadFileForbidden, (await files.WhereAsync(async f => !await FileSystem.CanOpenFileAsync(f.ItemId, cancellationToken))).First().Item.Name);
                        question += "\n\n";
                    }
                    if (downloadableFiles.All(f => f.Item.Size.HasValue))
                    {
                        var totalSize = downloadableFiles.Sum(f => f.Item.Size.Value);
                        sizeString = string.Format(" ({0}) ", ToSizeString(totalSize));
                    }
                    if (files.Count == 1)
                    {
                        question += string.Format(FileSystemResources.DownloadFileMessage, sizeString) + "\n\n";
                    }
                    else
                    {
                        question += string.Format(FileSystemResources.DownloadFilesMessage, files.Count, sizeString) + "\n\n";
                    }
                    question += FileSystemResources.ContinueQuestion;
                    if (!await AppService.ShowQuestionAsync(question))
                        throw new OperationCanceledException();

                    #region Creates directory structure & download files

                    try
                    {
                        var namesDict = new Dictionary<string, List<string>>();
                        var extensions = new LocalFileSystemExtensions(this);
                        foreach (var item in items)
                        {
                            if (item.Item.IsDirectory)
                            {
                                if (item.AlreadyCreated)
                                    continue;
                                var operation = txn.Enqueue(OperationKind.CreateDirectory, item.Item.Name, cancellationToken,
                                async ct =>
                                {
                                    await item.WaitForParent();
                                    var newDirectoryTargetId = item.GetTargetDirId() ?? "";
                                    var usedNames = await GetUsedNames(extensions, localFileSystem, namesDict, newDirectoryTargetId, ct);
                                    var newDirectory = extensions.CopyDirectoryItem(newDirectoryTargetId, item.Item as FileSystemDirectory, usedNames);
                                    usedNames.Add(newDirectory.Name);
                                    var createdDirectory = await localFileSystem.CreateDirectoryAsync(newDirectoryTargetId, newDirectory, ct);
                                    item.Result = localFileSystem.GetDirectoryId(newDirectoryTargetId, createdDirectory.Id);
                                });
                                item.Operation = operation;
                            }
                            else
                            {
                                txn.Enqueue(OperationKind.DownloadFile, item.Item.Name, item.Item.Size, cancellationToken,
                                async (p, ct) =>
                                {
                                    var file = item.Item as FileSystemFile;
                                    await item.WaitForParent();
                                    var targetDirId = item.GetTargetDirId() ?? "";
                                    System.IO.Stream fileStream = null;
                                    try
                                    {
                                        fileStream = await FileSystem.OpenFileAsync(item.ItemId, ct);
                                        var usedNames = await GetUsedNames(extensions, localFileSystem, namesDict, targetDirId, ct);
                                        var newItem = extensions.CopyFileItem(targetDirId, file, usedNames);
                                        usedNames.Add(newItem.Name);
                                        await localFileSystem.WriteFileAsync(targetDirId, newItem, fileStream, p, ct);
                                    }
                                    finally
                                    {
                                        fileStream?.Dispose();
                                    }
                                });
                            }
                        }
                        await txn.RunAsync();
                        AppService.Notify(GetDownloadSuccedMessage(context.Items), new Dictionary<string, string>(), placementTarget);
                    }
                    catch (AggregateException exc)
                    {
                        string m;
                        if (!GetDuplicatedItemMessage(exc, out m))
                        {
                            m = FileSystemResources.SaveFilesError;
                        }
                        AppService.NotifyError(m);
                        throw;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                    #endregion
                }
            }
            else
            {
                var file = context.SingleFile;
                var fileId = FileSystem.GetFileId(context.SingleGroup.BaseDirectoryId, file.Id);
                var suggestedFileName = Path.GetFileNameWithoutExtension(file.Name);
                if (string.IsNullOrWhiteSpace(suggestedFileName))
                    suggestedFileName = FileSystemResources.UnknownFileLabel;
                var defaultExtension = Path.GetExtension(file.Name);
                var extensions = MimeType.GetExtensionsFromContentType(file.ContentType);
                if (extensions == null)
                    extensions = new string[] { defaultExtension };
                var fileTypeChoices = new Dictionary<string, IList<string>>();
                fileTypeChoices.Add("", extensions);
                using (var txn = TransactionManager.CreateTransaction())
                {
                    try
                    {
                        txn.Enqueue(OperationKind.DownloadFile, file.Name, file.Size,
                        async (p, ct) =>
                        {
                            System.IO.Stream fileStream = null;
                            try
                            {
                                fileStream = await FileSystem.OpenFileAsync(fileId, CancellationToken.None);
                                await AppService.SaveFileAsync(suggestedFileName, file.ContentType, fileStream, defaultExtension, fileTypeChoices, p, ct);
                            }
                            finally
                            {
                                if (fileStream != null)
                                    fileStream.Dispose();
                            }
                        });
                        await txn.RunAsync();
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception)
                    {
                        await AppService.ShowErrorAsync(FileSystemResources.DownloadFileError);
                    }
                }
            }
        }
        private async Task<List<ExecItem>> CreateDownloadPlan(bool keepExistingFiles, IFileSystemAsync targetFileSystem, string targetDirId, IEnumerable<Tuple<string, string, FileSystemItem>> enumerable, CancellationToken cancellationToken)
        {
            var result = new List<ExecItem>();
            foreach (var item in enumerable)
            {
                if (item.Item3.IsDirectory)
                {
                    result.AddRange(await CreateDownloadDirectoryPlan(keepExistingFiles, targetFileSystem, targetDirId, FileSystem, item, null, cancellationToken));
                }
                else
                {
                    var fileId = FileSystem.GetFileId(item.Item1, item.Item2);
                    if (await FileSystem.CanOpenFileAsync(fileId, cancellationToken))
                    {
                        result.Add(new ExecItem(null, fileId, item.Item3, null, false, item, null));
                    }
                }
            }
            return result;
        }

        private static async Task<List<ExecItem>> CreateDownloadDirectoryPlan(bool keepExistingFiles, IFileSystemAsync targetFileSystem, string targetDirId, IFileSystemAsync sourceFileSystem, Tuple<string, string, FileSystemItem> contextItem, ExecItem parent, CancellationToken cancellationToken)
        {
            var sourceParentDirId = contextItem.Item1;
            var dir = contextItem.Item3 as FileSystemDirectory;
            var plan = new List<ExecItem>();
            FileSystemDirectory existingDir = null;
            string targetSubDirId = null;
            if (keepExistingFiles)
            {
                var existingDirs = await targetFileSystem.GetDirectoriesAsync(targetDirId, cancellationToken);
                await existingDirs.LoadAsync();
                existingDir = existingDirs.FirstOrDefault(d => IsSameItem(d, dir));
                if (existingDir != null)
                {
                    targetSubDirId = targetFileSystem.GetDirectoryId(targetDirId, existingDir.Id);
                }
            }
            var sourceDirId = sourceFileSystem.GetDirectoryId(sourceParentDirId, dir.Id);
            var createDir = new ExecItem(targetDirId, sourceDirId, dir, parent, false, contextItem, existingDir != null ? targetFileSystem.GetDirectoryId(targetDirId, existingDir.Id) : null);
            plan.Add(createDir);
            var subPlan = await CreateDownloadDirectoryPlan(keepExistingFiles, targetFileSystem, targetSubDirId, sourceFileSystem, sourceDirId, createDir, cancellationToken);
            plan.AddRange(subPlan);
            return plan;
        }

        private static async Task<List<ExecItem>> CreateDownloadDirectoryPlan(bool keepExistingFiles, IFileSystemAsync targetFileSystem, string targetDirId, IFileSystemAsync sourceFileSystem, string sourceDirId, ExecItem parent, CancellationToken cancellationToken)
        {
            var plan = new List<ExecItem>();
            IEnumerable<FileSystemFile> files;
            IEnumerable<FileSystemFile> existingFiles;
            IEnumerable<FileSystemDirectory> directories;
            IEnumerable<FileSystemDirectory> existingDirectories;
            if (keepExistingFiles && targetDirId != null)
            {
                var getFilesTask = GetAllFiles(sourceFileSystem, sourceDirId, cancellationToken);
                var getDirsTask = GetAllDirectories(sourceFileSystem, sourceDirId, cancellationToken);
                var getTargetFilesTask = GetAllFiles(targetFileSystem, targetDirId, cancellationToken);
                var getTargetDirsTask = GetAllDirectories(targetFileSystem, targetDirId, cancellationToken);
                await Task.WhenAll(getFilesTask, getDirsTask, getTargetFilesTask, getTargetDirsTask);
                directories = getDirsTask.Result;
                files = getFilesTask.Result;
                existingDirectories = getTargetDirsTask.Result;
                existingFiles = getTargetFilesTask.Result;
            }
            else
            {
                var getFilesTask = GetAllFiles(sourceFileSystem, sourceDirId, cancellationToken);
                var getDirsTask = GetAllDirectories(sourceFileSystem, sourceDirId, cancellationToken);
                await Task.WhenAll(getFilesTask, getDirsTask);
                directories = getDirsTask.Result;
                files = getFilesTask.Result;
                existingDirectories = new FileSystemDirectory[0];
                existingFiles = new FileSystemFile[0];
            }
            foreach (var file in files)
            {
                if (!keepExistingFiles || existingFiles.FirstOrDefault(f => IsSameItem(f, file)) == null)
                {
                    plan.Add(new ExecItem(targetDirId, sourceFileSystem.GetFileId(sourceDirId, file.Id), file, parent, false, null, null));
                }
            }
            foreach (var dir in directories)
            {
                var subDirId = sourceFileSystem.GetDirectoryId(sourceDirId, dir.Id);
                ExecItem createSubDir = null;
                string targetSubDirId = null;
                var existingDir = existingDirectories.FirstOrDefault(f => IsSameItem(f, dir));
                if (existingDir != null)
                {
                    targetSubDirId = targetFileSystem.GetDirectoryId(targetDirId, existingDir.Id);
                }
                if (!keepExistingFiles || existingDir == null)
                {
                    createSubDir = new ExecItem(targetDirId, subDirId, dir, parent, false, null, null);
                    plan.Add(createSubDir);
                }
                var subPlan = await CreateDownloadDirectoryPlan(keepExistingFiles, targetFileSystem, targetSubDirId, sourceFileSystem, subDirId, createSubDir, cancellationToken);
                plan.AddRange(subPlan);
            }
            return plan;
        }

        private static async Task<IEnumerable<FileSystemFile>> GetAllFiles(IFileSystemAsync fileSystem, string dirId, CancellationToken cancellationToken)
        {
            var files = await fileSystem.GetFilesAsync(dirId, cancellationToken);
            await files.LoadAsync();
            return files;
        }

        private static async Task<IEnumerable<FileSystemDirectory>> GetAllDirectories(IFileSystemAsync fileSystem, string dirId, CancellationToken cancellationToken)
        {
            var directories = await fileSystem.GetDirectoriesAsync(dirId, cancellationToken);
            await directories.LoadAsync();
            return directories;
        }

        public async Task<string> DownloadFileToCacheAsync(string fileId, bool skipCache, CancellationToken cancellationToken)
        {
            var dirId = await FileSystem.GetFileParentIdAsync(fileId, cancellationToken);
            var file = await FileSystem.GetFileAsync(fileId, false, cancellationToken);
            var fileVM = GetViewModel(dirId, file) as FileSystemFileViewModel;
            System.IO.Stream fileStream = null;
            try
            {
                fileStream = await fileVM.OpenFileAsync(cancellationToken);
                await fileStream.ReadToEndAsync(cancellationToken);
            }
            finally
            {
                if (fileStream is StreamAsync)
                {
                    await (fileStream as StreamAsync)?.DisposeAsync(cancellationToken);
                }
                else
                {
                    fileStream?.Dispose();
                }
            }
            var isoPath = Cache.GetFilesPath(await FileSystem.GetFullFilePathAsync(fileId, cancellationToken));
            return isoPath;
        }

        #endregion

        #region upload

        public async Task<bool> CanUploadAsync(FileSystemActionContext context)
        {
            if (AppService.Settings.IsOnline && context.IsEmptyGroup)
            {
                var dirId = context.SingleGroup.BaseDirectoryId;
                var contentTypes = FileSystem.GetAcceptedFileTypes(dirId, false);
                return (contentTypes == null || AppService.CanPickFiles(contentTypes)) &&
                        await FileSystem.CanWriteFileAsync(dirId, CancellationToken.None);
            }
            return false;
        }

        public async Task<Tuple<bool, string>> CanUploadToAsync(string[] contentTypes, string dirId)
        {
            if (!await FileSystem.CanWriteFileAsync(dirId, CancellationToken.None))
            {
                return new Tuple<bool, string>(false, GlobalResources.NotSupportedFilesMessage);
            }

            var mimeTypes = FileSystem.GetAcceptedFileTypes(dirId, false);
            foreach (var contentType in contentTypes)
            {
                if (!ContainsMimeType(mimeTypes, contentType))
                    return new Tuple<bool, string>(false, GlobalResources.NotSupportedFileTypeMessage);
            }
            return new Tuple<bool, string>(true, null);
        }

        public async Task UploadAsync(string dirId, string caption, bool multiSelect, bool showUploadForm, CancellationToken cancellationToken, object placementTarget)
        {
            var contentTypes = FileSystem.GetAcceptedFileTypes(dirId, false);
            var files = await AppService.PickFilesAsync(multiSelect: multiSelect, contentTypes: contentTypes);
            if (files != null && files.Count() > 0)
            {

                var itemViewModels = new List<FileSystemItemViewModel>();
                var namesDict = new Dictionary<string, List<string>>();
                foreach (var fileInfo in files)
                {
                    var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, dirId, cancellationToken);
                    var file = Extensions.CreateFileItem(dirId, "", fileInfo.Name, fileInfo.ContentType, usedNames);
                    usedNames.Add(file.Name);
                    var itemVM = CreateViewModel(dirId, file, fileInfo) as FileSystemFileViewModel;
                    itemViewModels.Add(itemVM);
                }

                if (!showUploadForm || await AppService.ShowItemsFormAsync(itemViewModels,
                    caption ?? FileSystemResources.UploadFilesLabel, FileSystemResources.UploadLabel, ApplicationResources.CancelLabel, placementTarget: placementTarget))
                {
                    await UploadFiles(dirId, itemViewModels.Select(ivm => new Tuple<FileSystemFile, IFileInfo>(ivm.Item as FileSystemFile, (ivm as FileSystemFileViewModel).File)).ToList(), cancellationToken, placementTarget);
                }
            }
        }


        public async Task<Task> UploadFilesAsync(IEnumerable<IFileInfo> files, CancellationToken cancellationToken, object placementTarget)
        {
            var pickedDirId = await AppService.PickFolderToUploadAsync("", files.Select(f => f.ContentType), placementTarget);
            var files2 = new List<Tuple<FileSystemFile, IFileInfo>>();
            var namesDict = new Dictionary<string, List<string>>();
            foreach (var fileInfo in files)
            {
                var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, pickedDirId, cancellationToken);
                var file = Extensions.CreateFileItem(pickedDirId, "", fileInfo.Name, fileInfo.ContentType, usedNames);
                usedNames.Add(file.Name);
                files2.Add(new Tuple<FileSystemFile, IFileInfo>(file, fileInfo));
            }
            return UploadFiles(pickedDirId, files2, cancellationToken, placementTarget);
        }

        public async Task<Task> UploadFileAsync(string dirId, IFileInfo fileInfo, CancellationToken cancellationToken, object placementTarget)
        {
            var files2 = new List<Tuple<FileSystemFile, IFileInfo>>();
            var namesDict = new Dictionary<string, List<string>>();
            var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, dirId, cancellationToken);
            var file = Extensions.CreateFileItem(dirId, "", fileInfo.Name, fileInfo.ContentType, usedNames);
            usedNames.Add(file.Name);
            files2.Add(new Tuple<FileSystemFile, IFileInfo>(file, fileInfo));
            return UploadFiles(dirId, files2, cancellationToken, placementTarget);
        }

        public async Task UploadFiles(string dirId, List<Tuple<FileSystemFile, IFileInfo>> files, CancellationToken cancellationToken, object placementTarget = null)
        {
            using (var txn = TransactionManager.CreateTransaction())
            {
                var parameters = new Dictionary<string, string>();
                try
                {
                    foreach (var file in files)
                    {
                        txn.Enqueue(OperationKind.UploadFile, file.Item1.Name, file.Item2.Size, cancellationToken,
                        async (p, ct) =>
                        {
                            var fileStream = await file.Item2.OpenSequentialReadAsync();
                            var newFile = await FileSystem.WriteFileAsync(dirId, file.Item1, fileStream, p, ct);
                            parameters.Add("fileId", FileSystem.GetFileId(dirId, newFile.Id));
                        });
                    }
                    await txn.RunAsync();
                }
                catch (AggregateException exc)
                {
                    string m;
                    if (!GetDuplicatedItemMessage(exc, out m))
                    {
                        m = exc.InnerExceptions.Count == 1 ? FileSystemResources.UploadFileError : string.Format(FileSystemResources.UploadFilesError, exc.InnerExceptions.Count());
                    }
                    AppService.NotifyError(m);
                    throw;
                }
                var message = "";
                if (files.Count == 1)
                {
                    var item = files[0].Item1;
                    message = string.Format(FileSystemResources.UploadFileSucceded, item.Name);
                }
                else
                {
                    parameters.Add("dirId", dirId);
                    message = string.Format(FileSystemResources.UploadFilesSucceded, files.Count());
                }
                AppService.Notify(message, parameters, placementTarget);
            }
        }

        #endregion

        #region delete

        public async Task DeleteDirectoryAsync(FileSystemActionContext context, string questionSingular, string questionPlural, Func<string, Task> afterDirectoryDeleted, CancellationToken cancellationToken)
        {
            string message;
            if (context.IsSingleDirectory)
                message = string.Format(questionSingular ?? FileSystemResources.DeleteFolderQuestion, context.SingleDirectory.Name);
            else
                message = string.Format(questionPlural ?? FileSystemResources.DeleteFoldersQuestion, context.Directories.Count());

            if (await AppService.ShowQuestionAsync(message))
            {
                using (var txn = TransactionManager.CreateTransaction())
                {
                    try
                    {
                        var trashId = await FileSystem.GetTrashId(context.SingleGroup.BaseDirectoryId, cancellationToken);
                        foreach (var d in context.Directories)
                        {
                            var dirId = FileSystem.GetDirectoryId(d.Item1, d.Item2);
                            txn.Enqueue(OperationKind.DeleteDirectory, d.Item3.Name, cancellationToken,
                            async ct =>
                            {
                                bool sendToTrash = !await FileSystem.IsSubDirectory(dirId, trashId, ct);
                                await DeleteDirectoryAsync(dirId, sendToTrash, ct);
                                SelectionManager.RemoveItem(d.Item1, d.Item3);
                                if (afterDirectoryDeleted != null)
                                    await afterDirectoryDeleted(dirId);
                            });
                        }
                        await txn.RunAsync();
                    }
                    catch (AggregateException exc)
                    {
                        string message2 = exc.InnerExceptions.Count() == 1 ? FileSystemResources.DeleteFolderError : string.Format(FileSystemResources.DeleteFoldersError, exc.InnerExceptions.Count());
                        await AppService.ShowErrorAsync(message2);
                        throw new OperationCanceledException();
                    }
                }
            }
            else
            {
                throw new OperationCanceledException();
            }
        }

        public async Task DeleteFilesAsync(FileSystemActionContext context, string questionSingular, string questionPlural, CancellationToken cancellationToken)
        {
            string message;
            if (context.IsSingleFile)
                message = string.Format(questionSingular ?? FileSystemResources.DeleteFileQuestion, context.SingleFile.Name);
            else
                message = string.Format(questionPlural ?? FileSystemResources.DeleteFilesQuestion, context.SingleGroup.Files.Count());

            if (await AppService.ShowQuestionAsync(message))
            {
                using (var txn = TransactionManager.CreateTransaction())
                {
                    try
                    {
                        var trashId = await FileSystem.GetTrashId(context.SingleGroup.BaseDirectoryId, cancellationToken);
                        foreach (var file in context.Files)
                        {
                            txn.Enqueue(OperationKind.DeleteFile, file.Item3.Name, cancellationToken,
                            async ct =>
                            {
                                var fileId = FileSystem.GetFileId(file.Item1, file.Item2);
                                var dirId = await FileSystem.GetFileParentIdAsync(fileId, ct);
                                bool sendToTrash = !await FileSystem.IsSubDirectory(dirId, trashId, ct);
                                await DeleteFileAsync(fileId, sendToTrash, ct);
                                SelectionManager.RemoveItem(file.Item1, file.Item3);
                            });
                        }
                        await txn.RunAsync();
                    }
                    catch (AggregateException exc)
                    {
                        message = exc.InnerExceptions.Count() == 1 ? FileSystemResources.DeleteFileError : string.Format(FileSystemResources.DeleteFilesError, exc.InnerExceptions.Count());
                        await AppService.ShowErrorAsync(message);
                        throw new OperationCanceledException();
                    }
                }
            }
            else
            {
                throw new OperationCanceledException();
            }
        }

        public async Task EmptyTrashAsync(FileSystemActionContext context, CancellationToken cancellationToken)
        {
            if (await AppService.ShowQuestionAsync(FileSystemResources.EmptyTrashQuestion))
            {
                using (var txn = TransactionManager.CreateTransaction())
                {
                    try
                    {
                        var trashPath = context.SingleGroup.BaseDirectoryId;
                        IDataCollection<FileSystemFile> files = null;
                        IDataCollection<FileSystemDirectory> directories = null;
                        await TransactionManager.RunAsync(txn, OperationKind.DownloadData, cancellationToken,
                        async ct =>
                        {
                            files = await FileSystem.GetFilesAsync(trashPath, ct);
                            await files.LoadAsync();
                            directories = await FileSystem.GetDirectoriesAsync(trashPath, ct);
                            await directories.LoadAsync();
                        });
                        foreach (var file in files)
                        {
                            txn.Enqueue(OperationKind.DeleteFile, file.Name, cancellationToken,
                            async ct =>
                            {
                                var fileId = FileSystem.GetFileId(trashPath, file.Id);
                                await DeleteFileAsync(fileId, false, ct);
                            });
                        }
                        foreach (var directory in directories)
                        {
                            txn.Enqueue(OperationKind.DeleteDirectory, directory.Name, cancellationToken,
                            async ct =>
                            {
                                var dirId = FileSystem.GetDirectoryId(trashPath, directory.Id);
                                await FileSystem.DeleteDirectoryAsync(dirId, false, ct);
                            });
                        }
                        await txn.RunAsync();
                    }
                    catch (AggregateException)
                    {
                        //message = exc.InnerExceptions.Count() == 1 ? GlobalResources.DeleteFileError : string.Format(GlobalResources.DeleteFilesError, exc.InnerExceptions.Count());
                        //AppService.ShowErrorAsync(message);
                        throw new OperationCanceledException();
                    }
                }
            }
            else
            {
                throw new OperationCanceledException();
            }
        }

        #endregion

        #region copy

        public async Task<bool> CanCopyAsync(FileSystemActionContext context)
        {
            return await context.Files.AllAsync(f => FileSystem.CanOpenFileAsync(FileSystem.GetFileId(f.Item1, f.Item2), CancellationToken.None));
        }

        public async Task<bool> CanMoveAsync(FileSystemActionContext context)
        {
            return await context.Files.AllAsync(f => FileSystem.CanOpenFileAsync(FileSystem.GetFileId(f.Item1, f.Item2), CancellationToken.None)) &&
                await context.Files.AllAsync(f => FileSystem.CanDeleteFile(FileSystem.GetFileId(f.Item1, f.Item2), CancellationToken.None)) &&
                await context.Directories.AllAsync(d => FileSystem.CanDeleteDirectory(FileSystem.GetDirectoryId(d.Item1, d.Item2), CancellationToken.None));
        }

        public async Task<Tuple<bool, string>> CanMoveToAsync(FileSystemActionContext context, string dirId)
        {
            foreach (var group in context.Groups)
            {
                foreach (var item in group.Items)
                {
                    if (item.IsDirectory)
                    {
                        var sourceDirId = FileSystem.GetDirectoryId(group.BaseDirectoryId, item.Id);
                        if (!await FileSystem.CanMoveDirectory(sourceDirId, dirId, CancellationToken.None))
                        {
                            return new Tuple<bool, string>(false, null);
                        }
                    }
                    else
                    {
                        var sourceFileId = FileSystem.GetFileId(group.BaseDirectoryId, item.Id);
                        if (!await FileSystem.CanMoveFile(sourceFileId, dirId, CancellationToken.None))
                        {
                            return new Tuple<bool, string>(false, null);
                        }
                    }
                }
            }
            return new Tuple<bool, string>(true, null);
        }
        public async Task<Tuple<bool, string>> CanCopyToAsync(FileSystemActionContext context, string dirId)
        {
            //Folder accepts file type?
            //Available space?
            //Maximum file size?
            var mimeTypes = FileSystem.GetAcceptedFileTypes(dirId, false);
            foreach (var group in context.Groups)
            {
                foreach (var item in group.Items)
                {
                    if (item.IsDirectory)
                    {
                        var sourceDirId = FileSystem.GetDirectoryId(group.BaseDirectoryId, item.Id);
                        if (await FileSystem.CanCopyDirectory(sourceDirId, dirId, CancellationToken.None))
                        {
                            continue;
                        }
                        else if (!await FileSystem.CanCreateDirectory(dirId, CancellationToken.None))
                        {
                            return new Tuple<bool, string>(false, GlobalResources.NotSupportedFoldersMessage);
                        }
                    }
                    else
                    {
                        var sourceFileId = FileSystem.GetFileId(group.BaseDirectoryId, item.Id);
                        if (await FileSystem.CanCopyFile(sourceFileId, dirId, CancellationToken.None))
                        {
                            continue;
                        }
                        else if (!ContainsMimeType(mimeTypes, (item as FileSystemFile).ContentType))
                        {
                            return new Tuple<bool, string>(false, GlobalResources.NotSupportedFileTypeMessage);
                        }
                        else if (!await FileSystem.CanWriteFileAsync(dirId, CancellationToken.None) ||
                            !await FileSystem.CanOpenFileAsync(sourceFileId, CancellationToken.None))
                        {
                            return new Tuple<bool, string>(false, GlobalResources.NotSupportedFilesMessage);
                        }
                    }
                }
            }
            return new Tuple<bool, string>(true, null);
        }

        public static bool ContainsMimeType(string[] mimeTypes, string mimeType)
        {
            return mimeTypes.Any(mt => MimeType.Contains(mt, mimeType));
        }

        public async Task CopyAsync(FileSystemActionContext context, string pickedDirId, CancellationToken cancellationToken, bool move, object placementTarget = null)
        {
            using (var txn = TransactionManager.CreateTransaction(4))
            {
                bool keepExistingFiles = false;
                var collisions = await TransactionManager.RunAsync(txn, OperationKind.DownloadData, cancellationToken,
                    (ct) => DetermineCollisions(context.Items, FileSystem, pickedDirId, ct));
                if (collisions.Count > 0)
                {
                    #region message
                    string message;
                    if (collisions.Count() == 1 && collisions.First().Item3.IsDirectory)
                    {
                        message = FileSystemResources.DuplicatedFolderError;
                    }
                    else if (collisions.Count() == 1)
                    {
                        message = FileSystemResources.DuplicatedFileError;
                    }
                    else
                    {
                        message = string.Format(FileSystemResources.DuplicatedItemsError, collisions.Count());
                    }
                    #endregion
                    var selected = await AppService.ShowSelectAsync(message, new string[] { FileSystemResources.KeepExistingFilesMessage, FileSystemResources.RenameFilesMessage }, placementTarget);
                    keepExistingFiles = selected == 0;
                }
                var items = await TransactionManager.RunAsync(txn, OperationKind.DownloadData, cancellationToken,
                    (ct) => CreateCopyPlan(context.Items, pickedDirId, move, keepExistingFiles, ct));
                var directExecItems = items.Where(i => i.Direct).ToList();
                var indirectExecItems = items.Where(i => !i.Direct).ToList();
                var indirectExecFiles = indirectExecItems.Where(i => !i.Item.IsDirectory).ToList();
                var indirectExecDirectories = indirectExecItems.Where(i => i.Item.IsDirectory).ToList();
                var downloadableFiles = (await indirectExecFiles.WhereAsync(async f => await FileSystem.CanOpenFileAsync(f.ItemId, cancellationToken))).ToList();
                #region show confirmation message
                if (indirectExecItems.Count > 0)
                {
                    int filesCount = downloadableFiles.Count();
                    if (filesCount > 0)
                    {
                        string sizeString = "";
                        string question = "";
                        if (downloadableFiles.All(f => f.Item.Size.HasValue))
                        {
                            var totalSize = downloadableFiles.Sum(f => f.Item.Size.Value);
                            sizeString = string.Format(" ({0}) ", ToSizeString(totalSize));
                        }

                        if (filesCount == 1)
                        {
                            question = string.Format(GlobalResources.CopyFileQuestion, sizeString) + "\n\n";
                        }
                        else
                        {
                            question = string.Format(GlobalResources.CopyFilesQuestion, filesCount, sizeString) + "\n\n";
                        }

                        if (indirectExecFiles.Count > downloadableFiles.Count)
                        {
                            //Not all files can be downloaded.
                            //question += GlobalResources.PartialCopyWarning + "\n\n";
                        }

                        //TODO check mime types.

#if WINDOWS_PHONE_APP
                        question += ApplicationResources.InternetChargesWarning + "\n\n";
#endif
                        question += FileSystemResources.ContinueQuestion;
                        if (!await AppService.ShowQuestionAsync(question))
                            throw new OperationCanceledException();
                    }
                }
                #endregion
                string subPath, providerName;
                var targetConnection = (FileSystem as GlobalFileSystem).GetConnection(pickedDirId, out providerName, out subPath);
                var providerExplorerExtensions = targetConnection.Provider.GetExplorerExtensions(this);

                var namesDict = new Dictionary<string, List<string>>();

                #region Creates directory structure, copy & move files
                try
                {
                    foreach (var item in items)
                    {
                        if (item.Item.IsDirectory)
                        {
                            if (item.Direct)
                            {
                                txn.Enqueue(move ? OperationKind.MoveDirectory : OperationKind.CopyDirectory, item.Item.Name, cancellationToken,
                                async ct =>
                                {
                                    var newDirectoryTargetId = item.Parent != null ? item.Parent.Result : (item.TargetDirId ?? pickedDirId);
                                    var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, newDirectoryTargetId, ct);
                                    var newItem = providerExplorerExtensions.CopyDirectoryItem(pickedDirId, item.Item as FileSystemDirectory, usedNames);
                                    usedNames.Add(newItem.Name);
                                    if (move)
                                    {
                                        await MoveDirectoryAsync(item.ItemId, pickedDirId, newItem, cancellationToken);
                                    }
                                    else
                                    {
                                        await CopyDirectoryAsync(pickedDirId, item.ItemId, newItem, cancellationToken);
                                    }
                                });
                            }
                            else if (item.AlreadyCreated)
                            {
                            }
                            else
                            {
                                var operation = txn.Enqueue(OperationKind.CreateDirectory, item.Item.Name, cancellationToken,
                                async ct =>
                                {
                                    await item.WaitForParent();
                                    var newDirectoryTargetId = item.GetTargetDirId() ?? pickedDirId;
                                    var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, newDirectoryTargetId, ct);
                                    var newDirectory = providerExplorerExtensions.CopyDirectoryItem(newDirectoryTargetId, item.Item as FileSystemDirectory, usedNames);
                                    usedNames.Add(newDirectory.Name);
                                    var createdDirectory = await FileSystem.CreateDirectoryAsync(newDirectoryTargetId, newDirectory, ct);
                                    item.Result = FileSystem.GetDirectoryId(newDirectoryTargetId, createdDirectory.Id);
                                });
                                item.Operation = operation;
                            }
                        }
                        else
                        {
                            txn.Enqueue(move ? OperationKind.MoveFile : OperationKind.CopyFile, item.Item.Name, item.Item.Size, cancellationToken,
                            async (p, ct) =>
                            {
                                if (item.Direct)
                                {
                                    var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, pickedDirId, ct);
                                    var newItem = providerExplorerExtensions.CopyFileItem(pickedDirId, item.Item as FileSystemFile, usedNames);
                                    if (move)
                                    {
                                        await MoveFileAsync(item.ItemId, pickedDirId, newItem, ct);
                                    }
                                    else
                                    {
                                        await CopyFileAsync(item.ItemId, pickedDirId, newItem, ct);
                                    }
                                }
                                else
                                {
                                    var file = item.Item as FileSystemFile;
                                    await item.WaitForParent();
                                    var targetDirId = item.GetTargetDirId() ?? pickedDirId;
                                    System.IO.Stream fileStream = null;
                                    BackupStream backupStream = null;
                                    try
                                    {
                                        fileStream = await FileSystem.OpenFileAsync(item.ItemId, ct);
                                        var usedNames = await GetUsedNames(Extensions, FileSystem, namesDict, targetDirId, ct);
                                        var newItem = providerExplorerExtensions.CopyFileItem(targetDirId, file, usedNames);
                                        usedNames.Add(newItem.Name);
                                        var storage = AppService.GetTemporaryStorage();
                                        var freeSpace = await storage.GetFreeSpaceAsync();
                                        bool cacheDownload = item.Item.Size > 10 * 1024 * 1024 && freeSpace > (item.Item.Size * 4);
                                        FileSystemFile uploadedFile;
                                        if (cacheDownload)
                                        {
                                            var filePath = Path.Combine("temp", Path.GetRandomFileName());
                                            var backupFile = await storage.CreateFileAsync(filePath);
                                            var backupFileStream = await backupFile.OpenWriteAsync();
                                            backupStream = new BackupStream(fileStream, backupFileStream,
                                                async (completed) =>
                                                {
                                                    backupFileStream.Dispose();
                                                    await storage.DeleteFileAsync(filePath);
                                                });
                                            var task = backupStream.BufferAllStream(ct);
                                            uploadedFile = await FileSystem.WriteFileAsync(targetDirId, newItem, backupStream, p, ct);
                                        }
                                        else
                                        {
                                            uploadedFile = await FileSystem.WriteFileAsync(targetDirId, newItem, fileStream, p, ct);
                                        }
                                        item.Result = FileSystem.GetFileId(targetDirId, uploadedFile.Id);
                                    }
                                    finally
                                    {
                                        if (backupStream != null)
                                            await backupStream.DisposeAsync();
                                        else
                                            fileStream?.Dispose();
                                    }
                                }
                            });
                        }
                    }
                    await txn.RunAsync();
                }
                catch (AggregateException exc)
                {
                    string m;
                    if (!GetDuplicatedItemMessage(exc, out m))
                    {
                        if (move)
                            m = exc.InnerExceptions.Count == 1 ? FileSystemResources.MoveFileError : string.Format(FileSystemResources.MoveFilesError, exc.InnerExceptions.Count());
                        else
                            m = exc.InnerExceptions.Count == 1 ? FileSystemResources.CopyFileError : string.Format(FileSystemResources.CopyFilesError, exc.InnerExceptions.Count());
                    }
                    AppService.NotifyError(m);
                    throw;
                }
                catch (Exception)
                {
                    throw;
                }
                #endregion
                var parameters = new Dictionary<string, string>();
                var contextPlanItems = items.Where(i => i.ContextItem != null);
                if (contextPlanItems.Count() == 1 && !string.IsNullOrWhiteSpace(contextPlanItems.First().Result))
                {
                    var item = context.Items.FirstOrDefault();
                    if (contextPlanItems.First().Item.IsDirectory)
                        parameters.Add("dirId", contextPlanItems.First().Result);
                    else
                    {
                        parameters.Add("fileId", contextPlanItems.First().Result);
                    }
                }
                else
                {
                    parameters.Add("dirId", pickedDirId);
                }
                AppService.Notify(await GetSuccedMessage(contextPlanItems, move), parameters, placementTarget);
            }
        }

        private SemaphoreSlim _cachedNamesSemaphore = new SemaphoreSlim(1);
        private async Task<List<string>> GetUsedNames(IFileExplorerExtensions extensions, IFileSystemAsync fileSystem, Dictionary<string, List<string>> namesDict, string dirId, CancellationToken ct)
        {
            var usedNames = new List<string>();
            try
            {
                await _cachedNamesSemaphore.WaitAsync();
                if (namesDict.ContainsKey(dirId))
                {
                    usedNames = namesDict[dirId];
                }
                else
                {
                    usedNames.AddRange(await GetUsedNames(fileSystem, dirId, ct));
                    namesDict.Add(dirId, usedNames);
                }
            }
            finally
            {
                _cachedNamesSemaphore.Release();
            }
            return usedNames;
        }

        protected static async Task<string[]> GetUsedNames(IFileSystemAsync fileSystem, string dirId, CancellationToken cancellationToken)
        {
            var names = new List<string>();
            try
            {
                var directories = await fileSystem.GetDirectoriesAsync(dirId, cancellationToken);
                await directories.LoadAsync();
                names.AddRange(directories.Select(d => d.Name));
            }
            catch { }
            try
            {
                var files = await fileSystem.GetFilesAsync(dirId, cancellationToken);
                await files.LoadAsync();
                names.AddRange(files.Select(d => d.Name));
            }
            catch { }
            return names.ToArray();
        }


        #region creates the execution plan

        private async Task<List<Tuple<string, string, FileSystemItem>>> DetermineCollisions(IEnumerable<Tuple<string, string, FileSystemItem>> enumerable, IFileSystemAsync targetFileSystem, string targetDirId, CancellationToken cancellationToken)
        {
            var collitions = new List<Tuple<string, string, FileSystemItem>>();
            foreach (var item in enumerable)
            {
                if (item.Item3.IsDirectory)
                {
                    var existingDirs = await targetFileSystem.GetDirectoriesAsync(targetDirId, cancellationToken);
                    await existingDirs.LoadAsync();
                    if (existingDirs.Any(d => IsSameItem(d, item.Item3)))
                    {
                        collitions.Add(item);
                    }
                }
                else
                {
                    var existingFiles = await targetFileSystem.GetFilesAsync(targetDirId, cancellationToken);
                    await existingFiles.LoadAsync();
                    if (existingFiles.Any(f => IsSameItem(f, item.Item3)))
                    {
                        collitions.Add(item);
                    }
                }
            }
            return collitions;
        }

        private static bool IsSameItem(FileSystemItem item1, FileSystemItem item2)
        {
            if (item1.IsDirectory && item2.IsDirectory)
            {
                return string.Equals(item1.Name, item2.Name, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (!item1.IsDirectory && !item2.IsDirectory)
            {
                var file1 = item1 as FileSystemFile;
                var file2 = item2 as FileSystemFile;
                var item1Name = Path.GetFileNameWithoutExtension(item1.Name);
                var item2Name = Path.GetFileNameWithoutExtension(item2.Name);
                var item1Ext = Path.GetExtension(item1.Name);
                var item2Ext = Path.GetExtension(item2.Name);
                return string.Equals(item1Name, item2Name, StringComparison.CurrentCultureIgnoreCase) &&
                    (string.Equals(item1Ext, item2Ext, StringComparison.CurrentCultureIgnoreCase) || MimeType.Parse(file1.ContentType) == MimeType.Parse(file2.ContentType));
            }
            return false;
        }

        private async Task<List<ExecItem>> CreateCopyPlan(IEnumerable<Tuple<string, string, FileSystemItem>> enumerable, string targetDirId, bool move, bool keepExistingFiles, CancellationToken cancellationToken)
        {
            var result = new List<ExecItem>();
            foreach (var item in enumerable)
            {
                if (item.Item3.IsDirectory)
                {
                    var dirId = FileSystem.GetDirectoryId(item.Item1, item.Item2);
                    if ((!move && await FileSystem.CanCopyDirectory(dirId, targetDirId, cancellationToken)) ||
                        (move && await FileSystem.CanMoveDirectory(dirId, targetDirId, cancellationToken)))
                    {
                        result.Add(new ExecItem(targetDirId, dirId, item.Item3, null, true, item, null));
                    }
                    else
                    {
                        var subPlan = await CreateDownloadDirectoryPlan(keepExistingFiles, FileSystem, targetDirId, FileSystem, item, null, cancellationToken);
                        result.AddRange(subPlan);
                    }
                }
                else
                {
                    var fileId = FileSystem.GetFileId(item.Item1, item.Item2);
                    if ((!move && await FileSystem.CanCopyFile(fileId, targetDirId, cancellationToken)) ||
                        (move && await FileSystem.CanMoveFile(fileId, targetDirId, cancellationToken)))
                    {
                        result.Add(new ExecItem(targetDirId, fileId, item.Item3, null, true, item, null));
                    }
                    else
                    {
                        FileSystemFile existingFile = null;
                        if (keepExistingFiles)
                        {
                            var existingFiles = await FileSystem.GetFilesAsync(targetDirId, cancellationToken);
                            await existingFiles.LoadAsync();
                            existingFile = existingFiles.FirstOrDefault(f => IsSameItem(f, item.Item3));
                        }
                        if (existingFile == null)
                            result.Add(new ExecItem(targetDirId, fileId, item.Item3, null, false, item, null));
                    }
                }
            }
            return result;
        }

        #endregion
        #endregion

        #region properties/update

        public async Task<FileSystemDirectory> DirectoryPropertiesAsync(FileSystemActionContext context, string caption, CancellationToken cancellationToken, object placementTarget)
        {
            var directory = context.SingleDirectory;
            var dirId = FileSystem.GetDirectoryId(context.SingleGroup.BaseDirectoryId, directory.Id);
            var canUpdate = false;
            await TransactionManager.RunAsync(OperationKind.DownloadData, directory.Name, cancellationToken, async ct =>
            {
                canUpdate = await FileSystem.CanUpdateDirectory(dirId, ct);
                directory = await FileSystem.GetDirectoryAsync(dirId, true, ct);
            });
            if (canUpdate)
                directory = Extensions.CopyDirectoryItem(context.SingleGroup.BaseDirectoryId, directory, new string[0]);
            var dirVM = CreateViewModel(context.SingleGroup.BaseDirectoryId, directory);
            dirVM.BeginChanging();
            try
            {
                beggining:
                if (await AppService.ShowItemFormAsync(dirVM,
                    caption ?? FileSystemResources.PropertiesLabel,
                    positiveButton: FileSystemResources.SaveLabel,
                    negativeButton: ApplicationResources.CancelLabel,
                    placementTarget: placementTarget))
                {
                    if (canUpdate)
                    {
                        dirVM.Validate();
                        if (dirVM.HasErrors)
                            goto beggining;
                        try
                        {
                            await TransactionManager.RunAsync(OperationKind.UpdateDirectory, directory.Name, cancellationToken, async ct =>
                            {
                                await FileSystem.UpdateDirectoryAsync(dirId, directory, ct);
                            });
                        }
                        catch (ArgumentNullException argExc)
                        {
                            dirVM.SetError(new ValidationError(FileSystemResources.RequiredLabel), argExc.Message/*ParamName*/);
                            goto beggining;
                        }
                        catch (DuplicatedItemException)
                        {
                            dirVM.SetError(new ValidationError(FileSystemResources.DuplicatedFolderError), "Name");
                            goto beggining;
                        }
                        catch (Exception)
                        {
                            dirVM.SetError(new ValidationError(FileSystemResources.UpdateFolderError), "");
                            goto beggining;
                        }
                    }
                }
                else
                {
                    dirVM.UndoChanges();
                }
            }
            catch (OperationCanceledException)
            {
                dirVM.UndoChanges();
                throw;
            }

            return directory;
        }

        public async Task<FileSystemFile> FilePropertiesAsync(FileSystemActionContext context, string caption, CancellationToken cancellationToken, object placementTarget)
        {
            var file = context.SingleFile;
            var dirId = context.SingleGroup.BaseDirectoryId;
            var fileId = FileSystem.GetFileId(dirId, file.Id);
            var canUpdate = false;
            await TransactionManager.RunAsync(OperationKind.DownloadData, file.Name, cancellationToken, async ct =>
            {
                canUpdate = await FileSystem.CanUpdateFile(fileId, ct);
                file = await FileSystem.GetFileAsync(fileId, true, ct);
            });
            if (canUpdate)
                file = Extensions.CopyFileItem(dirId, file, new string[0]);
            var fileVM = CreateViewModel(dirId, file);
            fileVM.BeginChanging();
            try
            {
                beggining:
                if (await AppService.ShowItemFormAsync(fileVM,
                    caption ?? FileSystemResources.PropertiesLabel,
                    positiveButton: FileSystemResources.SaveLabel,
                    negativeButton: ApplicationResources.CancelLabel,
                    placementTarget: placementTarget))
                {
                    if (canUpdate)
                    {
                        fileVM.Validate();
                        if (fileVM.HasErrors)
                            goto beggining;
                        try
                        {
                            await TransactionManager.RunAsync(OperationKind.UpdateFile, file.Name, cancellationToken, async ct =>
                            {
                                await UpdateFileAsync(fileId, file, ct);
                            });
                        }
                        catch (ArgumentNullException argExc)
                        {
                            fileVM.SetError(new ValidationError(FileSystemResources.RequiredLabel), argExc.Message/*ParamName*/);
                            goto beggining;
                        }
                        catch (DuplicatedItemException)
                        {
                            fileVM.ClearErrors();
                            fileVM.SetError(new ValidationError(FileSystemResources.DuplicatedFileError), "Name");
                            goto beggining;
                        }
                        catch (Exception)
                        {
                            fileVM.ClearErrors();
                            fileVM.SetError(new ValidationError(FileSystemResources.UpdateFileError), "");
                            goto beggining;
                        }
                    }
                }
                else
                {
                    fileVM.UndoChanges();
                }
            }
            catch (OperationCanceledException)
            {
                fileVM.UndoChanges();
                throw;
            }

            return file;
        }

        public async Task<long> GetDirectorySizeAsync(string dirId)
        {
            long size = 0;
            await TransactionManager.RunAsync(OperationKind.DownloadData, "", CancellationToken.None, async ct =>
            {
                size = await GetDirectorySizeAsync(dirId, ct);
            });
            return size;
        }

        private async Task<long> GetDirectorySizeAsync(string dirId, CancellationToken cancellationToken)
        {
            long size = 0;
            var files = await FileSystem.GetFilesAsync(dirId, cancellationToken);
            await files.LoadAsync(cancellationToken: cancellationToken);
            foreach (var file in files)
            {
                size += file.Size ?? 0;
            }
            var directories = await FileSystem.GetDirectoriesAsync(dirId, cancellationToken);
            await directories.LoadAsync(cancellationToken: cancellationToken);
            foreach (var subDir in directories)
            {
                var subDirId = FileSystem.GetDirectoryId(dirId, subDir.Id);
                size += await GetDirectorySizeAsync(subDirId, cancellationToken);
            }
            return size;
        }
        #endregion

        #region sort

        internal bool CanSortByNameAsc()
        {
            return Items.CanSort("Name");
        }

        internal bool CanSortByNameDesc()
        {
            return Items.CanSort("Name", SortDirection.Descending);
        }

        internal bool CanSortBySizeAsc()
        {
            return Items.CanSort("Size");
        }

        internal bool CanSortBySizeDesc()
        {
            return Items.CanSort("Size", SortDirection.Descending);
        }

        internal bool CanSortByDateAsc()
        {
            return Items.CanSort("ModifiedDate") || Items.CanSort("CreatedDate");
        }

        internal bool CanSortByDateDesc()
        {
            return Items.CanSort("ModifiedDate", SortDirection.Descending) || Items.CanSort("CreatedDate", SortDirection.Descending);
        }

        public async Task SortByName()
        {
            await Items.SortAsync("Name");
        }

        public async Task SortByNameDesc()
        {
            await Items.SortAsync("Name", SortDirection.Descending);
        }

        public async Task SortBySize()
        {
            await Items.SortAsync("Size");
        }

        public async Task SortBySizeDesc()
        {
            await Items.SortAsync("Size", SortDirection.Descending);
        }

        public async Task SortByDate()
        {
            if (Items.CanSort("ModifiedDate"))
            {
                await Items.SortAsync("ModifiedDate");
            }
            else
            {
                await Items.SortAsync("CreatedDate");
            }
        }

        public async Task SortByDateDesc()
        {
            if (Items.CanSort("ModifiedDate"))
            {
                await Items.SortAsync("ModifiedDate", SortDirection.Descending);
            }
            else
            {
                await Items.SortAsync("CreatedDate", SortDirection.Descending);
            }
        }

        #endregion

        protected async Task<string> GetSuccedMessage(IEnumerable<ExecItem> items, bool move)
        {
            var message = "";
            if (items.Count() == 1)
            {
                var planItem = items.First();
                if (planItem.Item.IsDirectory)
                {
                    var dir = await FileSystem.GetDirectoryAsync(planItem.Result, false, CancellationToken.None);
                    message = string.Format(move ? FileSystemResources.MoveFolderSucceded : FileSystemResources.CopyFolderSucceded, dir.Name);
                }
                else
                {
                    var file = await FileSystem.GetFileAsync(planItem.Result, false, CancellationToken.None);
                    message = string.Format(move ? FileSystemResources.MoveFileSucceded : FileSystemResources.CopyFileSucceded, file.Name);
                }
            }
            else if (items.All(i => i.Item.IsDirectory))
            {
                message = string.Format(move ? FileSystemResources.MoveFoldersSucceded : FileSystemResources.CopyFoldersSucceded, items.Count());
            }
            else if (items.All(i => !i.Item.IsDirectory))
            {
                message = string.Format(move ? FileSystemResources.MoveFilesSucceded : FileSystemResources.CopyFilesSucceded, items.Count());
            }
            else
            {
                message = string.Format(move ? FileSystemResources.MoveItemsSucceded : FileSystemResources.CopyItemsSucceded, items.Count());
            }
            return message;
        }

        protected string GetDownloadSuccedMessage(IEnumerable<Tuple<string, string, FileSystemItem>> items)
        {
            var message = "";
            if (items.Count() == 1)
            {
                var item = items.First().Item3;
                if (item.IsDirectory)
                    message = string.Format(FileSystemResources.DownloadFolderSucceded, item.Name);
                else
                    message = string.Format(FileSystemResources.DownloadFileSucceded, item.Name);
            }
            else if (items.All(i => i.Item3.IsDirectory))
            {
                message = string.Format(FileSystemResources.DownloadFoldersSucceded, items.Count());
            }
            else if (items.All(i => !i.Item3.IsDirectory))
            {
                message = string.Format(FileSystemResources.DownloadFilesSucceded, items.Count());
            }
            else
            {
                message = string.Format(FileSystemResources.DownloadItemsSucceded, items.Count());
            }
            return message;
        }


        protected static bool GetDuplicatedItemMessage(AggregateException exc, out string message)
        {
            if (exc.InnerExceptions.All(e => e is DuplicatedFileException))
            {
                message = FileSystemResources.DuplicatedFileError;
            }
            else if (exc.InnerExceptions.All(e => e is DuplicatedDirectoryException))
            {
                message = FileSystemResources.DuplicatedFolderError;
            }
            else if (exc.InnerExceptions.All(e => e is DuplicatedItemException))
            {
                message = FileSystemResources.DuplicatedItemError;
            }
            else
            {
                message = null;
            }
            return message != null;

        }

        public static string ToSizeString(long size)
        {
            if (size < 1024)
            {
                return string.Format("{0} bytes", size);
            }
            else if (size < Math.Pow(1024, 2))
            {
                return string.Format("{0:N2} KB", size / (double)1024);
            }
            else if (size < Math.Pow(1024, 3))
            {
                return string.Format("{0:N2} MB", size / Math.Pow(1024, 2));
            }
            else if (size < Math.Pow(1024, 4))
            {
                return string.Format("{0:N2} GB", size / Math.Pow(1024, 3));
            }
            else if (size < Math.Pow(1024, 5))
            {
                return string.Format("{0:N2} TB", size / Math.Pow(1024, 4));
            }
            else //if (size < Math.Pow(1024, 6))
            {
                return string.Format("{0:N2} PB", size / Math.Pow(1024, 5));
            }
        }

        #endregion
    }

    public class ExecItem
    {
        public ExecItem(string targetDirId, string itemId, FileSystemItem item, ExecItem parent, bool direct, Tuple<string, string, FileSystemItem> contextItem, string result)
        {
            TargetDirId = targetDirId;
            ItemId = itemId;
            Item = item;
            Parent = parent;
            Direct = direct;
            ContextItem = contextItem;
            AlreadyCreated = result != null;
            Result = result;
        }
        public string TargetDirId { get; set; }
        public string ItemId { get; set; }
        public Tuple<string, string, FileSystemItem> ContextItem { get; private set; }
        public bool AlreadyCreated { get; private set; }
        public FileSystemItem Item { get; set; }
        public ExecItem Parent { get; set; }
        public bool Direct { get; set; }
        public string Result { get; set; }
        public Operation Operation { get; internal set; }

        internal async Task WaitForParent()
        {
            if (Parent != null)
            {
                if (Parent.Operation != null)
                {
                    await Parent.Operation.RunAsync();
                }
            }
        }

        internal string GetTargetDirId()
        {
            return Parent != null ? Parent.Result : TargetDirId;
        }
    }

    public class ChangingDirectoryEventArgs : AsyncEventArgs
    {
        public ChangingDirectoryEventArgs(string oldDirectory, string newDirectory, bool forward, object placementTarget)
        {
            Forward = forward;
            OldDirectory = oldDirectory;
            NewDirectory = newDirectory;
            PlacementTarget = placementTarget;
        }

        public object PlacementTarget { get; private set; }
        public bool Forward { get; private set; }
        public string OldDirectory { get; private set; }
        public string NewDirectory { get; private set; }
    }

    public class ScrollItemIntoViewAsyncEventArgs : AsyncEventArgs
    {
        public ScrollItemIntoViewAsyncEventArgs(string itemId)
        {
            ItemId = itemId;
        }

        public string ItemId { get; private set; }
    }
}
