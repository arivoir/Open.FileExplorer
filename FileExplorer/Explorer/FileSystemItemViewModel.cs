#define HOLD_MEMORY
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;
using Open.FileExplorer.Strings;
using Path1 = Open.FileSystemAsync.Path;
namespace Open.FileExplorer
{
    public class FileSystemItemViewModel : BaseViewModel
    {
        #region fields

        protected string _oldName;
        private string _oldPermissions;
        private string _path;

#if HOLD_MEMORY
        private byte[] _thumbnail = null;
#else
        private WeakReference<byte[]> _thumbnail = null;
#endif
        private bool? _canGetThumbnail = null;
        private bool _canGetThumnailDirectly = true;
        private SemaphoreSlim _canGetThumbnailSemaphore = new SemaphoreSlim(1);
        private SemaphoreSlim _getThumbnailSemaphore = new SemaphoreSlim(1);

        #endregion

        #region initialization

        public FileSystemItemViewModel(FileExplorerViewModel fileExplorer, string parentDirId, FileSystemItem item)
        {
            ParentDirId = parentDirId;
            Item = item;
            FileExplorer = fileExplorer;
            if (item != null && !string.IsNullOrWhiteSpace(item.Id) && parentDirId != null)
            {
                try
                {
                    ItemId = item.IsDirectory ? FileExplorer.GetDirectoryId(parentDirId, item.Id) : FileExplorer.GetFileId(parentDirId, item.Id);
                }
                catch { }
            }
        }

        #endregion

        #region object model

        public FileExplorerViewModel FileExplorer { get; private set; }

        public IFileSystemAsync FileSystem
        {
            get
            {
                return FileExplorer.FileSystem;
            }
        }

        public IAppService AppService
        {
            get
            {
                return FileExplorer.AppService;
            }
        }

        public Func<Transaction> GetDownloadPicturesTransaction { get; set; }
        public string ItemId { get; private set; }
        public FileSystemItem Item { get; private set; }
        public string ParentDirId { get; private set; }

        public string Path
        {
            get
            {
                if (_path == null)
                {
                    GetPath(CancellationToken.None).ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.RanToCompletion)
                            {
                                OnPropertyChanged("Path");
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext() ?? TaskScheduler.Default);
                }
                return _path;
            }
        }

        public virtual async Task<string> GetPath(CancellationToken cancellationToken)
        {
            if (_path == null)
            {
                var fullPath = await FileExplorer.GetFullPathAsync(ParentDirId, cancellationToken);
                _path = Path1.Combine(fullPath.Select(p => p.Item2.Name).ToArray());
            }
            return _path;
        }

        public bool EditingEnabled
        {
            get
            {
                return Item != null && !Item.IsReadOnly;
            }
        }

        public virtual string Name
        {
            get
            {
                return Item != null ? Item.Name : "";
            }
            set
            {
                Item.Name = value;
                Validate();
            }
        }

        public string Permissions
        {
            get
            {
                return Item.Permissions;
            }
            set
            {
                Item.Permissions = value;
                Validate();
            }
        }

        /// <summary>
        /// Gets the extension of the file.
        /// </summary>
        public virtual string Extension
        {
            get
            {
                var file = Item as FileSystemFile;
                if (file != null)
                {
                    if (Path1.HasExtension(file.Name))
                    {
                        return Path1.GetExtension(file.Name).ToLower();
                    }
                    else if (!string.IsNullOrWhiteSpace(file.ContentType))
                    {
                        return MimeType.GetExtensionsFromContentType(file.ContentType).FirstOrDefault() ?? "";
                    }
                }
                return "";
            }
        }

        /// <summary>
        /// Gets the name of the file without extension.
        /// </summary>
        public string NameWithoutExtension
        {
            get
            {
                if (Item != null)
                    return Path1.GetFileNameWithoutExtension(Item.Name);
                else
                    return "";
            }
        }

        public double NameFontSize
        {
            get
            {
                return GetFontSize(NameWithoutExtension, FileExplorer.ThumbnailSize);
            }
        }

        protected virtual bool NameIsRequired
        {
            get
            {
                return true;
            }
        }

        private static double GetFontSize(string text, double width)
        {
            text = text ?? "";
            var charCoef = 0.59;
            var textCoef = 1.5;
            var longestWord = text.Split(' ').Max(word => word.Length);
            var wordWeight = longestWord * charCoef;
            var textWeight = text.Length * charCoef;
            int micro = 12, small = 14, medium = 18, large = 22;
            if (wordWeight * large < width &&
                textWeight * large < textCoef * width &&
                !text.Contains(" "))
            {
                return large;
            }
            else if (wordWeight * medium < width &&
                textWeight * medium < textCoef * width)
            {
                return medium;
            }
            else if (wordWeight * small < width &&
                textWeight * small < textCoef * width)
            {
                return small;
            }
            return micro;
        }

        public virtual long? Size
        {
            get
            {
                return Item != null ? Item.Size : null;
            }
        }

        public string SizeText
        {
            get
            {
                return Size.HasValue ? FileExplorerViewModel.ToSizeString(Size.Value) : "";
            }
        }

        public DateTime? CreatedDate
        {
            get
            {
                return Item != null ? Item.CreatedDate : null;
            }
        }

        public string CreatedDateText
        {
            get
            {
                return CreatedDate.HasValue ? CreatedDate.ToString() : "";
            }
        }

        public DateTime? ModifiedDate
        {
            get
            {
                return Item != null ? Item.ModifiedDate : null;
            }
        }

        public string ModifiedDateText
        {
            get
            {
                return ModifiedDate.HasValue ? ModifiedDate.ToString() : "";
            }
        }

        public virtual bool SizeVisible
        {
            get
            {
                return Size.HasValue;
            }
        }

        public bool CreatedDateVisible
        {
            get
            {
                return CreatedDate.HasValue;
            }
        }

        public bool ModifiedDateVisible
        {
            get
            {
                return ModifiedDate.HasValue;
            }
        }

        public bool? IsThumbnailVisible
        {
            get
            {
                if (_canGetThumbnail == null)
                {
                    var task = CanGetThumbnailAsync(CancellationToken.None);
                    if (task.Status != TaskStatus.RanToCompletion)
                    {
                        task.ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.RanToCompletion)
                            {
                                OnPropertyChanged("NoPreviewAvailableVisible");
                                OnPropertyChanged("ThumbnailVisible");
                                OnPropertyChanged("ItemTemplate");
                                OnPropertyChanged("SmallItemTemplate");
                                OnPropertyChanged("SmallItemTemplateWithoutName");
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                return _canGetThumbnail;
            }
        }

        public virtual byte[] ThumbnailContent
        {
            get
            {
#if HOLD_MEMORY
                if (_thumbnail == null)
#else
                if (_thumbnail.GetTarget() == null)
#endif
                {
                    var task = GetThumbnailAsync(CancellationToken.None);
                    if (task.Status != TaskStatus.RanToCompletion)
                    {
                        task.ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                            {
                                OnPropertyChanged("ThumbnailContent");
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
#if HOLD_MEMORY
                return _thumbnail;
#else
                return _thumbnail.GetTarget();
#endif
            }
        }

        #endregion

        #region templates

        public virtual string ItemTemplate
        {
            get
            {
                return null;
            }
        }

        public virtual string ListItemTemplate
        {
            get
            {
                return null;
            }
        }

        public virtual string SmallItemTemplate
        {
            get
            {
                return null;
            }
        }

        public virtual string GetEmptyTemplate()
        {
            return null;
        }

        public virtual string SmallItemTemplateWithoutName
        {
            get
            {
                string template = null;
                GetSmallItemTemplateWithoutName().ContinueWith(t =>
                    {
                        if (t.IsCompleted)
                        {
                            template = t.Result;
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                return template ?? GetEmptyTemplate();
            }
        }

        public virtual Task<string> GetSmallItemTemplateWithoutName()
        {
            return Task.FromResult<string>(null);
        }

        public virtual string FormTemplate
        {
            get
            {
                return null;
            }
        }

        public virtual string NewFormTemplate
        {
            get
            {
                return FormTemplate;
            }
        }

        #endregion

        #region editing

        public virtual void BeginChanging()
        {
            _oldName = Item != null ? Item.Name ?? "" : "";
            _oldPermissions = Item != null ? Item.Permissions ?? "" : "";
        }


        public virtual void UndoChanges()
        {
            if (!Item.IsReadOnly)
            {
                Item.Name = _oldName;
                Item.Permissions = _oldPermissions;
            }
        }


        public virtual bool HasChanges()
        {
            return _oldName != Item.Name ||
                _oldPermissions != Item.Permissions;
        }

        public virtual void Validate()
        {
            ClearErrors();
            if (NameIsRequired && string.IsNullOrWhiteSpace(Name))
            {
                SetError(new ValidationError(FileSystemResources.RequiredLabel), "Name");
            }
        }

        #endregion

        #region labels

        public string NameLabel
        {
            get
            {
                return FileSystemResources.NameLabel;
            }
        }

        public string SizeLabel
        {
            get
            {
                return FileSystemResources.SizeLabel;
            }
        }

        public string CreatedDateLabel
        {
            get
            {
                return FileSystemResources.CreatedDateLabel;
            }
        }

        public string ModifiedDateLabel
        {
            get
            {
                return FileSystemResources.ModifiedDateLabel;
            }
        }

        #endregion

        #region implementation

        public virtual void Load()
        {
        }

        public virtual void Unload()
        {
        }

        public virtual Task<string> GetDefaultCommentTextAsync()
        {
            return Task.FromResult("");
        }

        protected async Task<bool> CanGetThumbnailAsync(CancellationToken cancellationToken)
        {
            if (_canGetThumbnail == null)
            {
                try
                {
                    await _canGetThumbnailSemaphore.WaitAsync();
                    if (_canGetThumbnail == null)
                    {
                        _canGetThumbnail = await CanGetThumbnailAsyncOverride(cancellationToken);
                    }
                }
                finally
                {
                    _canGetThumbnailSemaphore.Release();
                }
            }
            return _canGetThumbnail ?? false;
        }

        protected virtual Task<bool> CanGetThumbnailAsyncOverride(CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }

        public async Task<byte[]> GetThumbnailAsync(CancellationToken cancellationToken)
        {
            if (!await CanGetThumbnailAsync(cancellationToken))
                return null;

#if HOLD_MEMORY
            if (_thumbnail == null)
#else
            if (_thumbnail.GetTarget() == null)
#endif
            {
                ImageException ie = null;
                try
                {
                    await _getThumbnailSemaphore.WaitAsync();
#if HOLD_MEMORY
                    if (_thumbnail == null)
#else
                    if (_thumbnail.GetTarget() == null)
#endif
                    {
                        try
                        {
                            var buffer = await GetThumbnailAsyncOverride(cancellationToken);
#if HOLD_MEMORY
                            _thumbnail = buffer;
#else
                            _thumbnail = new WeakReference<byte[]>(buffer);
#endif
                        }
                        catch
                        {
                            _canGetThumbnail = false;
                            OnPropertyChanged("NoPreviewAvailableVisible");
                            OnPropertyChanged("ThumbnailVisible");
                            OnPropertyChanged("ItemTemplate");
                            OnPropertyChanged("SmallItemTemplate");
                            OnPropertyChanged("SmallItemTemplateWithoutName");
                        }
                    }
                }
                catch (ImageException exc)
                {
                    ie = exc;
                }
                catch
                {
                }
                finally
                {
                    _getThumbnailSemaphore.Release();
                }
                if (ie != null)
                {
                    return ie.ImageData;
                }
            }
#if HOLD_MEMORY
            return _thumbnail;
#else
            return _thumbnail.GetTarget();
#endif
        }

        protected virtual Task<byte[]> GetThumbnailAsyncOverride(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
