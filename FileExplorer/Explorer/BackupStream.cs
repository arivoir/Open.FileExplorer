using Open.FileSystemAsync;
using Open.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class BackupStream : StreamAsync
    {

        private const int internalBufferSize = 65536;
        private byte[] internalBuffer = new byte[internalBufferSize];
        private long _position = 0;
        private long? _streamLength;
        private Stream _backupStream;
        private Func<bool, Task> _onDisposed;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private bool _sourceClosed = false;
        private bool _fullBackup = false;

        public BackupStream(Stream stream, Stream backupStream, Func<bool, Task> onDisposed = null)
            : base(stream)
        {
            if (backupStream == null)
                throw new ArgumentNullException(nameof(backupStream));
            if (!backupStream.CanSeek)
                throw new ArgumentException(nameof(backupStream) + " must be seekable.", nameof(backupStream));
            if (!backupStream.CanWrite)
                throw new ArgumentException(nameof(backupStream) + " must be writeable.", nameof(backupStream));

            _streamLength = stream.GetLength();
            _backupStream = backupStream;
            _onDisposed = onDisposed;
        }

        public override bool CanSeek
        {
            get
            {
                return _streamLength.HasValue;
            }
        }

        public override long Position
        {
            get
            {
                return _position;
            }

            set
            {
                if (!_streamLength.HasValue)
                    throw new Exception("Position can not be set because stream length is unknown.");
                if (value < 0 || value > _streamLength.Value)
                    throw new ArgumentOutOfRangeException();
                _position = value;
            }
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin != SeekOrigin.Begin)
                throw new Exception("Only Begin is supported");
            Position = offset;
            return offset;
        }

        public async Task BufferAllStream(CancellationToken cancellationToken)
        {
            while (await BufferNextBlock(cancellationToken)) { }

        }

        private async Task<bool> BufferNextBlock(CancellationToken cancellationToken)
        {
            bool readSomething = true;
            if (_sourceClosed)
                return false;
            try
            {
                await _semaphore.WaitAsync();
                if (_sourceClosed)
                    return false;
                var readBytes = await base.ReadAsync(internalBuffer, 0, internalBufferSize, cancellationToken);
                if (readBytes > 0)
                {
                    _backupStream.Seek(_backupStream.Length, SeekOrigin.Begin);
                    await _backupStream.WriteAsync(internalBuffer, 0, readBytes);
                }
                else
                {
                    readSomething = false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
            if (!readSomething)
                await CloseSourceStream(cancellationToken);
            return readSomething;
        }

        private async Task<long> GetBackupLength()
        {
            try
            {
                await _semaphore.WaitAsync();
                return _backupStream.Length;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int readBytes = 0;

            while (await GetBackupLength() < _position + count)
            {
                if (!await BufferNextBlock(cancellationToken))
                    break;
            }

            try
            {
                await _semaphore.WaitAsync();
                _backupStream.Seek(_position, SeekOrigin.Begin);
                readBytes = await _backupStream.ReadAsync(buffer, offset, count, cancellationToken);
                _position += readBytes;
            }
            finally
            {
                _semaphore.Release();
            }
            return readBytes;
        }

        private async Task CloseSourceStream(CancellationToken cancellationToken)
        {
            if (!_sourceClosed)
            {
                _sourceClosed = true;
                try
                {
                    await _semaphore.WaitAsync();
                    await _backupStream.FlushAsync();
                    var fileLength = _backupStream.Length;
                    _fullBackup = !_streamLength.HasValue || fileLength == _streamLength.Value;
                    await base.OnDisposing(cancellationToken);
                }
                catch
                {
                    _fullBackup = false;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        protected override async Task BeforeDisposing(CancellationToken cancellationToken)
        {
            await CloseSourceStream(cancellationToken);
        }

        protected override Task OnDisposing(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        protected override async Task AfterDisposing(CancellationToken cancellationToken)
        {
            await _onDisposed?.Invoke(_fullBackup && !cancellationToken.IsCancellationRequested);
        }
    }
}
