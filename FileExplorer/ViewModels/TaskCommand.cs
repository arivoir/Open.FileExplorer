using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class TaskCommand : INotifyPropertyChanged
    {
        #region  ** fields

        private Func<object, bool> _canExecuteAction;
        private Func<object, Task> _executeAsyncAction;
        private SemaphoreSlim _executeSemaphore = new SemaphoreSlim(1);

        #endregion

        #region ** initialization

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskCommand"/> class.
        /// </summary>
        /// <param name="action">The execute action.</param>
        public TaskCommand(Func<object, Task> action)
        {
            _executeAsyncAction = action;
            _canExecuteAction = x => true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskCommand"/> class.
        /// </summary>
        /// <param name="action">The execute action.</param>
        /// <param name="canExecute">The can execute.</param>
        public TaskCommand(Func<object, Task> action, Func<object, bool> canExecute)
        {
            _executeAsyncAction = action;
            _canExecuteAction = canExecute;
        }

        public TaskCommand(TaskCommand command)
        {
            _executeAsyncAction = command._executeAsyncAction;
            _canExecuteAction = command._canExecuteAction;
            command.CanExecuteChanged += (s, e) =>
            {
                OnCanExecuteChanged();
            };
        }

        #endregion

        #region ** object model

        public bool IsExecuting { get; private set; }

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region ** implementation

        public bool CanExecute(object parameter = null)
        {
            if (IsExecuting)
                return false;
            return _canExecuteAction(parameter);
        }

        public async Task ExecuteAsync(object parameter = null)
        {
            try
            {
                await _executeSemaphore.WaitAsync();
                IsExecuting = true;
                OnPropertyChanged("IsExecuting");
                OnCanExecuteChanged();
                await _executeAsyncAction(parameter);
            }
            catch { }
            finally
            {
                IsExecuting = false;
                OnPropertyChanged("IsExecuting");
                OnCanExecuteChanged();
                _executeSemaphore.Release();
            }
        }

        public virtual void OnCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
                CanExecuteChanged(this, new EventArgs());
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}