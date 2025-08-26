using System;
using Open.DropBox;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class DropBoxDirectory : FileSystemDirectory
    {
        internal DropBoxDirectory(Item folder)
        {
            Id = Open.FileSystemAsync.Path.GetFileName(folder.PathLower);
            Name = folder.Name;
            Link = new Uri(string.Format("https://www.dropbox.com/home{0}", DropBoxClient.GetValidPath(folder.PathDisplay)));
            Path = folder.PathLower;
            //switch (folder.Icon)
            //{
            //    case "folder_public":
            //        Permissions = "Public";
            //        break;
            //    case "folder_user":
            //        Permissions = "Shared";
            //        break;
            //    case "folder_photos":
            //    default:
            //        Permissions = "";
            //        break;
            //}
            IsReadOnly = true;
        }

        public string Path { get; private set; }
    }
}
