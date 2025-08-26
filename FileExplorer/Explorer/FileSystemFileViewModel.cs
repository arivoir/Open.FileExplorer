//#define HOLD_MEMORY
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;
using System.IO;
using Open.FileExplorer.Strings;
using System.Linq;
using Open.IO;
using C1.DataCollection;

namespace Open.FileExplorer
{
    public class FileSystemFileViewModel : FileSystemItemViewModel
    {
        #region ** fields

        private bool? _canGetFileImage = null;
#if HOLD_MEMORY
        private byte[] _fileImage = null;
#else
        private WeakReference<byte[]> _fileImage = null;
#endif

        private SemaphoreSlim _canGetFileImageSemaphore = new SemaphoreSlim(1);
        private SemaphoreSlim _getFileImageSemaphore = new SemaphoreSlim(1);
        private SemaphoreSlim _openFileContentSemaphore = new SemaphoreSlim(1);

        private long MAX_FILE_SIZE_WITH_THUMBNAIL = 1000000;
        private long MIN_THUMBNAIL_SIZE_RESIZE = 15000;
        private IList<CommentViewModel> _comments;
        private string _comment = "";

        #endregion

        #region ** initialization

        public FileSystemFileViewModel(IFileInfo fileInfo)
            : this(null, null, null, fileInfo)
        {
        }

        public FileSystemFileViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo fileInfo)
            : base(fileExplorer, dirId, item)
        {
            File = fileInfo;
            LikeCommand = new TaskCommand(async p => await SetLike((bool)p), p => !_updatingLike);
            AddCommentCommand = new TaskCommand(async message => await AddComment(), message => CanAddComment && !string.IsNullOrWhiteSpace(Comment));
        }

        public override void Load()
        {
            FileExplorer.Settings.PropertyChanged += OnSettingsChanged;
        }

        public override void Unload()
        {
            FileExplorer.Settings.PropertyChanged -= OnSettingsChanged;
        }

        void OnSettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowPhotoLabels")
            {
                OnPropertyChanged("NameVisible");
            }
        }

        #endregion

        #region ** object model


        public IFileInfo File { get; private set; }

        public ISocialExtension SocialFileSystem
        {
            get
            {
                return FileSystem as ISocialExtension;
            }
        }

        //public override string Name
        //{
        //    get
        //    {
        //        return Item != null ? Item.Name : File != null && NameIsRequired ? File.Name : "";
        //    }
        //    set
        //    {
        //        base.Name = value;
        //    }
        //}

        public bool NameVisible
        {
            get
            {
                return AppService.Settings.ShowPhotoLabels && !string.IsNullOrWhiteSpace(Item.Name);
            }
        }

        /// <summary>
        /// Gets the color associated with this kind of file.
        /// </summary>
        public virtual string BackgroundColor
        {
            get
            {
                var file = Item as FileSystemFile;
                if (file != null)
                {
                    return GetColorFromMimeType(file.ContentType);
                }
                else if (File != null)
                {
                    return GetColorFromMimeType(File.ContentType);
                }
                return "#FFFFFFFF";
            }
        }

        public bool ThumbnailVisible
        {
            get
            {
                return IsThumbnailVisible ?? false;
            }
        }

        public bool ContentVisible
        {
            get
            {
                return IsContentVisible ?? false;
            }
        }

        public bool NoPreviewAvailableVisible
        {
            get
            {
                return !ThumbnailVisible && !ContentVisible;
            }
        }


        public bool IsDownloadingContent { get; private set; }
        public bool DownloadingContentIsIndeterminate { get; private set; } = true;

        public double DownloadingContentProgress { get; private set; }

        public bool IsFileContentAvailable
        {
            get
            {
#if HOLD_MEMORY
                return _fileImage != null;
#else
                return _fileImage.GetTarget() != null;
#endif
            }
        }

        public byte[] FileContent
        {
            get
            {
#if HOLD_MEMORY
                if (_fileImage == null)
#else
                if (_fileImage.GetTarget() == null)
#endif
                {
                    var task = GetFileImageAsync(CancellationToken.None);
                    if (task.Status != TaskStatus.RanToCompletion)
                    {
                        task.ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.RanToCompletion)
                            {
                                OnPropertyChanged("FileContent");
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
#if HOLD_MEMORY
                return _fileImage;
#else
                return _fileImage.GetTarget();
#endif
            }
        }

        #endregion

        #region ** templates

        public override string ItemTemplate
        {
            get
            {
                var thumb = IsThumbnailVisible;
                if (thumb.HasValue)
                {
                    if (thumb.Value)
                        return "PhotoTemplate";
                    else
                        return "FileTemplate";
                }
                return "EmptyItemTemplate";
            }
        }

        public override string ListItemTemplate
        {
            get
            {
                var thumb = IsThumbnailVisible;
                if (thumb.HasValue)
                {
                    if (thumb.Value)
                        return "PhotoListTemplate";
                    else
                        return "FileListTemplate";
                }
                return "EmptyItemTemplate";
            }
        }

        public override string SmallItemTemplate
        {
            get
            {
                var thumb = IsThumbnailVisible;
                if (thumb.HasValue)
                {
                    if (thumb.Value)
                        return "SmallPhotoTemplate";
                    else
                        return "SmallFileTemplate";
                }
                return "EmptyItemTemplate";
            }
        }

        public override string SmallItemTemplateWithoutName
        {
            get
            {
                var thumb = IsThumbnailVisible;
                if (thumb.HasValue)
                {
                    if (thumb.Value)
                        return "SmallPhotoTemplateWithoutName";
                    else
                        return "SmallFileTemplateWithoutName";
                }
                return "EmptyItemTemplate";
            }
        }

        public override string GetEmptyTemplate()
        {
            return "EmptyItemTemplate";
        }

        public override async Task<string> GetSmallItemTemplateWithoutName()
        {
            bool hasThumnail = await CanGetThumbnailAsync(CancellationToken.None);
            if (hasThumnail)
            {
                return "SmallPhotoTemplateWithoutName";
            }
            else
            {
                return "SmallFileTemplateWithoutName";
            }
        }

        public override string FormTemplate
        {
            get
            {
                return "FileFormTemplate";
            }
        }

        #endregion

        #region ** implementation


        private bool? IsContentVisible
        {
            get
            {
                if (_canGetFileImage == null)
                {
                    var task = CanGetFileImageAsync(CancellationToken.None);
                    if (task.Status != TaskStatus.RanToCompletion)
                    {
                        task.ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.RanToCompletion)
                            {
                                OnPropertyChanged("NoPreviewAvailableVisible");
                                OnPropertyChanged("ContentVisible");
                                OnPropertyChanged("FileContent");
                            }

                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                return _canGetFileImage;
            }
        }

        public bool IsImage
        {
            get
            {
                return MimeType.Parse((Item as FileSystemFile)?.ContentType).Type == "image";
            }
        }

        public virtual bool IsVideo
        {
            get
            {
                return MimeType.Parse((Item as FileSystemFile)?.ContentType).Type == "video";
            }
        }

        #region ** file system concurrency access

        protected override async Task<bool> CanGetThumbnailAsyncOverride(CancellationToken cancellationToken)
        {
            if (File != null)
            {
                return true;
            }
            else if (Item is FileSystemFile)
            {
                if (await FileExplorer.CanOpenFileThumbnailAsync(ItemId, cancellationToken))
                {
                    return true;
                }
                else if (IsImage && ItemId != null && ((Item.Size ?? 0) <= MAX_FILE_SIZE_WITH_THUMBNAIL ? await FileExplorer.CanOpenFileAsync(ItemId, cancellationToken) : await FileExplorer.CanOpenFileFromCacheAsync(ItemId, cancellationToken)))
                {
                    return true;
                }
            }
            return false;
        }
        protected override async Task<byte[]> GetThumbnailAsyncOverride(CancellationToken cancellationToken)
        {
            if (File != null)
            {
                return await File.ReadAsBufferAsync();
            }
            else
            {
                byte[] buffer = null;
                System.IO.Stream fileStream = null;
                if (await FileExplorer.CanOpenFileThumbnailAsync(ItemId, cancellationToken))
                {
                    try
                    {
                        fileStream = await OpenFileThumbnailAsync();
                        buffer = await fileStream.ReadAsBufferAsync(cancellationToken);
                        if (buffer != null)
                        {
                            return buffer;
                        }
                    }
                    finally
                    {
                        fileStream?.Dispose();
                    }
                }
                else
                {
                    try
                    {
                        fileStream = await OpenFileAsync(cancellationToken);
                        if (fileStream.Length < MIN_THUMBNAIL_SIZE_RESIZE)
                            buffer = await fileStream.ReadAsBufferAsync(cancellationToken);
                        else
                            buffer = await AppService.ResizeImage(fileStream, FileExplorer.ThumbnailSize, FileExplorer.ThumbnailSize, true);
                        if (buffer != null)
                        {
                            return buffer;
                        }
                    }
                    finally
                    {
                        if (fileStream is StreamAsync)
                        {
                            await (fileStream as StreamAsync).DisposeAsync();
                        }
                        else
                        {
                            fileStream?.Dispose();
                        }
                    }
                }
            }
            throw new NotImplementedException();
        }

        private async Task<bool> CanGetFileImageAsync(CancellationToken cancellationToken)
        {
            if (_canGetFileImage == null)
            {
                try
                {
                    await _canGetFileImageSemaphore.WaitAsync();
                    if (_canGetFileImage == null)
                    {
                        _canGetFileImage = IsImage && ItemId != null && await FileExplorer.CanOpenFileAsync(ItemId, cancellationToken);
                    }
                }
                finally
                {
                    _canGetFileImageSemaphore.Release();
                }
            }
            return _canGetFileImage ?? false;
        }


        private async Task<Stream> OpenFileThumbnailAsync()
        {
            var openFileTaskCompletion = new TaskCompletionSource<Stream>(); //Completed when stream is opened,
            var operation = GetDownloadPicturesTransaction().Enqueue(OperationKind.DownloadThumbnail, Name, null, CancellationToken.None,
            async (p, ct) =>
            {
                var downloadFileTaskCompletion = new TaskCompletionSource<Stream>(); //Completed when stream is completely read.
                try
                {
                    var stream = await FileExplorer.OpenFileThumbnailAsync(ItemId, ct);
                    var streamLength = stream.GetLength();
                    openFileTaskCompletion.TrySetResult(new StreamWatcher(stream,
                        afterRead: (pos, buffer, offset, length, ct2) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            if (pos.HasValue && streamLength.HasValue)
                                p.Report(new StreamProgress(pos.Value + length, streamLength.Value));
                            return Task.FromResult(true);
                        },
                        afterDisposing: s =>
                        {
                            downloadFileTaskCompletion.TrySetResult(s);
                            return Task.FromResult(true);
                        }));
                    await downloadFileTaskCompletion.Task;
                }
                catch (Exception exc)
                {
                    openFileTaskCompletion.TrySetException(exc);
                }
            });
            var task = operation.RunAsync();
            return await openFileTaskCompletion.Task;
        }

        public async Task<byte[]> GetFileImageAsync(CancellationToken cancellationToken)
        {
#if HOLD_MEMORY
            if (_fileImage == null)
#else
            if (_fileImage.GetTarget() == null)
#endif
            {
                if (File != null)
                {
                    var buffer = await File.ReadAsBufferAsync();
#if HOLD_MEMORY
                    _fileImage = buffer;
#else
                    _fileImage = new WeakReference<byte[]>(buffer);
#endif
                }
                else
                {
                    if (!await CanGetFileImageAsync(cancellationToken))
                        return null;

                    System.IO.Stream fileStream = null;
                    var cts = new CancellationTokenSource();
                    try
                    {
                        await _getFileImageSemaphore.WaitAsync();
#if HOLD_MEMORY
                    if (_fileImage == null)
#else
                        if (_fileImage.GetTarget() == null)
#endif
                        {
                            fileStream = await OpenFileAsync(cts.Token);
                            byte[] res;
                            if (fileStream.CanSeek)
                                res = await AppService.ResizeImage(fileStream, FileExplorer.ScreenWidth, FileExplorer.ScreenHeight, false);
                            else
                                res = await fileStream.ReadAsBufferAsync(cancellationToken);
#if HOLD_MEMORY
                        _fileImage = res;
#else
                            _fileImage = new WeakReference<byte[]>(res);
#endif
                        }
                    }
                    catch
                    {
                        cts.Cancel();
                    }
                    finally
                    {
                        _getFileImageSemaphore.Release();
                        fileStream?.Dispose();
                    }
                }
            }
            return _fileImage.GetTarget();
        }


        public event EventHandler<AsyncEventArgs> DownloadStarted;
        public event EventHandler<DownloadDeltaEventArgs> DownloadChanged;
        public event EventHandler<AsyncEventArgs> DownloadCompleted;

        private Operation _downloadOperation;

        public async Task<Stream> OpenFileAsync(CancellationToken cancellationToken)
        {
            if (await FileExplorer.CanOpenFileFromCacheAsync(ItemId, cancellationToken))
                return await FileExplorer.OpenFileFromCacheAsync(ItemId, cancellationToken);

            await _openFileContentSemaphore.WaitAsync();

            var openFileTaskCompletion = new TaskCompletionSource<Stream>();
            _downloadOperation = GetDownloadPicturesTransaction().Enqueue(OperationKind.DownloadFile, base.Name, Item.Size, cancellationToken,
            async (p, ct) =>
            {
                var downloadFileTaskCompletion = new TaskCompletionSource<Stream>();
                try
                {
                    IsDownloadingContent = true;
                    DownloadingContentProgress = 0;
                    DownloadingContentIsIndeterminate = true;
                    base.OnPropertyChanged("IsDownloadingContent");
                    var task = OnDownloadAnimationStarted();

                    var stream = await FileExplorer.OpenFileAsync(ItemId, ct);
                    var streamLength = stream.GetLength();
                    var context = SynchronizationContext.Current;
                    openFileTaskCompletion.TrySetResult(new StreamWatcher(stream,
                        afterRead: (pos, buffer, offset, length, ct2) =>
                        {
                            ct.ThrowIfCancellationRequested();
                            if (pos.HasValue && streamLength.HasValue)
                            {
                                p.Report(new StreamProgress(pos.Value + length, streamLength.Value));

                                var progress = (double)(pos.Value + length) / streamLength.Value;
                                OnDownloadAnimationDelta(progress);
                                context.Post(new SendOrPostCallback(o =>
                                {
                                    if (DownloadingContentIsIndeterminate)
                                    {
                                        DownloadingContentIsIndeterminate = false;
                                        base.OnPropertyChanged("DownloadingContentIsIndeterminate");
                                    }
                                    DownloadingContentProgress = Math.Max(DownloadingContentProgress, progress);
                                    base.OnPropertyChanged("DownloadingContentProgress");
                                }), null);
                            }
                            return Task.FromResult(true);
                        },
                        afterDisposing: async s =>
                        {
                            await OnDownloadAnimationCompleted();
                            IsDownloadingContent = false;
                            base.OnPropertyChanged("IsDownloadingContent");
                            downloadFileTaskCompletion.TrySetResult(s);
                            _openFileContentSemaphore.Release();
                        }));
                    await downloadFileTaskCompletion.Task;
                }
                catch (Exception exc)
                {
                    IsDownloadingContent = false;
                    openFileTaskCompletion.TrySetException(exc);
                    _openFileContentSemaphore.Release();
                }
            });
            var t = _downloadOperation.RunAsync();
            return await openFileTaskCompletion.Task;
        }

        internal void StopDownload()
        {
            _downloadOperation.Cancel();
        }

        private SemaphoreSlim _downloadAnimationSemaphore = new SemaphoreSlim(1);
        private double _progress;

        private async Task OnDownloadAnimationStarted()
        {
            try
            {
                await _downloadAnimationSemaphore.WaitAsync();
                var startedEventArgs = new AsyncEventArgs();
                DownloadStarted?.Invoke(this, startedEventArgs);
                await startedEventArgs.WaitDeferralsAsync();
            }
            finally
            {
                _downloadAnimationSemaphore.Release();
            }
        }

        private async void OnDownloadAnimationDelta(double progress)
        {
            if (progress <= _progress)
                return;
            _progress = progress;
            try
            {
                await _downloadAnimationSemaphore.WaitAsync();
                if (progress == _progress)
                {
                    var changedEventArgs = new DownloadDeltaEventArgs(progress);
                    DownloadChanged?.Invoke(this, changedEventArgs);
                    await changedEventArgs.WaitDeferralsAsync();
                }
            }
            finally
            {
                _downloadAnimationSemaphore.Release();
            }
        }

        private async Task OnDownloadAnimationCompleted()
        {
            try
            {
                await _downloadAnimationSemaphore.WaitAsync();
                var completedEventArgs = new AsyncEventArgs();
                DownloadCompleted?.Invoke(this, completedEventArgs);
                await completedEventArgs.WaitDeferralsAsync();
            }
            finally
            {
                _downloadAnimationSemaphore.Release();
            }
        }

        #endregion


        #endregion

        #region ** labels

        public string LikeLabel
        {
            get
            {
                return PhotoViewerResources.LikeLabel;
            }
        }

        public string HideCommentsLabel
        {
            get
            {
                return PhotoViewerResources.HideCommentsLabel;
            }
        }

        public string NoPreviewAvailableLabel
        {
            get
            {
                return PhotoViewerResources.NoPreviewAvailableLabel;
            }
        }

        #endregion

        #region ** comments

        public async Task LoadDefaultComment()
        {
            _comment = await GetDefaultCommentTextAsync();
        }


        public string Comment
        {
            get
            {
                return _comment;
            }
            set
            {
                _comment = value;
                OnPropertyChanged();
                AddCommentCommand.OnCanExecuteChanged();
            }
        }

        public bool IsCommitingComment { get; set; }
        public bool NotIsCommitingComment
        {
            get
            {
                return !IsCommitingComment;
            }
        }
        public TaskCommand AddCommentCommand { get; private set; }

        public IList<CommentViewModel> Comments
        {
            get
            {
                if (_comments == null)
                {
                    if (Item != null && AppService.Settings.IsOnline)
                    {
                        var filePath = ItemId;
                        SocialFileSystem.GetCommentsAsync(filePath).ContinueWith(async r =>
                        {
                            if (r.Exception == null)
                            {
                                if (r.Result.NeedsLoadAsync())
                                    await r.Result.LoadAsync();
                                _comments = ConvertCollection(r.Result);
                                OnPropertyChanged("Comments");
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                return _comments;
            }
            private set
            {
                _comments = value;
                OnPropertyChanged();
            }
        }

        public bool CanAddComment
        {
            get
            {
                return Item != null && SocialFileSystem.CanAddComment(ItemId) && AppService.Settings.IsOnline;
            }
        }

        public async Task AddComment()
        {
            try
            {
                IsCommitingComment = true;
                OnPropertyChanged("IsCommitingComment");
                OnPropertyChanged("NotIsCommitingComment");
                await SocialFileSystem.AddCommentAsync(ItemId, Comment);
                var result = await SocialFileSystem.GetCommentsAsync(ItemId);
                await result.LoadAsync();
                _comments = ConvertCollection(result);
                OnPropertyChanged("Comments");
                Comment = null;
                AddCommentCommand.OnCanExecuteChanged();
            }
            catch (OperationCanceledException) { }
            catch
            {
                await AppService.ShowErrorAsync(PhotoViewerResources.AddCommentError);
            }
            finally
            {
                IsCommitingComment = false;
                OnPropertyChanged("IsCommitingComment");
                OnPropertyChanged("NotIsCommitingComment");
            }
        }

        private IList<CommentViewModel> ConvertCollection(IReadOnlyList<FileSystemComment> r)
        {
            return r.Select(c =>
            {
                return new CommentViewModel
                {
                    Message = c.Message,
                    UserName = c.From.Name,
                    UserImage = c.From.Thumbnail,
                    UserImageVisible = c.From.Thumbnail != null,
                    CreatedDate = GetFriendlyTime(c.CreatedTime),
                };
            }).ToList();
        }

        private string GetFriendlyTime(DateTime dateTime)
        {
            TimeSpan span = DateTime.Now - dateTime.ToLocalTime();
            if (span < TimeSpan.Zero)
            {
                return dateTime.ToString("d");
            }
            else if (span < TimeSpan.FromMinutes(2))
            {
                return string.Format(TimeSpanResources.TimeSpanSeconds, (int)span.TotalSeconds);
            }
            else if (span < TimeSpan.FromHours(1))
            {
                return string.Format(TimeSpanResources.TimeSpanMinutes, (int)span.TotalMinutes);
            }
            else if (span < TimeSpan.FromDays(2))
            {
                return string.Format(TimeSpanResources.TimeSpanHours, (int)span.TotalHours);
            }
            else if (span < TimeSpan.FromDays(14))
            {
                return string.Format(TimeSpanResources.TimeSpanDays, (int)span.TotalDays);
            }
            else if (span < TimeSpan.FromDays(62))
            {
                return string.Format(TimeSpanResources.TimeSpanWeeks, (int)Math.Floor(span.TotalDays / 7.0));
            }
            else if (span < TimeSpan.FromDays(730))
            {
                return string.Format(TimeSpanResources.TimeSpanMonths, (int)Math.Floor(span.TotalDays / 31.0));
            }
            else
            {
                return string.Format(TimeSpanResources.TimeSpanYears, (int)Math.Floor(span.TotalDays / 365.0));
            }
        }

        #endregion

        #region ** likes

        private bool _updatingLike = false;

        public TaskCommand LikeCommand { get; set; }

        public bool? Like { get; private set; }

        public bool CanLike
        {
            get
            {
                return Item != null && SocialFileSystem.CanThumbUp(ItemId) && AppService.Settings.IsOnline;
            }
        }

        internal async Task GetLike()
        {
            if (Item != null && AppService.Settings.IsOnline && SocialFileSystem.CanThumbUp(ItemId) && !Like.HasValue)
            {
                try
                {
                    _updatingLike = true;
                    LikeCommand.OnCanExecuteChanged();
                    var people = await SocialFileSystem.GetThumbsUpAsync(ItemId);
                    await people.LoadAsync();
                    Like = people.Any(p => p.IsAuthenticatedUser);
                }
                finally
                {
                    _updatingLike = false;
                    LikeCommand.OnCanExecuteChanged();
                    OnPropertyChanged("Like");
                }
            }
        }

        private async Task SetLike(bool like)
        {
            try
            {
                _updatingLike = true;
                LikeCommand.OnCanExecuteChanged();
                if (like)
                {
                    await SocialFileSystem.AddThumbUp(ItemId);
                    Like = true;
                }
                else
                {
                    await SocialFileSystem.RemoveThumbUp(ItemId);
                    Like = false;
                }
            }
            finally
            {
                _updatingLike = false;
                LikeCommand.OnCanExecuteChanged();
                OnPropertyChanged("Like");
            }
        }

        #endregion

        #region ** colors

        private static Dictionary<MimeType, string> _fileColors = new Dictionary<MimeType, string>();
        private static string[] _extraFileColors = new string[] { "#FFB9452B", "#FF665FB0", "#FF1C8097", "#FF8D3BBB", "#FFBF4AA4" };

        /// <summary>
        /// Gets the default color for a specified contentType.
        /// </summary>
        /// <param name="mimeType">The mime type.</param>
        protected string GetColorFromMimeType(string mimeType)
        {
            var mime = MimeType.Parse(mimeType);
            if (mime.Type == "text" ||
                mime == "application/msword" ||
                mime == "application/vnd.ms-word.document.1" ||
                mime == "application/rtf" ||
                mime == "application/vnd.google-apps.document" ||
                mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" ||
                mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.template" ||
                mime == "application/vnd.oasis.opendocument.text" ||
                mime == "application/vnd.oasis.opendocument.text-template" ||
                mime == "application/vnd.oasis.opendocument.text-web" ||
                mime == "application/vnd.oasis.opendocument.text-master" ||
                mime == "application/xml")
                return "#FF2B579A";
            if (mime == "application/vnd.ms-excel" ||
                mime == "application/vnd.ms-excel.12" ||
                mime == "application/vnd.google-apps.spreadsheet" ||
                mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" ||
                mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.template" ||
                mime == "application/vnd.oasis.opendocument.spreadsheet" ||
                mime == "application/vnd.oasis.opendocument.spreadsheet-template")
                return "#FF217346";
            if (mime == "application/vnd.ms-powerpoint" ||
                mime == "application/vnd.google-apps.presentation" ||
                mime == "application/vnd.openxmlformats-officedocument.presentationml.template" ||
                mime == "application/vnd.openxmlformats-officedocument.presentationml.slideshow" ||
                mime == "application/vnd.openxmlformats-officedocument.presentationml.presentation" ||
                mime == "application/vnd.openxmlformats-officedocument.presentationml.slide" ||
                mime == "application/vnd.oasis.opendocument.presentation" ||
                mime == "application/vnd.oasis.opendocument.presentation-template")
                return "#FFDA401A";
            if (mime == "application/pdf")
                return "#FFA33D25";
            if (mime.Type == "image")
                return "#FF6A7912";
            if (mime.Type == "audio" || mime.Type == "video")
                return "#FF176A6C";
            if (mime == "application/zip" ||
                mime == "application/x-zip" ||
                mime == "application/rar" ||
                mime == "application/x-rar-compressed")
                return "#FFC9731D";

            if (_fileColors.ContainsKey(mime))
            {
                return _fileColors[mime];
            }
            var color = _extraFileColors[_fileColors.Count % _extraFileColors.Length];
            _fileColors[mime] = color;
            return color;
        }

        #endregion
    }

    public class DownloadDeltaEventArgs : AsyncEventArgs
    {
        public DownloadDeltaEventArgs(double progress)
            : base()
        {
            Progress = progress;
        }
        public double Progress { get; private set; }
    }
}
