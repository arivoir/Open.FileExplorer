using Open.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace Open.FileExplorer
{
    //[DebuggerNonUserCode()]
    //[CompilerGenerated()]
    public class Transaction : IDisposable
    {
        #region ** fields

        private SemaphoreSlim _semaphore;

        #endregion

        #region ** initialization

        public Transaction(ITransactionManager transactionManager, int maxParallelThreads = 10)
        {
            TransactionManager = transactionManager;
            CancellationSource = new CancellationTokenSource();
            Operations = new ObservableCollection<Operation>();
            _semaphore = new SemaphoreSlim(maxParallelThreads);
        }

        #endregion

        #region ** object model

        public ITransactionManager TransactionManager { get; private set; }

        internal CancellationTokenSource CancellationSource { get; set; }

        public ObservableCollection<Operation> Operations { get; private set; }

        public bool IsCompleted { get; private set; }

        public bool IsActive
        {
            get
            {
                return Operations.Count > 0 && Operations.Any(o => !o.IsCompleted);
            }
        }

        public double? ProgressValue
        {
            get
            {
                if (Operations.Any(o => o.Kind == OperationKind.DownloadFile || o.Kind == OperationKind.CopyFile || o.Kind == OperationKind.UploadFile))
                {
                    var weightedOperations = Operations.Where(o => o.GetWeight().HasValue);
                    var startedOperations = weightedOperations.Where(o => o.IsStarted && o.ProgressValue != null).ToList();
                    var startedCoef = (double)startedOperations.Count / (double)Operations.Count;
                    if (startedCoef > 0.05)
                    {
                        var downloadOffset = weightedOperations.Sum(o => o.GetOffset());
                        var downloadLength = weightedOperations.Sum(o => o.GetWeight());
                        return (double)downloadOffset / (double)downloadLength;
                    }
                }
                else
                {
                    var downloadThumbOperations = Operations.Where(o => o.Kind == OperationKind.DownloadThumbnail).ToList();
                    if (downloadThumbOperations.Count > 1)
                    {
                        return (double)downloadThumbOperations.Count(o => o.IsCompleted) / (double)downloadThumbOperations.Count;
                    }
                }
                return null;
            }
        }

        #endregion

        #region ** implementation

        public bool CanBeCanceled
        {
            get
            {
                return Operations.Any(o => o.CanBeCanceled);
            }
        }

        public void Cancel()
        {
            CancellationSource.Cancel();
        }

        internal async Task WaitTransactionAsync()
        {
            await _semaphore.WaitAsync();
        }

        internal void ReleaseTransaction()
        {
            _semaphore.Release();
        }

        public async Task RunAsync()
        {
            var nonQueuedOperations = Operations.Where(o => !o.IsQueued).ToList();
            foreach (var o in nonQueuedOperations)
            {
                try
                {
                    var task = o.StartAsync();
                }
                catch { }
            }
            var nonCompletedOperations = Operations.Where(o => !o.IsCompleted).ToList();
            Func<Operation, Task> runOperation = async o =>
            {
                try
                {
                    await o.RunAsync();
                }
                catch { }
            };
            var tasks = new List<Task>();
            foreach (var o in nonCompletedOperations)
            {
                tasks.Add(runOperation(o));
            }
            await Task.WhenAll(tasks);
            if (CancellationSource.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
            else
            {
                var faulted = Operations.Where(o => o.IsFaulted);
                if (faulted.Count() > 0)
                {
                    throw new AggregateException(faulted.Select(o => o.Exception.InnerException));
                }
                var canceled = Operations.Where(o => o.IsCanceled);
                if (canceled.Count() > 0)
                {
                    throw new OperationCanceledException();
                }
            }
        }

        public Operation Enqueue(OperationKind kind, CancellationToken cancellationToken, Func<CancellationToken, Task> action)
        {
            return Enqueue(kind, "", cancellationToken, action);
        }

        public Operation Enqueue(OperationKind kind, string description, CancellationToken cancellationToken, Func<CancellationToken, Task> action)
        {
            var operation = new Operation(this, kind, description, null, CancellationToken.None, action);
            Enqueue(operation);
            return operation;
        }


        public Operation Enqueue(OperationKind kind, string description, long? weight, CancellationToken cancellationToken, Func<IProgress<StreamProgress>, CancellationToken, Task> action)
        {
            var operation = new OperationWithProgress(this, kind, description, weight, cancellationToken, action);
            Enqueue(operation);
            return operation;
        }

        public Operation Enqueue(OperationKind kind, string description, long? weight, Func<IProgress<StreamProgress>, CancellationToken, Task> action)
        {
            var operation = new OperationWithProgress(this, kind, description, weight, CancellationToken.None, action);
            Enqueue(operation);
            return operation;
        }

        private void Enqueue(Operation operation)
        {
            Operations.Add(operation);
            RaiseStatusChanged(TransactionChange.OperationAdded, null);
        }

        public void Dispose()
        {
            IsCompleted = true;
            RaiseStatusChanged(TransactionChange.TransactionEnded, null);
        }

        internal void RaiseStatusChanged(TransactionChange change, Operation operation)
        {
            TransactionManager.ReportTransactionChanged(change, this, operation);
        }

        #endregion

    }

    #region ** operations

    public class Operation
    {
        private Func<CancellationToken, Task> _operation;
        private SemaphoreSlim _startSemaphore = new SemaphoreSlim(1);

        protected Operation(Transaction transaction, OperationKind kind, string description, long? weight, CancellationToken cancellationToken)
        {
            Debug.Assert(transaction != null);
            Transaction = transaction;
            Kind = kind;
            Description = description;
            Weight = weight;
            CancellationSource = new CancellationTokenSource();
            CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(Transaction.CancellationSource.Token, CancellationSource.Token, cancellationToken).Token;
        }

        public Operation(Transaction transaction, OperationKind kind, string description, long? weight, CancellationToken cancellationToken, Func<CancellationToken, Task> operation)
            : this(transaction, kind, description, weight, cancellationToken)
        {
            _operation = operation;
        }

        public Transaction Transaction { get; private set; }
        public OperationKind Kind { get; private set; }
        public string Description { get; private set; }
        public long? Weight { get; private set; }
        //public IProgress<double> Progress { get; private set; }
        protected CancellationToken CancellationToken { get; set; }
        private CancellationTokenSource CancellationSource { get; set; }
        private Task Task { get; set; }
        public bool IsQueued { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsCompleted { get; private set; } //{ get { return CancellationToken.IsCancellationRequested || (IsStarted && Task.IsCompleted); } }
        public bool IsCanceled { get { return CancellationToken.IsCancellationRequested || (IsStarted && Task.IsCanceled); } }
        public bool IsFaulted { get { return IsStarted && Task.IsFaulted; } }
        public bool IsRunning { get { return !IsCanceled && IsStarted && !IsCompleted; } }

        public Exception Exception { get { return IsStarted ? Task.Exception : null; } }

        StreamProgress _progressValue;
        public StreamProgress ProgressValue
        {
            get { return _progressValue; }
            protected set
            {
                _progressValue = value;
                Transaction.RaiseStatusChanged(TransactionChange.OperationStatusChanged, this);
            }
        }


        internal async Task StartAsync()
        {
            if (!IsStarted && !IsCanceled)
            {
                try
                {
                    await _startSemaphore.WaitAsync();
                    if (!IsQueued)
                    {
                        IsQueued = true;
                        await Transaction.WaitTransactionAsync();
                        if (!IsCanceled)
                        {
                            IsStarted = true;
                            Task = GetOperationTaskOverride();
                            Transaction.RaiseStatusChanged(TransactionChange.TransactionStarted, this);
                        }
                    }
                }
                catch
                {
                    Transaction.RaiseStatusChanged(TransactionChange.OperationEnded, this);
                }
                finally
                {
                    _startSemaphore.Release();
                }
            }
        }

        internal async Task<T> RunAsync<T>()
        {
            await RunAsync();
            return (Task as Task<T>).Result;
        }

        internal async Task RunAsync()
        {
            await StartAsync();
            try
            {
                if (!IsCanceled && !IsCompleted)
                    await Task;
            }
            finally
            {
                if (!IsCompleted)
                {
                    IsCompleted = true;
                    if (IsQueued)
                    {
                        Transaction.ReleaseTransaction();
                    }
                    Transaction.RaiseStatusChanged(TransactionChange.OperationEnded, this);
                }
            }
        }

        protected virtual Task GetOperationTaskOverride()
        {
            if (_operation != null)
            {
                return _operation(CancellationToken);
            }
            return Task.FromResult(true);
        }

        public bool CanBeCanceled
        {
            get
            {
                return !IsCompleted && !IsCanceled && !IsFaulted;
            }
        }

        public void Cancel()
        {
            CancellationSource.Cancel();
        }

        internal long? GetWeight()
        {
            return Weight ?? (ProgressValue != null ? ProgressValue.TotalBytes : (long?)null);
        }

        internal long GetOffset()
        {
            return IsCompleted ? (GetWeight() ?? 0) : (ProgressValue != null ? ProgressValue.Bytes : 0);
        }
    }

    internal class OperationWithProgress : Operation
    {
        private Func<IProgress<StreamProgress>, CancellationToken, Task> _operation;

        public OperationWithProgress(Transaction transaction, OperationKind kind, string description, long? weight, CancellationToken cancellationToken, Func<IProgress<StreamProgress>, CancellationToken, Task> operation)
            : base(transaction, kind, description, weight, cancellationToken)
        {
            _operation = operation;
        }

        protected override async Task GetOperationTaskOverride()
        {
            if (_operation != null)
            {
                await _operation(new Progress<StreamProgress>(percentage => { ProgressValue = percentage; }), CancellationToken);
            }
        }
    }

    #endregion

    public enum OperationKind
    {
        OpenDirectory,
        DownloadData,
        DownloadFile,
        DownloadThumbnail,
        UploadFile,
        CreateDirectory,
        UpdateFile,
        UpdateDirectory,
        Loading,
        DeleteFile,
        DeleteDirectory,
        CopyDirectory,
        CopyFile,
        MoveFile,
        MoveDirectory,
        Search,
    }

}
