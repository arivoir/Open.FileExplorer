using Open.FileSystemAsync;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class StreamWatcher : StreamAsync
    {
        Action<long, SeekOrigin> _afterSeek;
        Func<long?, byte[], int, int, CancellationToken, Task> _afterRead;
        Func<Stream, Task> _beforeDisposing;
        Func<Stream, Task> _afterDisposing;

        public StreamWatcher(Stream stream,
            Action<long, SeekOrigin> afterSeek = null,
            Func<long?, byte[], int, int, CancellationToken, Task> afterRead = null,
            Func<Stream, Task> beforeDisposing = null,
            Func<Stream, Task> afterDisposing = null)
            : base(stream)
        {
            _afterSeek = afterSeek;
            _afterRead = afterRead;
            _beforeDisposing = beforeDisposing;
            _afterDisposing = afterDisposing;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var result = base.Seek(offset, origin);
            _afterSeek?.Invoke(offset, origin);
            return result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            long? position = null;
            if (CanSeek)
                position = Position;
            var readBytes = await base.ReadAsync(buffer, offset, count, cancellationToken);
            if (_afterRead != null)
                await _afterRead(position, buffer, offset, readBytes, cancellationToken);
            return readBytes;
        }

        protected override async Task BeforeDisposing(CancellationToken cancellationToken)
        {
            if (_beforeDisposing != null)
                await _beforeDisposing(InnerStream);
        }

        protected override async Task AfterDisposing(CancellationToken cancellationToken)
        {
            if (_afterDisposing != null)
                await _afterDisposing(InnerStream);
        }
    }
}
