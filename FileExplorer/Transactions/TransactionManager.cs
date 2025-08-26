using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class TransactionManager : ITransactionManager
    {
        public TransactionManager()
        {
            Transactions = new ObservableCollection<Transaction>();
        }

        public ObservableCollection<Transaction> Transactions { get; private set; }

        public Transaction CreateTransaction(int maxParallelThreads = 10)
        {
            var transaction = new Transaction(this, maxParallelThreads);
            Transactions.Add(transaction);
            return transaction;
        }

        public void ReportTransactionChanged(TransactionChange change, Transaction transaction, Operation operation)
        {
            TransactionChanged?.Invoke(this, new TransactionEventArgs(change, transaction, operation));
            if (transaction.IsCompleted)
            {
                Transactions.Remove(transaction);
            }
        }

        internal async Task RunAsync(OperationKind kind, string description, CancellationToken cancellationToken, Func<CancellationToken, Task> action)
        {
            using (var txn = CreateTransaction(1))
            {
                await RunAsync(txn, kind, cancellationToken, action);
            }
        }

        internal async Task<T> RunAsync<T>(OperationKind kind, string description, CancellationToken cancellationToken, Func<CancellationToken, Task<T>> action)
        {
            using (var txn = CreateTransaction(1))
            {
                return await RunAsync<T>(txn, kind, cancellationToken, action);
            }
        }

        internal async Task RunAsync(Transaction txn, OperationKind kind, CancellationToken cancellationToken, Func<CancellationToken, Task> action)
        {
            var operation = txn.Enqueue(kind, cancellationToken, action);
            await operation.RunAsync();
        }

        internal Task<T> RunAsync<T>(Transaction txn, OperationKind kind, CancellationToken cancellationToken, Func<CancellationToken, Task<T>> action)
        {
            var operation = txn.Enqueue(kind, cancellationToken, action);
            return operation.RunAsync<T>();
        }

        public event EventHandler<TransactionEventArgs> TransactionChanged;
    }
}
