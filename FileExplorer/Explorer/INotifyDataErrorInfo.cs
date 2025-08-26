using System;

namespace Open.FileExplorer
{
    public interface INotifyDataErrorInfo
    {
        event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;
    }

    public class DataErrorsChangedEventArgs : EventArgs
    {
        private string propertyName;

        public DataErrorsChangedEventArgs(string propertyName)
        {
            // TODO: Complete member initialization
            this.propertyName = propertyName;
        }

    }
}
