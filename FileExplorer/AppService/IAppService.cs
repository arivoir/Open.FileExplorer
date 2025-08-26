using System;
using System.IO;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public interface IAppService :
        IAppSettings,
        IAppNavigation,
        IAppDialogs,
        IAppTiles,
        IAppSharing,
        IAuthenticationBroker
    {
        /****Encription****/
        bool DataProtectionEnabled { get; }
        Task<byte[]> ProtectData(byte[] userData);
        Task<byte[]> UnprotectData(byte[] userData);

        IFileSystemStorage GetLocalStorage();
        IFileSystemStorage GetTemporaryStorage();
        Task<IFileSystemStorage> GetPublicStorage(string baseFolderPath);

        Task<byte[]> ResizeImage(Stream imageStream, double width, double height, bool fill);
        IAuthenticationManager GetAuhtenticationManager(AccountDirectory connection);
    }
}
