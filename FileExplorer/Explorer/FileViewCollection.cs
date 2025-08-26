using System.Collections.Specialized;
using System.ComponentModel;
using C1.DataCollection;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class FileViewCollection : TransformList<FileSystemItem, FileSystemItemViewModel>
    {
        private FileExplorerViewModel _explorer;
        private string _dirId;
        private string _updateOnItemPropertyChange;

        public FileViewCollection(FileExplorerViewModel explorer, string dirId, IDataCollection<FileSystemItem> source, string updateOnItemPropertyChange)
            : base(source)
        {
            Items = source;
            IsEnabled = true;
            _dirId = dirId;
            _explorer = explorer;
            _updateOnItemPropertyChange = updateOnItemPropertyChange;
        }

        public IDataCollection<FileSystemItem> Items { get; private set; }
        public bool IsEnabled { get; set; }

        protected override FileSystemItemViewModel Transform(int index, FileSystemItem item)
        {
            if (item == null)
                return null;
            return _explorer.GetViewModel(_dirId, item);
        }

        protected override void OnItemSet(int index, FileSystemItemViewModel item)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        protected override void OnItemRemoved(int index, FileSystemItemViewModel item)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsEnabled && _updateOnItemPropertyChange != null &&
                e.PropertyName == _updateOnItemPropertyChange)
            {
                var itemVM2 = sender as FileSystemItemViewModel;
                var index = this.IndexOf(itemVM2);
                if (index >= 0)
                    OnCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, itemVM2, itemVM2, index));
            }
        }

        protected override FileSystemItem TransformBack(FileSystemItemViewModel item)
        {
            return (item as FileSystemItemViewModel).Item;
        }
    }
}
