using Open.FileSystemAsync;

namespace Open.FileExplorer.X
{
    public class TwitterFileViewModel : FileSystemFileViewModel
    {
        #region initialization

        public TwitterFileViewModel(FileExplorerViewModel fileExplorer, string dirId, FileSystemItem item, IFileInfo file)
            : base(fileExplorer, dirId, item, file)
        {
        }

        #endregion

        protected override bool NameIsRequired
        {
            get
            {
                return false;
            }
        }

        public override async Task<string> GetDefaultCommentTextAsync()
        {
            var user = await (FileSystem as ISocialExtension).GetCurrentUserAsync(ItemId) as TwitterPerson;
            return "@" + user.ScreenName;
        }
    }
}
