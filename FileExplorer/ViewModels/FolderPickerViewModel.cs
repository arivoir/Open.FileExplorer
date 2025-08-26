using Open.FileSystemAsync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FileOpenPickerViewModel : FileExplorerViewModel
    {
        public FileOpenPickerViewModel(FileExplorerViewModel fileExplorer)
            : base(fileExplorer.AppService, fileExplorer.FileSystem)
        {
        }

        public override async Task ExecDefaultAction(FileSystemItemViewModel item, object originalSource)
        {
            if (item.Item.IsDirectory)
            {
                await base.ExecDefaultAction(item, originalSource);
            }
            else
            {
                Select(item);
            }
        }
    }

    public abstract class FolderPickerViewModel : FileExplorerViewModel
    {
        protected bool CanSubmit { get; set; }
        protected bool CanCreateDirectory { get; set; }

        public FolderPickerViewModel(IAppService appService, IFileSystemAsync fileSystem)
            : base(appService, fileSystem)
        {
            SubmitCommand = new TaskCommand(OnSubmit, CanSubmit1);
            OpenCreateDirectoryCommand = new TaskCommand(OpenCreateDirectory, CanOpenCreateDirectory);
            CancelCreateDirectoryCommand = new TaskCommand(CancelCreateDirectoryAsync);
            CreateDirectoryCommand = new TaskCommand(CreateDirectoryAsync);
            ShowFiles = false;
        }

        public FileSystemActionContext Context { get; set; }

        public TaskCommand SubmitCommand { get; protected set; }
        public TaskCommand OpenCreateDirectoryCommand { get; protected set; }
        public TaskCommand CancelCreateDirectoryCommand { get; protected set; }
        public TaskCommand CreateDirectoryCommand { get; protected set; }

        public event EventHandler Submit;



        public string SubmitButtonText { get; protected set; }
        public string CreateDirectoryButtonText { get; protected set; } = FileSystemResources.CreateFolderLabel;
        public bool CreatingDirectory { get; private set; }

        public PickFolderMode Mode { get; set; }

        public string SelectFolderLabel
        {
            get
            {
                return FolderPickerResources.SelectFolderLabel;
            }
        }

        public string CancelText
        {
            get
            {
                return ApplicationResources.CancelLabel;
            }
        }

        public FileSystemItemViewModel NewDirectoryViewModel { get; private set; }

        protected override async Task<List<FileSystemAction>> GetActualFileActions(FileSystemActionContext context, string targetDirectoryId)
        {
            var actions = await base.GetActualFileActions(context, targetDirectoryId);
            return actions.Where(a => a.Id == "OpenDirectory" || a.Id == "CreateDirectory" || a.Id == "Refresh").ToList();
        }

        protected override async Task BeforeEnteringAsync()
        {
            await base.BeforeEnteringAsync();
            CanCreateDirectory = await CanCreateDirectoryAsync(new FileSystemActionContext(CurrentDirectory));
            OpenCreateDirectoryCommand.OnCanExecuteChanged();
        }

        private bool CanOpenCreateDirectory(object arg)
        {
            return CanCreateDirectory;
        }

        private bool CanSubmit1(object arg)
        {
            return CanSubmit;
        }

        private Task OnSubmit(object arg)
        {
            Submit?.Invoke(this, new EventArgs());
            return Task.FromResult(true);
        }

        private Task OpenCreateDirectory(object arg)
        {
            var dir = Extensions.CreateDirectoryItem(CurrentDirectory, "", "", null);
            var dirVM = CreateViewModel(CurrentDirectory, dir);
            NewDirectoryViewModel = dirVM;
            CreatingDirectory = true;
            OnPropertyChanged("NewDirectoryViewModel");
            OnPropertyChanged("CreatingDirectory");
            return Task.FromResult(true);
        }

        private Task CancelCreateDirectoryAsync(object arg)
        {
            NewDirectoryViewModel = null;
            CreatingDirectory = false;
            OnPropertyChanged("NewDirectoryViewModel");
            OnPropertyChanged("CreatingDirectory");
            return Task.FromResult(true);
        }

        private async Task CreateDirectoryAsync(object arg)
        {
            var createdDir = await CreateDirectoryTransactionAsync(CurrentDirectory, NewDirectoryViewModel, CancellationToken.None);
            if (createdDir != null)
            {
                var createdDirId = FileSystem.GetDirectoryId(CurrentDirectory, createdDir.Id);
                await SetDirectoryAsync(createdDirId);

                NewDirectoryViewModel = null;
                CreatingDirectory = false;
                OnPropertyChanged("NewDirectoryViewModel");
                OnPropertyChanged("CreatingDirectory");
            }
        }
    }

    public class CopyFolderPickerViewModel : FolderPickerViewModel
    {
        public CopyFolderPickerViewModel(FileExplorerViewModel fileExplorer)
            : base(fileExplorer.AppService, fileExplorer.FileSystem)
        {
            SubmitButtonText = FileSystemResources.CopyHereLabel;
        }

        protected override async Task BeforeEnteringAsync()
        {
            await base.BeforeEnteringAsync();
            var result = await CanCopyToAsync(Context, CurrentDirectory);
            CanSubmit = result.Item1;
            if (!ItemsVisible)
            {
                Message = result.Item2 ?? FolderPickerResources.PressCopyToMessage;
                OnPropertyChanged("Message");
            }
            SubmitCommand.OnCanExecuteChanged();
        }
    }

    public class MoveFolderPickerViewModel : FolderPickerViewModel
    {
        public MoveFolderPickerViewModel(FileExplorerViewModel fileExplorer)
            : base(fileExplorer.AppService, fileExplorer.FileSystem)
        {
            SubmitButtonText = FileSystemResources.MoveHereLabel;
        }

        protected override async Task BeforeEnteringAsync()
        {
            await base.BeforeEnteringAsync();
            var result = await CanMoveToAsync(Context, CurrentDirectory);
            CanSubmit = result.Item1;
            if (!ItemsVisible)
            {
                Message = result.Item2 ?? FolderPickerResources.PressMoveToMessage;
                OnPropertyChanged("Message");
            }
            SubmitCommand.OnCanExecuteChanged();
        }
    }

    public class UploadFolderPickerViewModel : FolderPickerViewModel
    {
        public UploadFolderPickerViewModel(FileExplorerViewModel fileExplorer)
            : base(fileExplorer.AppService, fileExplorer.FileSystem)
        {
            SubmitButtonText = FileSystemResources.UploadHereLabel;
        }

        public string[] ContentTypes { get; set; }

        protected override async Task BeforeEnteringAsync()
        {
            await base.BeforeEnteringAsync();
            var result = await CanUploadToAsync(ContentTypes, CurrentDirectory);
            CanSubmit = result.Item1;
            if (!ItemsVisible)
            {
                Message = result.Item2 ?? FolderPickerResources.PressUploadToMessage;
                OnPropertyChanged("Message");
            }
            SubmitCommand.OnCanExecuteChanged();
        }
    }

    public class DownloadFolderPickerViewModel : FolderPickerViewModel
    {
        public DownloadFolderPickerViewModel(IAppService appService, IFileSystemAsync fileSystem)
            : base(appService, fileSystem)
        {
            Extensions = new LocalFileSystemExtensions(this);
            SubmitButtonText = FileSystemResources.DownloadHereLabel;
            Message = FolderPickerResources.PressDownloadToMessage;
        }

        public string[] ContentTypes { get; set; }

        protected override string GetRootName()
        {
            return ApplicationResources.PhoneLabel;
        }

        protected override async Task BeforeEnteringAsync()
        {
            await base.BeforeEnteringAsync();
            CanSubmit = true;
            if (!ItemsVisible)
            {
                Message = FolderPickerResources.PressDownloadToMessage;
                OnPropertyChanged("Message");
            }
            SubmitCommand.OnCanExecuteChanged();
        }
    }

}
