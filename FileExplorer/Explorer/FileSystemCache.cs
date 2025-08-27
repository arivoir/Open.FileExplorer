using C1.DataCollection;
using Open.FileSystemAsync;
using Open.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Path = Open.FileSystemAsync.Path;

namespace Open.FileExplorer
{
    public class FileSystemCache
    {
        #region fields

        private static TimeSpan SAVE_DELAY = TimeSpan.FromSeconds(5);

        #endregion

        #region initialization

        public FileSystemCache(IFileSystemStorage storage,
            string metadataPath,
            string filesPath,
            string thumbnailsPath)
        {
            Storage = storage ?? new EmptyStorage();
            //SaveFiles = storage != null;
            MetadataDirectory = metadataPath;
            FilesDirectory = filesPath;
            ThumbnailsDirectory = thumbnailsPath;
        }

        #endregion

        #region object model

        public IFileSystemStorage Storage { get; private set; }

        public string ThumbnailsDirectory { get; private set; }
        public string FilesDirectory { get; private set; }
        public string MetadataDirectory { get; private set; }

        #endregion

        #region metadata

        Dictionary<string, IDataCollection<FileSystemDirectory>> _pendingDirectories = new Dictionary<string, IDataCollection<FileSystemDirectory>>();
        Dictionary<string, IDataCollection<FileSystemFile>> _pendingFiles = new Dictionary<string, IDataCollection<FileSystemFile>>();
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public Task SaveDirectoriesAsync(string uniqueDirPath, IDataCollection<FileSystemDirectory> directories)
        {
            directories.CollectionChanged += (s, e) =>
            {
                _pendingDirectories[uniqueDirPath] = directories;
                SavePendingItems();
            };
            _pendingDirectories[uniqueDirPath] = directories;
            SavePendingItems();
            return Task.FromResult(true);
        }

        public Task SaveFilesAsync(string uniqueDirPath, IDataCollection<FileSystemFile> files)
        {
            files.CollectionChanged += (s, e) =>
            {
                _pendingFiles[uniqueDirPath] = files;
                SavePendingItems();
            };
            _pendingFiles[uniqueDirPath] = files;
            SavePendingItems();
            return Task.FromResult(true);
        }

        private async void SavePendingItems()
        {
            try
            {
                await _semaphore.WaitAsync();
                await Task.Delay(SAVE_DELAY);
                var pendingDirectories = _pendingDirectories.Select(pair => new KeyValuePair<string, FileSystemDirectory[]>(pair.Key, pair.Value.GetLoadedItems().Select(info => info.Item).ToArray())).ToArray();
                var pendingFiles = _pendingFiles.Select(pair => new KeyValuePair<string, FileSystemFile[]>(pair.Key, pair.Value.GetLoadedItems().Select(info => info.Item).ToArray())).ToArray();
                _pendingDirectories.Clear();
                _pendingFiles.Clear();
                if (pendingDirectories.Length > 0 || pendingFiles.Length > 0)
                {
                    foreach (var pending in pendingDirectories)
                    {
                        await SaveDirectoriesAsync(pending.Key, pending.Value);
                    }
                    foreach (var pending in pendingFiles)
                    {
                        await SaveFilesAsync(pending.Key, pending.Value);
                    }
                }
            }
            catch { }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task SaveDirectoriesAsync(string uniqueDirPath, IEnumerable<FileSystemDirectory> enumerable)
        {
            var isoPath = GetDirsMetadataFilePath(uniqueDirPath);
            Stream stream = null;
            try
            {
                var file = await Storage.TryGetFileAsync(isoPath);
                if (file == null)
                {
                    file = await Storage.CreateFileAsync(isoPath);
                }
                stream = await file.OpenWriteAsync();
                if (stream != null)
                {
                    var directories = enumerable.Select(d => new CachedFileSystemDirectory { Id = d.Id, Name = d.Name }).ToList();
                    await JsonSerializer.SerializeAsync(stream, directories);
                }
            }
            catch { }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }
        }

        public async Task SaveFilesAsync(string uniqueDirPath, IEnumerable<FileSystemFile> files)
        {
            var isoPath = GetFilesMetadataFilePath(uniqueDirPath);
            Stream stream = null;
            try
            {
                var file = await Storage.TryGetFileAsync(isoPath);
                if (file == null)
                {
                    file = await Storage.CreateFileAsync(isoPath);
                }
                stream = await file.OpenWriteAsync();
                if (stream != null)
                {
                    var filesList = files.Select(d => new CachedFileSystemFile { Id = d.Id, Name = d.Name, ContentType = d.ContentType }).ToList();
                    await JsonSerializer.SerializeAsync(stream, filesList);
                }
            }
            catch { }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }
        }

        public async Task<IDataCollection<FileSystemDirectory>> GetDirectoriesAsync(string uniqueDirPath)
        {
            try
            {
                var isoPath = GetDirsMetadataFilePath(uniqueDirPath);
                var file = await Storage.TryGetFileAsync(isoPath);
                if (file != null)
                {
                    var stream = await file.OpenSequentialReadAsync();
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<CachedFileSystemDirectory>>(stream);
                        return list.Select(cd => new CacheFileSystemDirectory(cd.Id, cd.Name)).Cast<FileSystemDirectory>().AsDataCollection();
                    }
                    finally
                    {
                        if (stream != null)
                            stream.Dispose();
                    }
                }
            }
            catch { }
            throw new CacheNotAvailableException();
        }


        public async Task<IDataCollection<FileSystemFile>> GetFilesAsync(string uniqueDirPath)
        {
            try
            {
                var isoPath = GetFilesMetadataFilePath(uniqueDirPath);
                var file = await Storage.TryGetFileAsync(isoPath);
                if (file != null)
                {
                    var stream = await file.OpenSequentialReadAsync();
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<CachedFileSystemFile>>(stream);
                        return list.Select(cd => new CacheFileSystemFile(cd.Id, cd.Name, cd.ContentType)).Cast<FileSystemFile>().AsDataCollection();
                    }
                    finally
                    {
                        if (stream != null)
                            stream.Dispose();
                    }
                }
            }
            catch { }
            throw new CacheNotAvailableException();
        }

        #endregion

        #region thumbnails and files

        public async Task<bool> ContainsSavedFileAsync(string path)
        {
            var isoPath = Path.Combine(FilesDirectory, path);
            return await Storage.TryGetFileAsync(isoPath) != null;
        }

        public async Task<bool> ContainsSavedThumbnailAsync(string path)
        {
            var isoPath = GetThumbnailsPath(path);
            return await Storage.TryGetFileAsync(isoPath) != null;
        }

        public async Task<Stream> TryGetSavedFileAsync(string path)
        {
            try
            {
                var isoPath = Path.Combine(FilesDirectory, path);
                var file = await Storage.TryGetFileAsync(isoPath);
                if (file != null)
                {
                    return await file.OpenSequentialReadAsync();
                }
            }
            catch { }
            return null;
        }

        internal async Task<Stream> TryGetSavedThumbnailAsync(string path)
        {
            try
            {
                var isoPath = GetThumbnailsPath(path);
                var file = await Storage.TryGetFileAsync(isoPath);
                if (file != null)
                {
                    return await file.OpenSequentialReadAsync();
                }
            }
            catch { }
            return null;
        }

        public async Task<Stream> WatchFileStreamAsync(string path, Stream stream)
        {
            var isoPath = GetFilesPath(path);
            var tempPath = GetTempPath(isoPath);
            IFileInfo file = null;
            try
            {
                file = await Storage.CreateFileAsync(tempPath);
            }
            catch { }
            if (file != null)
            {
                try
                {
                    var fileStream = await file.OpenWriteAsync();
                    return new StreamCloner(stream, fileStream,
                        onSuccess: async () =>
                        {
                            await Storage.MoveFileAsync(tempPath, isoPath);
                        },
                        onFail: async () =>
                        {
                            await Storage.DeleteFileAsync(tempPath);
                        });
                }
                catch { }
            }
            return stream;
        }

        internal async Task<Stream> WatchFileThumbnailStreamAsync(string path, Stream stream)
        {
            var isoPath = GetThumbnailsPath(path);
            var tempPath = GetTempPath(isoPath);
            IFileInfo file = null;
            try
            {
                file = await Storage.CreateFileAsync(tempPath);
            }
            catch { }
            if (file != null)
            {
                try
                {
                    var fileStream = await file.OpenWriteAsync();
                    return new StreamCloner(stream, fileStream,
                        onSuccess: async () =>
                        {
                            await Storage.MoveFileAsync(tempPath, isoPath);
                        },
                        onFail: async () =>
                        {
                            await Storage.DeleteFileAsync(tempPath);
                        });
                }
                catch { }
            }
            return stream;
        }

        private string GetThumbnailsPath(string path)
        {
            return Path.Combine(ThumbnailsDirectory, path);
        }

        public string GetFilesPath(string path)
        {
            return Path.Combine(FilesDirectory, path);
        }

        private string GetTempPath(string path)
        {
            return Path.Combine(Path.GetParentPath(path), Path.GetRandomFileName());
        }

        private string GetDirsMetadataFilePath(string uniqueDirPath)
        {
            return Path.Combine(MetadataDirectory, Path.Combine(uniqueDirPath, "dirs.json"));
        }

        private string GetFilesMetadataFilePath(string uniqueDirPath)
        {
            return Path.Combine(MetadataDirectory, Path.Combine(uniqueDirPath, "files.json"));
        }

        #endregion

        public async Task<IFileInfo> TryGetFileAsync(string path)
        {
            return await Storage.TryGetFileAsync(path);
        }

        #region clear

        public async Task DeleteFolderAsync(string uniqueDirPath)
        {
            try
            {
                var metadataIsoPath = Path.Combine(MetadataDirectory, uniqueDirPath);
                //if (await Storage.DirectoryExistsAsync(metadataIsoPath))
                {
                    await Storage.DeleteDirectoryAsync(metadataIsoPath);
                }
                var thumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, uniqueDirPath);
                //if (await Storage.DirectoryExistsAsync(thumbnailsIsoPath))
                {
                    await Storage.DeleteDirectoryAsync(thumbnailsIsoPath);
                }
                var filesIsoPath = Path.Combine(FilesDirectory, uniqueDirPath);
                //if (await Storage.DirectoryExistsAsync(filesIsoPath))
                {
                    await Storage.DeleteDirectoryAsync(filesIsoPath);
                }
            }
            catch { }
        }

        public async Task ClearCache()
        {
            try
            {
                var deleted = await Storage.DeleteDirectoryAsync(FilesDirectory);
            }
            catch (Exception)
            {
            }
            try
            {
                var deleted = await Storage.DeleteDirectoryAsync(ThumbnailsDirectory);
            }
            catch (Exception)
            {
            }
            try
            {
                var deleted = await Storage.DeleteDirectoryAsync(MetadataDirectory);
            }
            catch (Exception)
            {
            }
        }

        public async Task DeleteFileAsync(string uniqueFilePath)
        {
            try
            {
                var thumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, uniqueFilePath);
                //if (await Storage.DirectoryExistsAsync(thumbnailsIsoPath))
                {
                    await Storage.DeleteFileAsync(thumbnailsIsoPath);
                }
                var filesIsoPath = Path.Combine(FilesDirectory, uniqueFilePath);
                //if (await Storage.DirectoryExistsAsync(filesIsoPath))
                {
                    await Storage.DeleteFileAsync(filesIsoPath);
                }
            }
            catch { }
        }

        public async Task MoveDirectoryAsync(string originalDirPath, string updatedDirPath)
        {
            try
            {
                var origianlMetadataIsoPath = Path.Combine(MetadataDirectory, originalDirPath);
                //if (await Storage.DirectoryExistsAsync(origianlMetadataIsoPath))
                {
                    var updatedMetadataIsoPath = Path.Combine(MetadataDirectory, updatedDirPath);
                    await Storage.MoveDirectoryAsync(origianlMetadataIsoPath, updatedMetadataIsoPath);
                }
                var origianlThumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, originalDirPath);
                //if (await Storage.DirectoryExistsAsync(origianlThumbnailsIsoPath))
                {
                    var updatedThumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, updatedDirPath);
                    await Storage.MoveDirectoryAsync(origianlThumbnailsIsoPath, updatedThumbnailsIsoPath);
                }
                var origianlFilesIsoPath = Path.Combine(FilesDirectory, originalDirPath);
                //if (await Storage.DirectoryExistsAsync(origianlFilesIsoPath))
                {
                    var updatedFilesIsoPath = Path.Combine(FilesDirectory, updatedDirPath);
                    await Storage.MoveDirectoryAsync(origianlFilesIsoPath, updatedFilesIsoPath);
                }
            }
            catch { }
        }

        public async Task MoveFileAsync(string originalFilePath, string updatedFilePath)
        {
            try
            {
                var origianlThumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, originalFilePath);
                //if (await Storage.DirectoryExistsAsync(origianlThumbnailsIsoPath))
                {
                    var updatedThumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, updatedFilePath);
                    await Storage.MoveFileAsync(origianlThumbnailsIsoPath, updatedThumbnailsIsoPath);
                }
                var origianlFilesIsoPath = Path.Combine(FilesDirectory, originalFilePath);
                //if (await Storage.DirectoryExistsAsync(origianlFilesIsoPath))
                {
                    var updatedFilesIsoPath = Path.Combine(FilesDirectory, updatedFilePath);
                    await Storage.MoveFileAsync(origianlFilesIsoPath, updatedFilesIsoPath);
                }
            }
            catch { }
        }

        public async Task CopyFileAsync(string originalFilePath, string copiedFilePath)
        {
            try
            {
                var origianlThumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, originalFilePath);
                //if (await Storage.DirectoryExistsAsync(origianlThumbnailsIsoPath))
                {
                    var updatedThumbnailsIsoPath = Path.Combine(ThumbnailsDirectory, copiedFilePath);
                    await Storage.CopyFileAsync(origianlThumbnailsIsoPath, updatedThumbnailsIsoPath);
                }
                var origianlFilesIsoPath = Path.Combine(FilesDirectory, originalFilePath);
                //if (await Storage.DirectoryExistsAsync(origianlFilesIsoPath))
                {
                    var updatedFilesIsoPath = Path.Combine(FilesDirectory, copiedFilePath);
                    await Storage.CopyFileAsync(origianlFilesIsoPath, updatedFilesIsoPath);
                }
            }
            catch { }
        }

        #endregion

        #region space

        public async Task<long> GetMetadataUsedSpaceAsync()
        {
            return await Storage.GetFolderSizeAsync(MetadataDirectory);
        }

        public async Task<long> GetThumbnailsUsedSpaceAsync()
        {
            return await Storage.GetFolderSizeAsync(ThumbnailsDirectory);
        }

        public async Task<long> GetFilesUsedSpaceAsync()
        {
            return await Storage.GetFolderSizeAsync(FilesDirectory);
        }

        #endregion
    }

    internal class StreamCloner : StreamAsync
    {
        private long _position = 0;
        private long? _streamLength;
        private Stream _backupStream;
        private Func<Task> _onsuccess;
        private Func<Task> _onFail;
        private bool _completed;

        public StreamCloner(Stream stream, Stream backupStream, Func<Task> onSuccess = null, Func<Task> onFail = null)
            : base(stream)
        {
            _streamLength = stream.GetLength();
            _backupStream = backupStream;
            _onsuccess = onSuccess;
            _onFail = onFail;
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

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int readBytes = 0;

            if (_position < _backupStream.Position)
            {
                //There was a backwards jump, let's get the bytes from the backup stream.
                var fileStreamPosition = _backupStream.Position;
                _backupStream.Seek(_position, SeekOrigin.Begin);
                var readCount = Math.Min(count, (int)(_backupStream.Length - _backupStream.Position));
                readBytes = await _backupStream.ReadAsync(buffer, offset, readCount, cancellationToken);
                offset += readBytes;
                count -= readBytes;
                _position += readBytes;
                _backupStream.Seek(fileStreamPosition, SeekOrigin.Begin);
            }

            if (_position > _backupStream.Position)
            {
                //There was a forward jump, let's save the intermedia bytes.
                int internalBufferSize = 4096;
                byte[] internalBuffer = new byte[internalBufferSize];
                while (_backupStream.Position < _position)
                {
                    var internalReadBytes = await base.ReadAsync(internalBuffer, 0, Math.Min(internalBufferSize, (int)(_position - _backupStream.Position)), cancellationToken);
                    await _backupStream.WriteAsync(internalBuffer, 0, internalReadBytes);
                }
            }

            if (count > 0)
            {
                var bytes = await base.ReadAsync(buffer, offset, count, cancellationToken);
                await _backupStream.WriteAsync(buffer, offset, bytes);
                readBytes += bytes;
                _position += bytes;
            }
            return readBytes;
        }

        protected override async Task BeforeDisposing(CancellationToken cancellationToken)
        {
            await _backupStream.FlushAsync();
            var fileLength = _backupStream.Length;
            _completed = !_streamLength.HasValue || fileLength == _streamLength.Value;
            try
            {
                _backupStream.Dispose();
            }
            catch
            {
                _completed = false;
            }
        }

        protected override async Task AfterDisposing(CancellationToken cancellationToken)
        {
            if (_completed && !cancellationToken.IsCancellationRequested)
                await _onsuccess?.Invoke();
            else
                await _onFail?.Invoke();
        }
    }

    [DataContract]
    public class CachedFileSystemDirectory
    {
        [DataMember(Name = "id", IsRequired = true)]
        public string Id { get; set; }
        [DataMember(Name = "name", IsRequired = true)]
        public string Name { get; set; }
    }

    [DataContract]
    public class CachedFileSystemFile
    {
        [DataMember(Name = "id", IsRequired = true)]
        public string Id { get; set; }
        [DataMember(Name = "name", IsRequired = true)]
        public string Name { get; set; }
        [DataMember(Name = "mime", IsRequired = true)]
        public string ContentType { get; set; }
    }
    internal class CacheFileSystemDirectory : FileSystemDirectory
    {
        public CacheFileSystemDirectory(string id, string name)
            : base(id, name, true)
        {
        }
    }
    internal class CacheFileSystemFile : FileSystemFile
    {
        public CacheFileSystemFile(string id, string name, string contentType)
            : base(id, name, contentType, true)
        {
        }
    }

    class EmptyStorage : IFileSystemStorage
    {
        public Task<bool> CheckAccessAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public Task<bool> DirectoryExistsAsync(string path)
        {
            return Task.FromResult(false);
        }

        public Task<bool> FileExistsAsync(string path)
        {
            return Task.FromResult(false);
        }

        public Task CreateDirectoryAsync(string path)
        {
            return Task.FromResult(false);
        }

        public Task<IFileInfo> CreateFileAsync(string path)
        {
            return Task.FromResult<IFileInfo>(null);
        }

        public Task<IFileInfo> TryGetFileAsync(string path)
        {
            return Task.FromException<IFileInfo>(new NotImplementedException());
        }

        public Task<bool> DeleteDirectoryAsync(string path)
        {
            return Task.FromResult(false);
        }

        public Task<bool> DeleteFileAsync(string path)
        {
            return Task.FromResult(false);
        }


        public Task<long> GetFolderSizeAsync(string path)
        {
            return Task.FromResult<long>(0);
        }

        public Task<bool> CopyFileAsync(string origianlPath, string targetPath)
        {
            return Task.FromResult(false);
        }

        public Task<bool> MoveDirectoryAsync(string origianlPath, string updatedPath)
        {
            return Task.FromResult(false);
        }

        public Task<bool> MoveFileAsync(string origianlPath, string updatedPath)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<string>> GetDirectoriesAsync(string path)
        {
            return Task.FromException<IReadOnlyList<string>>(new NotImplementedException());
        }

        public Task<IReadOnlyList<string>> GetFilesAsync(string path)
        {
            return Task.FromException<IReadOnlyList<string>>(new NotImplementedException());
        }

        public Task<long> GetFreeSpaceAsync()
        {
            return Task.FromException<long>(new NotImplementedException());
        }
    }
}
