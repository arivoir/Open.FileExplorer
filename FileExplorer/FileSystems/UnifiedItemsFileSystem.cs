using C1.DataCollection;
using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public abstract class UnifiedItemsFileSystem : AuthenticatedFileSystem
    {
        #region ** fields

        private Dictionary<string, Task<IList<FileSystemItem>>> _runningOperations = new Dictionary<string, Task<IList<FileSystemItem>>>();

        #endregion

        public virtual string[] AllowedDirectorySortFields => new string[] { "Name", "Size", "ModifiedDate", "CreatedDate" };
        public virtual string[] AllowedFileSortFields => new string[] { "Name", "Size", "ModifiedDate", "CreatedDate" };

        protected sealed override async Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            try
            {
                var items = await GetItemsTransaction(dirId, cancellationToken);
                return new CustomSortCollectionView<FileSystemDirectory>(items.OfType<FileSystemDirectory>().ToList(), AllowedDirectorySortFields);
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }

        }

        protected sealed override async Task<IDataCollection<FileSystemFile>> GetFilesAsyncOverride(string dirId, CancellationToken cancellationToken)
        {
            try
            {
                var items = await GetItemsTransaction(dirId, cancellationToken);
                return new CustomSortCollectionView<FileSystemFile>(items.OfType<FileSystemFile>().ToList(), AllowedFileSortFields);
            }
            catch (Exception exc) { throw await ProcessExceptionAsync(exc); }
        }

        private async Task<IList<FileSystemItem>> GetItemsTransaction(string dirId, CancellationToken cancellationToken)
        {
            Task<IList<FileSystemItem>> operation;
            if (_runningOperations.ContainsKey(dirId))
            {
                operation = _runningOperations[dirId];
            }
            else
            {
                operation = GetItemsAsync(dirId, cancellationToken);
                _runningOperations.Add(dirId, operation);
            }
            try
            {
                var items = await operation;
                if (items is IDataCollection<FileSystemItem>)
                {
                    await (items as IDataCollection<FileSystemItem>).LoadAsync();
                }
                return items;
            }
            finally
            {
                _runningOperations.Remove(dirId);
            }
        }

        protected virtual Task<IList<FileSystemItem>> GetItemsAsync(string dirId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IList<FileSystemItem>>(new FileSystemItem[0]);
        }

        protected override Task RefreshAsyncOverride(string dirId = null)
        {
            if (dirId != null)
            {
                if (_runningOperations.ContainsKey(dirId))
                    _runningOperations.Remove(dirId);
            }
            else
            {
                _runningOperations.Clear();
            }
            return Task.FromResult(true);
        }
    }
}
