using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Open.FileSystemAsync;

namespace Open.FileExplorer
{
    public abstract class Provider : IProvider
    {
        public abstract string Name { get; }

        public abstract string Color { get; }

        public virtual string IconResourceKey
        {
            get { return Name.Replace(" ", "").Replace("+", "Plus") + "Icon"; }
        }

        public abstract AuthenticatedFileSystem CreateFileSystem(IAuthenticationManager authenticationManager);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public virtual IFileExplorerExtensions GetExplorerExtensions(FileExplorerViewModel explorer)
        {
            return new ProviderFileExplorerExtensions(explorer, this);
        }
    }
}
