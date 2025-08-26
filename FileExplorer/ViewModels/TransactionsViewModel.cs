using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class TransactionsViewModel : BaseViewModel
    {
        #region ** fields

        private WeakReference<List<TransactionViewModel>> _transactions;

        #endregion

        #region ** initialization

        public TransactionsViewModel(FileExplorerViewModel fileExplorer)
        {
            FileExplorer = fileExplorer;
            FileExplorer.TransactionManager.TransactionChanged += OnTransactionChanged;
        }

        #endregion

        #region ** object model

        public FileExplorerViewModel FileExplorer { get; private set; }

        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string NoPendingActionsMessage
        {
            get
            {
                return ProgressResources.NoPendingActionsMesssage;
            }
        }

        public bool NoPendingActionsVisible
        {
            get
            {
                return ActiveTransactions.Count == 0;
            }
        }

        public IList<Transaction> ActiveTransactions
        {
            get
            {
                return FileExplorer.Transactions.Where(t => t.IsActive).ToList();
            }
        }

        public virtual IList<TransactionViewModel> Transactions
        {
            get
            {
                var list = ActiveTransactions.Select(t => CreateTransactionViewModel(t)).ToList();
                _transactions = new WeakReference<List<TransactionViewModel>>(list);
                return list;
            }
        }

        protected virtual TransactionViewModel CreateTransactionViewModel(Transaction t)
        {
            return new TransactionViewModel(t);
        }

        private void OnTransactionChanged(object sender, TransactionEventArgs e)
        {
            var list = _transactions.GetTarget();
            if (list != null)
            {
                var txn = list.FirstOrDefault(tvm => tvm.Transaction == e.Transaction);
                if (txn != null)
                {
                    txn.OnTransactionChanged(e);
                }
            }
        }

        #endregion
    }

    public class TransactionViewModel : BaseViewModel
    {
        #region ** fields

        private TransformList<Operation, OperationViewModel> _operations;

        #endregion

        #region ** initialization

        public TransactionViewModel(Transaction transaction)
        {
            Transaction = transaction;
            CancelCommand = new TaskCommand(Cancel, CanCancel);
        }

        private bool CanCancel(object arg)
        {
            return Transaction != null && Transaction.CanBeCanceled;
        }

        private Task Cancel(object arg)
        {
            Transaction.Cancel();
            return Task.FromResult(true);
        }

        #endregion

        #region ** object model

        public Transaction Transaction { get; set; }
        public TaskCommand CancelCommand { get; protected set; }

        public bool CancelButtonVisible
        {
            get
            {
                return Operations.Count > 1;
            }
        }

        public virtual IReadOnlyList<OperationViewModel> Operations
        {
            get
            {
                if (_operations == null)
                {
                    _operations = new TransformList<Operation, OperationViewModel>((Transaction != null ? Transaction.Operations : new ObservableCollection<Operation>()), o => CreateOperationViewModel(o), ovm => (ovm as OperationViewModel).Operation, true);
                }
                return _operations;
            }
        }

        protected virtual OperationViewModel CreateOperationViewModel(Operation o)
        {
            return new OperationViewModel(o);
        }

        public virtual string Description
        {
            get
            {
                var operations = Operations.Select(o => (o as OperationViewModel).Operation);
                return GetPendingActionsMessage(new Transaction[] { Transaction }, false);
            }
        }

        public double? ProgressValue
        {
            get
            {
                return Transaction != null ? Transaction.ProgressValue : (double?)null;
            }
        }

        public bool ProgressVisible
        {
            get
            {
                return Transaction.IsActive && Transaction.ProgressValue.HasValue;
            }
        }

        #endregion

        #region ** implementation

        public static string GetPendingActionsMessage(IEnumerable<Transaction> transactions, bool filterActive = true)
        {
            var operations = transactions.SelectMany(t => t.Operations.Where(o => !filterActive || !o.IsCompleted));
            return GetPendingActionsMessage(operations);
        }

        public static string GetPendingActionsMessage(IEnumerable<Operation> operations)
        {
            if (operations.Count() == 0)
                return "";

            var categories = operations.Select(o => o.Kind).Distinct();
            if (categories.Count() == 1)
            {
                var category = categories.First();
                var operationsCount = operations.Count();
                var description = operations.First().Description;
                var size = "";
                if (operationsCount == 1)
                {

                    var operation = operations.First();
                    if (operation.Weight.HasValue)
                    {
                        size = " " + FileExplorerViewModel.ToSizeString(operation.Weight.Value);
                    }
                }
                switch (category)
                {
                    case OperationKind.Loading:
                        if (operationsCount > 1)
                        {
                            return ProgressResources.LoadingMessage;
                        }
                        else
                        {
                            return string.Format(ProgressResources.LoadingItemMessage, description);
                        }
                    default:
                    case OperationKind.OpenDirectory:
                        return string.Format(ProgressResources.OpeningFolderMessage, description);
                    case OperationKind.DownloadData:
                        return ProgressResources.DownloadingDataMessage;
                    case OperationKind.Search:
                        return string.Format(ProgressResources.SearchingMessage, description);
                    case OperationKind.DownloadThumbnail:
                        return ProgressResources.DownloadingThumbnailsMessage;
                    case OperationKind.DownloadFile:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.DownloadingFilesMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.DownloadingFileMessage, description) + size;
                        }
                    case OperationKind.UploadFile:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.UploadingFilesMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.UploadingFileMessage, description) + size;
                        }
                    case OperationKind.CreateDirectory:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.CreatingFoldersMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.CreatingFolderMessage, description);
                        }
                    case OperationKind.UpdateFile:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.UpdatingFilesMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.UpdatingFileMessage, description);
                        }
                    case OperationKind.UpdateDirectory:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.UpdatingFoldersMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.UpdatingFolderMessage, description);
                        }
                    case OperationKind.DeleteFile:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.DeletingFilesMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.DeletingFileMessage, description);
                        }
                    case OperationKind.DeleteDirectory:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.DeletingFoldersMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.DeletingFolderMessage, description);
                        }
                    case OperationKind.CopyDirectory:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.CopyingFoldersMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.CopyingFolderMessage, description) + size;
                        }
                    case OperationKind.CopyFile:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.CopyingFilesMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.CopyingFileMessage, description) + size;
                        }
                    case OperationKind.MoveFile:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.MovingFilesMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.MovingFileMessage, description) + size;
                        }
                    case OperationKind.MoveDirectory:
                        if (operationsCount > 1)
                        {
                            return string.Format(ProgressResources.MovingFoldersMessage, operationsCount);
                        }
                        else
                        {
                            return string.Format(ProgressResources.MovingFolderMessage, description) + size;
                        }
                }
            }
            else
            {
                return string.Format(ProgressResources.PendingActionsMessage, operations.Count());
            }
        }


        internal static double? GetPendingActionsProgress(IEnumerable<Transaction> transactions)
        {
            var activeTransactions = transactions.Where(t => t.IsActive).ToList();
            if (activeTransactions.Count() == 1)
            {
                var transaction = activeTransactions.First();
                return transaction.ProgressValue;
            }
            return null;
        }

        #endregion

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private bool _needsUpdate = false;

        internal async void OnTransactionChanged(TransactionEventArgs e)
        {
            if (e.Change != TransactionChange.OperationStatusChanged)
            {
                CancelCommand.OnCanExecuteChanged();
            }
            if (e.Operation != null && _operations != null)
            {
                var ope = _operations.GetLoadedItems().FirstOrDefault(ovm => ovm.Item.Operation == e.Operation);
                if (ope != null)
                {
                    ope.Item.OnTransactionChanged(e);
                }
            }
            try
            {
                _needsUpdate = true;
                await _semaphore.WaitAsync();
                if (_needsUpdate)
                {
                    await Task.Delay(400);
                    OnPropertyChanged("ProgressValue");
                    OnPropertyChanged("ProgressVisible");
                    _needsUpdate = true;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

    }

    public class OperationViewModel : BaseViewModel
    {
        #region ** initialization

        public OperationViewModel(Operation operation)
        {
            Operation = operation;
            CancelCommand = new TaskCommand(Cancel, CanCancel);
        }

        #endregion

        #region ** object model

        public Operation Operation { get; private set; }
        public TaskCommand CancelCommand { get; protected set; }

        public virtual string Description
        {
            get
            {
                return GetOperationStatus();
            }
        }

        public string DescriptionForeground
        {
            get
            {
                return Operation != null && Operation.IsCompleted ? "#99FFFFFF" : (string)null;
            }
        }

        public virtual FileSystemItemViewModel ItemViewModel
        {
            get
            {
                return null;
            }
        }

        #endregion

        #region ** implementation

        public virtual Task<string> GetPathAsync()
        {
            return Task.FromResult("");
        }

        public virtual Task<string> GetDescriptionAsync()
        {
            return Task.FromResult(TransactionViewModel.GetPendingActionsMessage(new Operation[] { Operation }));
        }

        private bool CanCancel(object arg)
        {
            return Operation != null && Operation.CanBeCanceled;
        }

        private Task Cancel(object arg)
        {
            Operation.Cancel();
            return Task.FromResult(true);
        }

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private bool _needsUpdate = false;
        internal async void OnTransactionChanged(TransactionEventArgs e)
        {
            if (e.Change == TransactionChange.OperationAdded || e.Change == TransactionChange.OperationEnded)
            {
                OnPropertyChanged("DescriptionForeground");
                CancelCommand.OnCanExecuteChanged();
                OnPropertyChanged("CancelButtonVisible");
            }
            if (e.Change == TransactionChange.OperationStatusChanged)
            {
                try
                {
                    _needsUpdate = true;
                    await _semaphore.WaitAsync();
                    if (_needsUpdate)
                    {
                        await Task.Delay(400);
                        OnPropertyChanged("Description");
                        _needsUpdate = true;
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        private string GetOperationStatus()
        {
            bool active = Operation.IsRunning;
            var size = "";
            if (Operation.Weight.HasValue)
            {
                if (active && Operation.ProgressValue != null)
                {
                    var progress = Operation.ProgressValue;
                    size = " " + string.Format(ApplicationResources.SizeProgressMessage, FileExplorerViewModel.ToSizeString(progress.Bytes), FileExplorerViewModel.ToSizeString(progress.TotalBytes.Value));
                }
                else
                {
                    size = " " + FileExplorerViewModel.ToSizeString(Operation.Weight.Value);
                }
            }
            var status = "";
            if (!active)
            {
                if (Operation.IsCanceled)
                    status = " (" + ProgressResources.OperationCanceledMessage + ")";
                else if (!Operation.IsStarted)
                    status = " (" + ProgressResources.OperationNotStartedMessage + ")";
                else if (Operation.IsFaulted)
                    status = " (" + ProgressResources.OperationFaultedMessage + ")";
                else if (Operation.IsCompleted)
                    status = " (" + ProgressResources.OperationCompletedMessage + ")";
            }
            switch (Operation.Kind)
            {
                case OperationKind.Loading:
                    return string.Format(ProgressResources.LoadingItemMessage, Operation.Description) + status;
                default:
                case OperationKind.OpenDirectory:
                    return string.Format(ProgressResources.OpeningFolderMessage, Operation.Description) + status;
                case OperationKind.DownloadData:
                    return ProgressResources.DownloadingDataMessage;
                case OperationKind.Search:
                    return string.Format(ProgressResources.SearchingMessage, Operation.Description) + status;
                case OperationKind.DownloadThumbnail:
                    return string.Format(active ? ProgressResources.DownloadingThumbnailMessage : ProgressResources.DownloadThumbnailMessage, Operation.Description) + size + status;
                case OperationKind.DownloadFile:
                    return string.Format(active ? ProgressResources.DownloadingFileMessage : ProgressResources.DownloadFileMessage, Operation.Description) + size + status;
                case OperationKind.UploadFile:
                    return string.Format(active ? ProgressResources.UploadingFileMessage : ProgressResources.UploadFileMessage, Operation.Description) + size + status;
                case OperationKind.CreateDirectory:
                    return string.Format(active ? ProgressResources.CreatingFolderMessage : ProgressResources.CreateFolderMessage, Operation.Description) + status;
                case OperationKind.UpdateFile:
                    return string.Format(active ? ProgressResources.UpdatingFileMessage : ProgressResources.UpdateFileMessage, Operation.Description) + status;
                case OperationKind.UpdateDirectory:
                    return string.Format(active ? ProgressResources.UpdatingFolderMessage : ProgressResources.UpdateFolderMessage, Operation.Description) + status;
                case OperationKind.DeleteFile:
                    return string.Format(active ? ProgressResources.DeletingFileMessage : ProgressResources.DeleteFileMessage, Operation.Description) + status;
                case OperationKind.DeleteDirectory:
                    return string.Format(active ? ProgressResources.DeletingFolderMessage : ProgressResources.DeleteFolderMessage, Operation.Description) + status;
                case OperationKind.CopyDirectory:
                    return string.Format(active ? ProgressResources.CopyingFolderMessage : ProgressResources.CopyFolderMessage, Operation.Description) + size + status;
                case OperationKind.CopyFile:
                    return string.Format(active ? ProgressResources.CopyingFileMessage : ProgressResources.CopyFileMessage, Operation.Description) + size + status;
                case OperationKind.MoveFile:
                    return string.Format(active ? ProgressResources.MovingFileMessage : ProgressResources.MoveFileMessage, Operation.Description) + size + status;
                case OperationKind.MoveDirectory:
                    return string.Format(active ? ProgressResources.MovingFolderMessage : ProgressResources.MoveFolderMessage, Operation.Description) + size + status;
            }

        }
        #endregion
    }
}
