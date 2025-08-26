using Open.IO;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public abstract class StreamAsync : StreamWrapper
    {
        bool disposed = false;
        SemaphoreSlim _disposeSemaphore = new SemaphoreSlim(1);

        public StreamAsync(Stream stream)
            : base(stream)
        {
        }

        public async Task DisposeAsync()
        {
            await DisposeAsync(CancellationToken.None);
        }

        public async Task DisposeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _disposeSemaphore.WaitAsync();
                if (!disposed)
                {
                    await BeforeDisposing(cancellationToken);
                    await OnDisposing(cancellationToken);
                    disposed = true;
                    await AfterDisposing(cancellationToken);
                }
            }
            finally
            {
                _disposeSemaphore.Release();
            }
        }

        protected virtual Task BeforeDisposing(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected virtual Task OnDisposing(CancellationToken cancellationToken)
        {
            var innerAsyncStream = InnerStream as StreamAsync;
            if (innerAsyncStream != null)
            {
                return innerAsyncStream.DisposeAsync(cancellationToken);
            }
            else
            {
                base.Dispose();
            }
            return Task.FromResult(true);
        }

        protected virtual Task AfterDisposing(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected sealed override void Dispose(bool disposing)
        {
            var task = DisposeAsync(CancellationToken.None);
        }
    }
}
