using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class FileSystemAction
    {
        protected FileSystemAction(string id,
            string displayName,
            FileSystemActionContext context)
        {
            Id = id;
            DisplayName = displayName;
            IsEnabled = true;
            Context = context;
            Category = new FileSystemActionCategory("");
        }

        public FileSystemAction(string id,
            string displayName,
            FileSystemActionContext context,
            Func<FileSystemAction, FileSystemActionEventArgs, Task> action)
            : this(id, displayName, context)
        {
            Action += action;
        }

        public FileSystemAction(string id,
            string displayName,
            FileSystemActionContext context,
            Action<FileSystemAction, FileSystemActionEventArgs> action)
            : this(id, displayName, context)
        {
            Action += (a, e) => { action(a, e); return Task.FromResult(true); };
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public bool NeedsInternetAccess { get; set; } = true;
        public bool NeedsNotificationsEnabled { get; set; } = false;
        private Func<FileSystemAction, FileSystemActionEventArgs, Task> Action;
        public FileSystemActionCategory Category { get; set; }
        public bool IsDefault { get; set; }
        public bool IsDestructive { get; set; }
        public bool IsEnabled { get; set; }
        public FileSystemActionContext Context { get; private set; }
        public object Tag { get; set; }
        public object SourceOrigin { get; set; }

        public CancellationToken CancellationToken { get; set; }

        public virtual async Task ExecuteActionAsync(IAppService appService, object sourceOrigin)
        {
            SourceOrigin = sourceOrigin;
            if (Action != null)
            {
                if (NeedsInternetAccess)
                {
                    var isNetworkAvailable = await appService.IsNetworkAvailableAsync();
                    if (!isNetworkAvailable)
                    {
                        await appService.ShowErrorAsync(FileSystemResources.NetworkUnavailableMessage);
                        return;
                    }
                }
                if (NeedsNotificationsEnabled)
                {
                    if (!await appService.RequestNotificationsAuthorization())
                    {
                        await appService.ShowErrorAsync(ApplicationResources.EnableNotificationsMessage);
                        return;
                    }
                }
                await Action(this, new FileSystemActionEventArgs(Context));
            }
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            return obj is FileSystemAction && Id == (obj as FileSystemAction).Id;
        }

    }

    public class FileSystemActionList : FileSystemAction
    {
        public FileSystemActionList(string id, string displayName)
            : base(id, displayName, null)
        {
            Actions = new List<FileSystemAction>();
        }
        public List<FileSystemAction> Actions { get; set; }

        public override async Task ExecuteActionAsync(IAppService appService, object sourceOrigin)
        {
            var options = Actions.Select(a => a.DisplayName).ToArray();
            var result = await appService.ShowSelectAsync(null, options, sourceOrigin);
            var selectedOption = Actions[result];
            await selectedOption.ExecuteActionAsync(appService, sourceOrigin);
        }
    }

    public class FileSystemActionEventArgs : EventArgs
    {
        internal FileSystemActionEventArgs(FileSystemActionContext context)
        {
            Context = context;
        }

        public FileSystemActionContext Context { get; private set; }
    }
}
