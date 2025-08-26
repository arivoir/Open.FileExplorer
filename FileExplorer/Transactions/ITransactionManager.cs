using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Open.FileExplorer
{
    public interface ITransactionManager
    {
        ObservableCollection<Transaction> Transactions { get; }

        Transaction CreateTransaction(int maxParallelThreads);

        void ReportTransactionChanged(TransactionChange change, Transaction transaction, Operation operation);
    }

    public class TransactionEventArgs : EventArgs
    {
        internal TransactionEventArgs(TransactionChange change, Transaction t, Operation o)
        {
            Change = change;
            Transaction = t;
            Operation = o;
        }

        public TransactionChange Change { get; private set; }
        public Transaction Transaction { get; private set; }
        public Operation Operation { get; private set; }
    }

    public enum TransactionChange
    {
        OperationAdded,
        TransactionEnded,
        TransactionStarted,
        OperationStatusChanged,
        OperationEnded
    }
}
