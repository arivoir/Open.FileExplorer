using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IProvider : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets the name of the provider displayed in provider list 
        /// as well as the folder name by default.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the hexadecimal string color that identifies the provider.
        /// </summary>
        string Color { get; }

        /// <summary>
        /// Gets the key that identifies the icon of the provider.
        /// </summary>
        string IconResourceKey { get; }

        /// <summary>
        /// Creates a new file system instance of this provider.
        /// </summary>
        /// <returns></returns>
        AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager);

        IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer);
    }
}
