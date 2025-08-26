using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public class ProviderViewModel : BaseViewModel
    {
        public IProvider Provider { get; set; }
        public ConnectionsViewModel ConnectionsViewModel { get; set; }

        public ProviderViewModel(ConnectionsViewModel connectionsViewModel, IProvider provider)
        {
            ConnectionsViewModel = connectionsViewModel;
            Provider = provider;
            AddProviderCommand = new TaskCommand(AddProvider);
        }

        private async Task AddProvider(object arg)
        {
            await ConnectionsViewModel.AddProvider(this);
        }

        public string Name
        {
            get
            {
                return Provider.Name;
            }
        }

        public string IconKey
        {
            get
            {
                return Provider.IconResourceKey;
            }
        }

        public TaskCommand AddProviderCommand { get; protected set; }

    }
}
