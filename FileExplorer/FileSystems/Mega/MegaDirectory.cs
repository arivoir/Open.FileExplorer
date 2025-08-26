using Open.FileSystemAsync;
using Open.Mega;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class MegaDirectory : FileSystemDirectory
    {
        internal MegaDirectory(Node dir)
        {
            Node = dir;
            Id = dir.Id;
            if (dir.Type == NodeType.Directory)
            {
                Name = dir.Name;
            }
            else if (dir.Type == NodeType.Root)
            {
                Name = MegaResources.RootLabel;
            }
            else if (dir.Type == NodeType.Inbox)
            {
                Name = MegaResources.InboxLabel;
            }
            else if (dir.Type == NodeType.Trash)
            {
                Name = MegaResources.TrashLabel;
            }
            IsSpecial = dir.Type != NodeType.Directory;
            //Parents = dir.Parents;
            //Link = new Uri(dir.AlternateLink);
            //Permissions = dir.Shared ? "Shared" : "";
            //if (!string.IsNullOrWhiteSpace(dir.CreatedDate))
            //    CreatedDate = DateTime.Parse(dir.CreatedDate, CultureInfo.InvariantCulture.DateTimeFormat);
            //if (!string.IsNullOrWhiteSpace(dir.ModifiedDate))
            //    ModifiedDate = DateTime.Parse(dir.ModifiedDate, CultureInfo.InvariantCulture.DateTimeFormat);
            IsReadOnly = true;
        }

        internal Node Node { get; set; }
        //internal bool IsTrashed { get; set; }
    }
}
