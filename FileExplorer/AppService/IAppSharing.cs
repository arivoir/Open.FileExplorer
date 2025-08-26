using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IAppSharing
    {
        bool CanShareLink();
        bool CanShareFile();
        Task ShareLinkAsync(IEnumerable<SharedSource> packages, object sourceOrigin);
    }

    public class SharedSource
    {
        public SharedSource(string title)
        {
            Title = title;
        }
        public string Title { get; private set; }
    }
    public class SharedLink : SharedSource
    {
        public SharedLink(string title, Uri link)
            : base(title)
        {
            Link = link;
        }
        public Uri Link { get; private set; }
    }

    public class SharedFile : SharedSource
    {
        private Func<Task<IFileInfo>> _getFileFunc;

        public SharedFile(string title, Func<Task<IFileInfo>> getFileFunc)
            : base(title)
        {
            _getFileFunc = getFileFunc;
        }

        public async Task<IFileInfo> GetFileAsync()
        {
            return await _getFileFunc();
        }
    }
}
