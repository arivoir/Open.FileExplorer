using System;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IAppNavigation
    {
        Task GoToMain(string path = "");
        Task GoToConnections(object placementTarget);
        Task GoToSearch();

        Task GoToSettings();

        Task GoToAbout();

        Task GoToTransactions(Transaction txn = null, object placementTarget = null);
        Task GoToStoreReview();

        Task NavigateUrl(Uri url);
        Task<bool> CanOpenAlbum(CancellationToken cancellationToken);
        Task OpenAlbum(string albumPath, FileSystemFile file, object placementTarget, CancellationToken cancellationToken);
        Task<bool> CanOpenFile(string contentType, string filePath, CancellationToken cancellationToken);
        Task OpenFile(string contentType, string filePath, object placementTarget, CancellationToken cancellationToken);
    }
}
