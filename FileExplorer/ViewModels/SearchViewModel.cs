using C1.DataCollection;
using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class SearchViewModel : BaseViewModel
    {
        #region fields

        private string _query = "";
        private IDataCollection<SearchItemViewModel> _items;
        private bool _searching = false;
        private AccountDirectory _currentAccount;
        private bool _onlyThisAccountFilterIsChecked = true;

        #endregion

        #region initialization

        public SearchViewModel(FileExplorerViewModel fileExplorer)
        {
            FileExplorer = fileExplorer;
            SearchCommand = new TaskCommand(Search, CanSearch);
            UpdateVisibilities();
        }

        #endregion

        #region object model

        public FileExplorerViewModel FileExplorer { get; private set; }

        public TaskCommand SearchCommand { get; set; }

        public string Query
        {
            get
            {
                return _query;
            }
            set
            {
                if (_query != value)
                {
                    _query = value;
                    OnPropertyChanged();
                    OnPropertyChanged("QuotedQuery");
                    SearchCommand.OnCanExecuteChanged();
                }
            }
        }

        public string QuotedQuery
        {
            get
            {
                return string.Format(@"""{0}""", Query);
            }
        }

        public virtual IDataCollection<SearchItemViewModel> Items
        {
            get
            {
                return _items;
            }
            set
            {
                if (_items != null)
                {
                    _items.CollectionChanged -= OnSearchResultsCollectionChanged;
                }
                _items = value;
                if (_items != null)
                {
                    _items.CollectionChanged += OnSearchResultsCollectionChanged;
                }
                OnPropertyChanged();
            }
        }

        void OnSearchResultsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateVisibilities();
        }

        public bool ResultsVisible { get; private set; }
        public bool NoResultsVisible { get; private set; }
        public bool InvalidQueryVisible { get; private set; }
        public bool ValidQueryVisible { get; private set; }
        public bool IsSearching { get; private set; }

        public AccountDirectory CurrentAccount
        {
            get
            {
                return _currentAccount;
            }
            set
            {
                _currentAccount = value;
                OnPropertyChanged();
                OnPropertyChanged("FilterVisible");
            }
        }

        public virtual bool FilterVisible
        {
            get
            {
                return CurrentAccount != null;
            }
        }

        public bool OnlyThisAccountFilterIsChecked
        {
            get
            {
                return _onlyThisAccountFilterIsChecked;
            }
            set
            {
                _onlyThisAccountFilterIsChecked = value;
                OnPropertyChanged();
                OnPropertyChanged("AllAccountsFilterIsChecked");
                OnPropertyChanged("AccountIconKey");
            }
        }

        public bool AllAccountsFilterIsChecked
        {
            get
            {
                return !_onlyThisAccountFilterIsChecked;
            }
            set
            {
                _onlyThisAccountFilterIsChecked = !value;
                OnPropertyChanged("OnlyThisAccountFilterIsChecked");
                OnPropertyChanged();
                OnPropertyChanged("AccountIconKey");
            }
        }

        public string AccountIconKey
        {
            get
            {
                if (CurrentAccount != null && CurrentAccount.Provider != null && OnlyThisAccountFilterIsChecked)
                {
                    return CurrentAccount.Provider.IconResourceKey;
                }
                else
                {
                    return "WoopitiIcon";
                }
            }
        }

        public string ResultsCount
        {
            get
            {
                return Items != null && Items.Count > 0 ? string.Format("({0})", Items.Count) : "";
            }
        }

        public string CurrentAccountName
        {
            get
            {
                return CurrentAccount != null ? CurrentAccount.Name : "";
            }
        }

        public event EventHandler<AsyncEventArgs> Searching;
        public event EventHandler<AsyncEventArgs> Searched;

        #endregion

        #region labels

        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string SearchLabel
        {
            get
            {
                return FileSystemResources.SearchLabel;
            }
        }

        public string SearchResultsFor
        {
            get
            {
                return SearchResources.SearchResultsFor;
            }
        }

        public string HomeLabel
        {
            get
            {
                return ApplicationResources.HomeLabel;
            }
        }

        public string BackLabel
        {
            get
            {
                return ApplicationResources.BackLabel;
            }
        }

        public string NoSearchResultsMessage
        {
            get
            {
                return SearchResources.NoSearchResultsMessage;
            }
        }

        public string TooShortQueryMessage
        {
            get
            {
                return SearchResources.TooShortQueryMessage;
            }
        }

        public string OnlyThisAccountLabel
        {
            get
            {
                return string.Format(SearchResources.OnlyThisAccountLabel, CurrentAccountName);
            }
        }

        public string AllAccountsLabel
        {
            get
            {
                return SearchResources.AllAccountsLabel;
            }
        }

        public string Message
        {
            get
            {
                return Query.Length == 0 ? TooShortQueryMessage : NoSearchResultsMessage;
            }
        }

        #endregion

        #region implementation

        protected virtual async Task OnSearchingAsync()
        {
            if (Searching != null)
            {
                var e = new AsyncEventArgs();
                Searching(this, e);
                await e.WaitDeferralsAsync();
            }
        }

        protected virtual async Task OnSearchedAsync()
        {
            if (Searched != null)
            {
                var e = new AsyncEventArgs();
                Searched(this, e);
                await e.WaitDeferralsAsync();
            }
        }

        private bool CanSearch(object parameter)
        {
            return Query != null && Query.Length >= 1;
        }

        private async Task Search(object parameter)
        {
            _searching = true;
            //UpdateVisibilities();
            if (!CanSearch(parameter))
            {
                Items = null;
            }
            else
            {
                try
                {
                    await OnSearchingAsync();
                    await FileExplorer.TransactionManager.RunAsync(OperationKind.Search, Query, CancellationToken.None, async (ct) =>
                    {
                        IDataCollection<FileSystemSearchItem> result = null;
                        result = await FileExplorer.FileSystem.SearchAsync(CurrentAccount != null && OnlyThisAccountFilterIsChecked ? CurrentAccount.Id : "", Query, ct);
                        await result.LoadAsync(toIndex: 10);
                        Items = new SearchCollection(this, result);
                    });
                    UpdateVisibilities();
                    await OnSearchedAsync();
                }
                catch
                {
                }
                finally
                {
                    //UpdateVisibilities();
                    //await Items.LoadAsync(50);
                    _searching = false;
                    UpdateVisibilities();
                }
            }
        }

        protected void UpdateVisibilities()
        {
            InvalidQueryVisible = Query.Length == 0;
            ValidQueryVisible = Query.Length > 0;
            ResultsVisible = Query.Length > 0 && Items != null && Items.Count > 0;
            NoResultsVisible = Query.Length > 0 && !_searching && (Items == null || Items.Count == 0);
            IsSearching = Items != null && _searching;
            OnPropertyChanged("InvalidQueryVisible");
            OnPropertyChanged("ValidQueryVisible");
            OnPropertyChanged("ResultsVisible");
            OnPropertyChanged("NoResultsVisible");
            OnPropertyChanged("IsSearching");
            OnPropertyChanged("ResultsCount");
        }

        #endregion

        public async Task ExecuteDefaultAction(SearchItemViewModel searchItem, object originalSource)
        {
            var fileExplorer = FileExplorer;
            var item = searchItem.ItemViewModel;
            if (item.Item.IsDirectory)
            {
                var dirId = FileExplorer.FileSystem.GetDirectoryId(searchItem.Data.DirectoryId, item.Item.Id);
                await FileExplorer.AppService.GoToMain(dirId);
            }
            else
            {
                var actions = await fileExplorer.GetActionsAsync(searchItem.Data.DirectoryId, item);
                var defaultAction = actions.FirstOrDefault(a => a.IsDefault);
                if (defaultAction != null)
                {
                    await fileExplorer.ExecuteAction(defaultAction, originalSource);
                }
            }
        }

        public async Task<List<FileSystemAction>> GetActionsAsync(SearchItemViewModel searchItemVM)
        {
            var searchItem = searchItemVM.Data;
            FileSystemActionContext context;
            if (searchItem.Item.IsDirectory)
                context = new FileSystemActionContext(searchItem.DirectoryId, new FileSystemDirectory[] { searchItem.Item as FileSystemDirectory });
            else
                context = new FileSystemActionContext(searchItem.DirectoryId, new FileSystemFile[] { searchItem.Item as FileSystemFile });

            var actions = (await FileExplorer.GetActionsAsync(searchItem.DirectoryId, searchItemVM.ItemViewModel)).Where(act => act.Category == FileSystemActionCategory.Open).ToList();
            AddOpenContainingDirectory(context, actions);
            return actions;
        }

        private void AddOpenContainingDirectory(FileSystemActionContext context, IList<FileSystemAction> actions)
        {
            if (context.IsSingleFile)
            {
                var open = new FileSystemAction("OpenDirectory",
                    ApplicationResources.OpenContainingFolderLabel,
                    context,
                    async (e, a) =>
                    {
                        await FileExplorer.NavigateTo(e.Context.SingleGroup.BaseDirectoryId, FileExplorer.FileSystem.GetFileId(e.Context.SingleGroup.BaseDirectoryId, e.Context.SingleFile.Id));
                    })
                {
                    Category = FileSystemActionCategory.Open,
                    IsDefault = true,
                };
                actions.Add(open);
            }
        }
    }

    public class SearchItemViewModel
    {
        public SearchViewModel SearchViewModel { get; set; }
        public FileSystemItemViewModel ItemViewModel { get; set; }
        public string ProviderIconKey { get; set; }
        public FileSystemSearchItem Data { get; set; }
        public string Query
        {
            get
            {
                return SearchViewModel.Query;
            }
        }
    }

    public class SearchCollection : TransformList<FileSystemSearchItem, SearchItemViewModel>
    {
        private SearchViewModel _searchViewModel;

        public SearchCollection(SearchViewModel searchViewModel, IReadOnlyList<FileSystemSearchItem> collection)
            : base(collection)
        {
            _searchViewModel = searchViewModel;
        }

        protected override SearchItemViewModel Transform(int index, FileSystemSearchItem item)
        {
            var fileExplorer = _searchViewModel.FileExplorer;
            var fileViewModel = fileExplorer.GetViewModel(item.DirectoryId, item.Item);
            var root = Path.SplitPath(item.DirectoryId).FirstOrDefault();
            var provider = (fileExplorer.FileSystem as GlobalFileSystem).GetConnection(root).Provider;
            return new SearchItemViewModel
            {
                SearchViewModel = _searchViewModel,
                Data = item,
                ItemViewModel = fileViewModel,
                ProviderIconKey = provider.IconResourceKey,
            };
        }
    }
}
