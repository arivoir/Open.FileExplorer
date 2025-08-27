using Open.FileSystemAsync;
using Open.IO;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FileSystemDirectoryViewModel : FileSystemItemViewModel
    {
        #region fields

        private long? _size = null;

        #endregion

        #region initialization

        public FileSystemDirectoryViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item)
            : base(fileExplorer, dirId, item)
        {
            CalculateSizeCommand = new TaskCommand(CalculateSizeAsync);
        }

        #endregion

        #region object model

        public virtual string Count
        {
            get
            {
                var count = (Item as FileSystemDirectory).Count;
                return count.HasValue ? count.Value.ToString() : "";
            }
        }

        #region Size

        public override bool SizeVisible
        {
            get
            {
                return Size.HasValue || (ItemId != null && FileExplorer.Extensions.FilesHaveSize(ItemId));
            }
        }


        public override long? Size
        {
            get
            {
                return base.Size ?? _size;
            }
        }

        public bool CalculateSizeVisible
        {
            get
            {
                return SizeVisible && !Size.HasValue;
            }
        }

        public TaskCommand CalculateSizeCommand { get; set; }

        private async Task CalculateSizeAsync(object arg)
        {
            try
            {
                _size = await FileExplorer.GetDirectorySizeAsync(ItemId);
                OnPropertyChanged("Size");
                OnPropertyChanged("SizeText");
                OnPropertyChanged("CalculateSizeVisible");
            }
            catch { }
        }

        #endregion

        #endregion

        #region templates

        public bool NameVisible
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Item.Name);
            }
        }

        public virtual string Icon
        {
            get
            {
                var directory = Item as FileSystemDirectory;
                var directoryName = directory.Name.Trim().ToLower();
                if (directoryName == "music" || directoryName == "musica" || directoryName == "música")
                {
                    return "MusicIcon";
                }
                else
                {
                    switch (Item.Permissions)
                    {
                        case "Public":
                            return "GlobeIcon";
                        case "Shared":
                            return "SharedIcon";
                        case "Private":
                            return "LockIcon";
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Gets the color associated with this kind of file.
        /// </summary>
        public virtual string BackgroundColor
        {
            get
            {
                if ((Item as FileSystemDirectory).IsSpecial)
                {
                    return SpecialDirectoryBackgroundBrush;
                }
                return DirectoryBackgroundBrush;
            }
        }

        //public virtual TextAlignment NameTextAlignment
        //{
        //    get
        //    {
        //        return TextAlignment.Left;
        //    }
        //}

        public override string ItemTemplate
        {
            get
            {
                return (IsThumbnailVisible ?? false) ? "AlbumTemplate" : "DirectoryTemplate";
            }
        }

        public override string ListItemTemplate
        {
            get
            {
                return "DirectoryListTemplate";
            }
        }

        public override string SmallItemTemplate
        {
            get
            {
                return "SmallDirectoryTemplate";
            }
        }
        public override string SmallItemTemplateWithoutName
        {
            get
            {
                return "SmallDirectoryTemplateWithoutName";
            }
        }

        public override Task<string> GetSmallItemTemplateWithoutName()
        {
            return Task.FromResult("SmallDirectoryTemplateWithoutName");
        }

        public override string FormTemplate
        {
            get
            {
                return "DirectoryFormTemplate";
            }
        }

        public static string SpecialDirectoryBackgroundBrush
        {
            get
            {
                return "#FFC9831D";
                //var color = Color.FromArgb(0xFF, 0xC9, 0x83, 0x1D);
                //return new SolidColorBrush(color);
            }
        }

        public static string DirectoryBackgroundBrush
        {
            get
            {
                return "#FFD29C27";
                //var color = Color.FromArgb(0xFF, 0xD2, 0x9C, 0x27);
                //return new SolidColorBrush(color);
            }
        }

        #endregion

        #region labels

        public string CalculateLabel
        {
            get
            {
                return GlobalResources.CalculateLabel;
            }
        }

        #endregion

        protected override async Task<bool> CanGetThumbnailAsyncOverride(CancellationToken cancellationToken)
        {
            if (Item is FileSystemDirectory)
            {
                return await FileExplorer.CanOpenDirectoryThumbnailAsync(ItemId, cancellationToken);
            }
            return false;
        }

        protected override async Task<byte[]> GetThumbnailAsyncOverride(CancellationToken cancellationToken)
        {
            Stream fileStream = null;
            try
            {
                fileStream = await OpenDirectoryThumbnailAsync();
                var buffer = await fileStream.ReadAsBufferAsync(cancellationToken);
                if (buffer != null)
                {
                    return buffer;
                }
            }
            finally
            {
                fileStream?.Dispose();
            }
            throw new NotImplementedException();
        }

        private async Task<Stream> OpenDirectoryThumbnailAsync()
        {
            var openFileTaskCompletion = new TaskCompletionSource<Stream>(); //Completed when stream is opened,
            var operation = GetDownloadPicturesTransaction().Enqueue(OperationKind.DownloadThumbnail, Name, null, CancellationToken.None,
            async (p, ct) =>
            {
                var downloadFileTaskCompletion = new TaskCompletionSource<Stream>(); //Completed when stream is completely read.
                try
                {
                    var stream = await FileExplorer.OpenDirectoryThumbnailAsync(ItemId, ct);
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

    }
}
