using C1.DataCollection;
using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class PhotoViewerViewModel : BaseViewModel
    {
        #region ** fields

        private IDataCollection<FileSystemFileViewModel> _photos;
        private int _currentIndex = -1;

        private FileSystemFileViewModel _firstPhoto;
        private FileSystemFileViewModel _secondPhoto;
        private FileSystemFileViewModel _thirdPhoto;
        private FileSystemFileViewModel _currentPhoto;
        private int _firstPhotoSign = 0;
        private int _secondPhotoSign = 1;
        private int _thirdPhotoSign = -1;
        private int _firstPhotoIndex = -1;
        private int _secondPhotoIndex = -1;
        private int _thirdPhotoIndex = -1;
        private bool _firstPhotoVisible = false;
        private bool _secondPhotoVisible = false;
        private bool _thirdPhotoVisible = false;

        private bool _showComments = false;
        private bool _isLoading = false;

        #endregion

        public PhotoViewerViewModel(FileExplorerViewModel fileExplorer)
        {
            FileExplorer = fileExplorer;
            _firstPhoto = CreateEmptyPhotoViewModel();
            _secondPhoto = CreateEmptyPhotoViewModel();
            _thirdPhoto = CreateEmptyPhotoViewModel();
            _currentPhoto = CreateEmptyPhotoViewModel();
            StatusNotificationDelay = TimeSpan.FromMilliseconds(700);
        }

        protected virtual FileSystemFileViewModel CreateEmptyPhotoViewModel()
        {
            return new FileSystemFileViewModel(FileExplorer, null, null, null);
        }

        #region ** object model

        public FileExplorerViewModel FileExplorer { get; private set; }

        public IFileSystemAsync FileSystem
        {
            get
            {
                return FileExplorer.FileSystem;
            }
        }

        protected virtual IDataCollection<FileSystemFileViewModel> Photos
        {
            get
            {
                return _photos;
            }
        }

        public virtual int PhotosCount
        {
            get
            {
                return _photos != null ? Photos.Count : 0;
            }
        }

        public int CurrentIndex
        {
            get
            {
                return _currentIndex;
            }
            set
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;
                    UpdatePhotos();
                }
            }
        }

        #region ** photos

        public virtual FileSystemFileViewModel FirstPhoto
        {
            get
            {
                return _firstPhoto;
            }
        }

        public virtual FileSystemFileViewModel SecondPhoto
        {
            get
            {
                return _secondPhoto;
            }
        }

        public virtual FileSystemFileViewModel ThirdPhoto
        {
            get
            {
                return _thirdPhoto;
            }
        }

        public virtual FileSystemFileViewModel CurrentPhoto
        {
            get
            {
                return _currentPhoto;
            }
        }

        public int FirstPhotoIndex
        {
            get
            {
                return _firstPhotoIndex;
            }
        }

        public int SecondPhotoIndex
        {
            get
            {
                return _secondPhotoIndex;
            }
        }

        public int ThirdPhotoIndex
        {
            get
            {
                return _thirdPhotoIndex;
            }
        }

        public int FirstPhotoSign
        {
            get
            {
                return _firstPhotoSign;
            }
        }

        public int SecondPhotoSign
        {
            get
            {
                return _secondPhotoSign;
            }
        }

        public int ThirdPhotoSign
        {
            get
            {
                return _thirdPhotoSign;
            }
        }

        public bool FirstPhotoVisible
        {
            get
            {
                return _firstPhotoVisible;
            }
        }

        public bool SecondPhotoVisible
        {
            get
            {
                return _secondPhotoVisible;
            }
        }

        public bool ThirdPhotoVisible
        {
            get
            {
                return _thirdPhotoVisible;
            }
        }

        #endregion

        #region ** comments

        public bool ToggleDetailsVisible
        {
            get
            {
                return CurrentPhoto.CanAddComment || CurrentPhoto.CanLike;
            }
        }

        public bool ShowComments
        {
            get
            {
                return _showComments;
            }
            set
            {
                _showComments = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region ** labels

        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string HomeLabel
        {
            get
            {
                return ApplicationResources.HomeLabel;
            }
        }

        public string BackLabel
        {
            get
            {
                return ApplicationResources.BackLabel;
            }
        }

        public string ShowCommentsLabel
        {
            get
            {
                return PhotoViewerResources.ShowCommentsLabel;
            }
        }

        public string HideCommentsLabel
        {
            get
            {
                return PhotoViewerResources.HideCommentsLabel;
            }
        }

        //public string PrevLabel
        //{
        //    get
        //    {
        //        return ApplicationResources.PreviousLabel;
        //    }
        //}

        //public string NextLabel
        //{
        //    get
        //    {
        //        return ApplicationResources.NextLabel;
        //    }
        //}

        public string DownloadingPhotoMessage
        {
            get
            {
                return PhotoViewerResources.DownloadingPhotoMessage;
            }
        }

        #endregion

        public string AlbumPath { get; set; }

        public string Status { get; private set; } = "";
        public double? StatusProgress { get; private set; } = null;

        public TimeSpan StatusNotificationDelay { get; private set; }

        public event EventHandler<AsyncEventArgs> StatusShown;
        public event EventHandler<AsyncEventArgs> StatusHidden;
        public event EventHandler<AsyncEventArgs> StatusChanged;

        public event EventHandler<CurrentPhotoRemovedEventArgs> CurrentPhotoRemoved;

        #endregion

        #region ** status
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        private async Task OnStatusShown()
        {
            var e = new AsyncEventArgs();
            if (StatusShown != null)
                StatusShown(this, e);
            await e.WaitDeferralsAsync();
        }

        private async Task OnStatusHidden()
        {
            var e = new AsyncEventArgs();
            if (StatusHidden != null)
                StatusHidden(this, e);
            await e.WaitDeferralsAsync();
        }

        private async Task OnStatusChanged()
        {
            var e = new AsyncEventArgs();
            if (StatusChanged != null)
                StatusChanged(this, e);
            await e.WaitDeferralsAsync();
        }

        #endregion

        #region ** implementation

        public async Task LoadAlbum(string albumPath, FileSystemFile file, CancellationToken cancellationToken)
        {
            AlbumPath = albumPath;
            IsLoading = true;
            var files = await FileSystem.GetFilesAsync(albumPath, cancellationToken);
            await files.LoadAsync();
            var photos = new C1FilterDataCollection<FileSystemFile>(files);
            await photos.FilterAsync(new FilterPredicateExpression(o => MimeType.Parse((o as FileSystemFile).ContentType).Type == "image"));
            int index = -1;
            if (file != null)
            {
                //index = await photos.IndexOfAsync(file, cancellationToken);
            }
            else
            {
            }
            _photos = CreatePhotosCollection(albumPath, photos);
            _photos.CollectionChanged += OnPhotosCollectionChanged;
            CurrentIndex = index;
            IsLoading = false;
        }

        public async Task LoadPhoto(string photoPath, CancellationToken cancellationToken)
        {
            IsLoading = true;
            AlbumPath = await FileSystem.GetFileParentIdAsync(photoPath, cancellationToken);
            var file = await FileSystem.GetFileAsync(photoPath, false, cancellationToken);
            _photos = CreatePhotosCollection(Path.GetParentPath(photoPath), new FileSystemFile[1] { file }.AsDataCollection());
            _currentIndex = 0;
            UpdatePhotos();
            IsLoading = false;
        }

        protected virtual PhotoViewCollection CreatePhotosCollection(string albumId, IDataCollection<FileSystemFile> photos)
        {
            return new PhotoViewCollection(FileExplorer, photos, albumId);
        }

        private async void OnPhotosCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                if (e.OldStartingIndex == CurrentIndex)
                {
                    var isLastItem = e.OldStartingIndex >= _photos.Count;
                    try
                    {
                        var e2 = new CurrentPhotoRemovedEventArgs(isLastItem);
                        CurrentPhotoRemoved?.Invoke(this, e2);
                        await e2.WaitDeferralsAsync();
                    }
                    catch { }
                    if (isLastItem)
                    {
                        _currentIndex--;
                    }
                    UpdatePhotos();
                    OnPropertyChanged("PhotosCount");
                }
            }
        }

        Func<int, int, int> calcSign = (x, y) => (((y + 3) - (x % 3)) % 3) - 1;
        Func<int, int, int> calcPhotoIndex = (x, y) => x + ((-y + 5) % 3) - ((x + ((-y + 5) % 3)) % 3) + ((y % 3) - 1);
        protected void UpdatePhotos()
        {
            var oldFirstPhoto = _firstPhoto;
            var oldSecondPhoto = _secondPhoto;
            var oldThirdPhoto = _thirdPhoto;
            var oldFirstPhotoVisible = _firstPhotoVisible;
            var oldSecondPhotoVisible = _secondPhotoVisible;
            var oldThirdPhotoVisible = _thirdPhotoVisible;
            var oldFirstPhotoSign = _firstPhotoSign;
            var oldSecondPhotoSign = _secondPhotoSign;
            var oldThirdPhotoSign = _thirdPhotoSign;

            _firstPhotoIndex = calcPhotoIndex(_currentIndex, 1);
            _secondPhotoIndex = calcPhotoIndex(_currentIndex, 2);
            _thirdPhotoIndex = calcPhotoIndex(_currentIndex, 3);
            _currentPhoto.PropertyChanged -= OnCurrentPhotoPropertyChanged;
            _firstPhoto.PropertyChanged -= OnFirstPhotoPropertyChanged;
            _secondPhoto.PropertyChanged -= OnSecondPhotoPropertyChanged;
            _thirdPhoto.PropertyChanged -= OnThirdPhotoPropertyChanged;
            _firstPhoto = _firstPhotoIndex >= 0 && _firstPhotoIndex < PhotosCount ? Photos[_firstPhotoIndex] : CreateEmptyPhotoViewModel();
            _secondPhoto = _secondPhotoIndex >= 0 && _secondPhotoIndex < PhotosCount ? Photos[_secondPhotoIndex] : CreateEmptyPhotoViewModel();
            _thirdPhoto = _thirdPhotoIndex >= 0 && _thirdPhotoIndex < PhotosCount ? Photos[_thirdPhotoIndex] : CreateEmptyPhotoViewModel();
            _firstPhoto.PropertyChanged += OnFirstPhotoPropertyChanged;
            _secondPhoto.PropertyChanged += OnSecondPhotoPropertyChanged;
            _thirdPhoto.PropertyChanged += OnThirdPhotoPropertyChanged;
            _currentPhoto = _currentIndex >= 0 && _currentIndex < PhotosCount ? Photos[_currentIndex] : CreateEmptyPhotoViewModel();
            _currentPhoto.PropertyChanged += OnCurrentPhotoPropertyChanged;

            _firstPhotoSign = calcSign(_currentIndex, 1);
            _secondPhotoSign = calcSign(_currentIndex, 2);
            _thirdPhotoSign = calcSign(_currentIndex, 3);
            _firstPhotoVisible = _firstPhotoIndex >= 0 && _firstPhotoIndex < PhotosCount;
            _secondPhotoVisible = _secondPhotoIndex >= 0 && _secondPhotoIndex < PhotosCount;
            _thirdPhotoVisible = _thirdPhotoIndex >= 0 && _thirdPhotoIndex < PhotosCount;

            OnPropertyChanged("CurrentPhoto");
            OnPropertyChanged("IsDownloadingContent");

            FetchComments();

            if (_firstPhotoVisible != oldFirstPhotoVisible)
            {
                OnPropertyChanged("FirstPhotoVisible");
            }
            if (_secondPhotoVisible != oldSecondPhotoVisible)
            {
                OnPropertyChanged("SecondPhotoVisible");
            }
            if (_thirdPhotoVisible != oldThirdPhotoVisible)
            {
                OnPropertyChanged("ThirdPhotoVisible");
            }

            if (_firstPhotoSign != oldFirstPhotoSign)
            {
                OnPropertyChanged("FirstPhotoSign");
            }
            if (_secondPhotoSign != oldSecondPhotoSign)
            {
                OnPropertyChanged("SecondPhotoSign");
            }
            if (_thirdPhotoSign != oldThirdPhotoSign)
            {
                OnPropertyChanged("ThirdPhotoSign");
            }

            if (!AreSamePhoto(_firstPhoto, oldFirstPhoto))
            {
                OnPropertyChanged("FirstPhoto");
            }
            if (!AreSamePhoto(_secondPhoto, oldSecondPhoto))
            {
                OnPropertyChanged("SecondPhoto");
            }
            if (!AreSamePhoto(_thirdPhoto, oldThirdPhoto))
            {
                OnPropertyChanged("ThirdPhoto");
            }
        }

        private void FetchComments()
        {
            //touch three pics thumbs up so that are loaded before shown.
            if (_firstPhoto != null)
            {
                var fp = _firstPhoto.GetLike();
                var fpc = _firstPhoto.Comments;
            }
            if (_secondPhoto != null)
            {
                var sp = _secondPhoto.GetLike();
                var spc = _secondPhoto.Comments;
            }
            if (_thirdPhoto != null)
            {
                var tp = _thirdPhoto.GetLike();
                var tpc = _thirdPhoto.Comments;
            }
        }

        private bool AreSamePhoto(FileSystemFileViewModel file1, FileSystemFileViewModel file2)
        {
            return file1.Item == null && file2.Item == null || file1.Item != null && file1.Item.Equals(file2.Item);
        }

        public async Task<List<FileSystemAction>> GetActionsAsync()
        {
            if (CurrentPhoto.Item == null)
                return new List<FileSystemAction>();
            return (from action in await FileExplorer.GetActionsAsync(AlbumPath, CurrentPhoto)
                    where action.Id == "Share" || action.Id == "Download" || action.Id == "DeleteFile"
                    select action).ToList();
        }

        private void OnFirstPhotoPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged("FirstPhoto." + e.PropertyName);
        }

        private void OnSecondPhotoPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged("SecondPhoto." + e.PropertyName);
        }

        private void OnThirdPhotoPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged("ThirdPhoto." + e.PropertyName);
        }

        private async void OnCurrentPhotoPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDownloadingContent")
            {
                await UpdateStatus();
                //OnPropertyChanged("IsDownloadingContent");
            }
            if (e.PropertyName == "DownloadingContentIsIndeterminate")
            {
                await UpdateStatus();
                //OnPropertyChanged("DownloadingContentIsIndeterminate");
            }
            if (e.PropertyName == "DownloadingContentProgress")
            {
                await UpdateStatus();
                //OnPropertyChanged("DownloadingContentProgress");
            }
        }

        private async Task UpdateStatus()
        {
            try
            {
                await _semaphore.WaitAsync();

                var status = IsDownloadingContent ? LoadingMessage : "";
                var progress = IsDownloadingContent && !DownloadingContentIsIndeterminate ? DownloadingContentProgress : (double?)null;

                if (status != Status || progress != StatusProgress)
                {
                    var showStatus = Status == "";
                    var hideStatus = status == "";
                    if (hideStatus)
                    {
                        Status = "";
                        StatusProgress = null;
                        await OnStatusHidden();
                    }
                    else
                    {
                        Status = status;
                        StatusProgress = progress;
                        await OnStatusChanged();
                        if (showStatus)
                        {
                            await OnStatusShown();
                            await Task.Delay(StatusNotificationDelay);
                        }
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public bool IsDownloadingContent
        {
            get
            {
                return CurrentPhoto != null && CurrentPhoto.IsDownloadingContent;
            }
        }

        public bool DownloadingContentIsIndeterminate
        {
            get
            {
                return CurrentPhoto != null && CurrentPhoto.DownloadingContentIsIndeterminate;
            }
        }

        public double DownloadingContentProgress
        {
            get
            {
                return CurrentPhoto != null ? CurrentPhoto.DownloadingContentProgress : 0;
            }
        }
        #endregion

        public bool IsLoading
        {
            get
            {
                return _isLoading;
            }
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string LoadingMessage
        {
            get
            {
                return ProgressResources.LoadingMessage;
            }
        }
    }

    public class PhotoViewCollection : TransformList<FileSystemFile, FileSystemFileViewModel>
    {
        public PhotoViewCollection(FileExplorerViewModel fileExplorer, IDataCollection<FileSystemFile> photos, string albumId)
            : base(photos)
        {
            FileExplorer = fileExplorer;
            AlbumId = albumId;
        }
        public FileExplorerViewModel FileExplorer { get; private set; }
        public string AlbumId { get; private set; }

        protected override FileSystemFileViewModel Transform(int index, FileSystemFile item)
        {
            var photoViewModel = FileExplorer.GetViewModel(AlbumId, item) as FileSystemFileViewModel;
            var task = photoViewModel.LoadDefaultComment();
            return photoViewModel;
        }
    }

    public class CurrentPhotoRemovedEventArgs : AsyncEventArgs
    {
        public CurrentPhotoRemovedEventArgs(bool isLastPhoto)
        {
            IsLastPhoto = isLastPhoto;
        }

        public bool IsLastPhoto { get; private set; }
    }
}
