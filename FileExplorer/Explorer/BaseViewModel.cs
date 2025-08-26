using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Open.FileExplorer
{
    public class BaseViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        public BaseViewModel()
        {
            Errors = new ErrorsDictionary();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string Error { get; private set; }

        public bool ErrorVisible
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Error);
            }
        }

        /// <summary>
        /// Raises property changed event.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        protected virtual void OnPropertyChanged([CallerMemberName]string propertyName = "")
        {
            if (PropertyChanged != null)
            {
                try
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
                catch { }
            }
        }

        public ErrorsDictionary Errors { get; private set; }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public IEnumerable GetErrors(string propertyName)
        {
            if (Errors.ContainsKey(propertyName))
                yield return Errors[propertyName];
        }

        public bool HasErrors
        {
            get { return Errors.Count > 0; }
        }

        public void ClearErrors()
        {
            if (Error != null)
            {
                Error = null;
                OnPropertyChanged("Error");
                OnPropertyChanged("ErrorVisible");
            }
            if (Errors.Count > 0)
            {
                var errors = Errors.ToArray();
                Errors.Clear();
                foreach (var error in errors)
                {
                    RaiseErrorsChanged(error.Key);
                }
                OnPropertyChanged("Errors");
            }
        }

        public void SetError(ValidationError error, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                Error = error.Message;
                OnPropertyChanged("Error");
                OnPropertyChanged("ErrorVisible");
            }
            Errors[propertyName] = error;
            RaiseErrorsChanged(propertyName);
            OnPropertyChanged("Errors");
        }

        private void RaiseErrorsChanged(string propertyName)
        {
            if (ErrorsChanged != null)
                ErrorsChanged(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    public class ErrorsDictionary : Dictionary<string, ValidationError>
    {
        public new ValidationError this[string key]
        {
            get
            {
                ValidationError error = null;
                base.TryGetValue(key, out error);
                return error;
            }
            set
            {
                base[key] = value;
            }
        }
    }
}
