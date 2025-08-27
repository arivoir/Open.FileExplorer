using Open.FileSystemAsync;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Open.FileExplorer
{
    [DataContract]
    public class AccountDirectory : FileSystemDirectory
    {
        #region fields

        private IProvider _provider = null;

        #endregion

        #region initialization

        public AccountDirectory()
        {
        }

        #endregion

        #region object model

        [DataMember(Name = "id", IsRequired = true)]
        public new string Id
        {
            get
            {
                return base.Id;
            }
            set
            {
                base.Id = value;
            }
        }

        [DataMember(Name = "name", IsRequired = true)]
        public new string Name
        {
            get
            {
                return base.Name;
            }
            set
            {
                base.Name = value;
            }
        }

        [DataMember(Name = "provider", IsRequired = true)]
        public string ProviderId { get; set; }
        [DataMember(Name = "connection_string")]
        public string ConnectionString { get; set; }
        [DataMember(Name = "user_id", EmitDefaultValue = false)]
        public string UserId { get; set; }
        #region back compatibility
        [DataMember(Name = "account", EmitDefaultValue = false)]
        public string Account 
        { 
            get { return UserId; }
            set { UserId = value; }
        }
        [DataMember(Name = "type", EmitDefaultValue = false)]
        public string Type { get; set; }
        #endregion
        public IFileSystemAsync FileSystem { get; set; }
        internal IAuthenticationManager AuthenticationManager { get; set; }

        public IProvider Provider
        {
            get
            {
                if (_provider == null && !string.IsNullOrWhiteSpace(ProviderId))
                {
                    var providerId = ProviderId.Replace("Woopiti.Portable.FileSystem.SkyDriveProvider", "Woopiti.FileExplorer.OneDriveProvider");
                    providerId = providerId.Replace("Woopiti.Portable.FileSystem.GooglePlusProvider", "Woopiti.FileExplorer.GooglePhotosProvider");
                    providerId = providerId.Replace("Woopiti.Portable.FileSystem.", "Woopiti.FileExplorer.");
                    var type = System.Type.GetType(string.Format("{0}, Woopiti.FileExplorer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", providerId));
                    if (type != null)
                    {
                        _provider = type.GetTypeInfo().DeclaredConstructors.Where(c => c.GetParameters().Length == 0).First().Invoke(new object[0]) as IProvider;
                    }
                }
                return _provider;
            }
        }

        public long? UsedSize { get; internal set; }
        public long? TotalSize { get; internal set; }

        public long? AvailableSize
        {
            get
            {
                return UsedSize.HasValue && TotalSize.HasValue ? TotalSize - UsedSize : null;
            }
        }


        #endregion
    }
}
