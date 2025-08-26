using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public class GooglePhotosDirectory : FileSystemDirectory
    {
        public GooglePhotosDirectory(string id, string name)
        {
            Id = id;
            Name = name;
            IsSpecial = true;
            IsReadOnly = true;
        }
    }
}
