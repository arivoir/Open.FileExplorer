using System.Collections.Generic;
using System.Threading.Tasks;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class ConnectionsViewModel : BaseViewModel
    {
        #region fields

        private static List<ProviderViewModel> _providers;

        #endregion

        public ConnectionsViewModel(FileExplorerViewModel fileExplorer)
        {
            FileExplorer = fileExplorer;
            _providers = new List<ProviderViewModel>();
            foreach (var provider in GlobalFileExplorerExtensions.Providers)
            {
                _providers.Add(CreateProviderViewModel(provider));
            }
        }

        protected virtual ProviderViewModel CreateProviderViewModel(IProvider provider)
        {
            return new ProviderViewModel(this, provider);
        }

        #region object model

        public FileExplorerViewModel FileExplorer { get; set; }
        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string AddAccountLabel
        {
            get
            {
                return GlobalResources.AddAccountLabel;
            }
        }

        public string ConnectMessage
        {
            get
            {
                return ApplicationResources.ConnectMessage;
            }
        }

        public virtual IEnumerable<ProviderViewModel> Providers
        {
            get
            {
                return _providers;
            }
        }

        #endregion

        internal async Task AddProvider(ProviderViewModel providerViewModel)
        {
            await FileExplorer.AddProvider(providerViewModel);
        }
    }
}
