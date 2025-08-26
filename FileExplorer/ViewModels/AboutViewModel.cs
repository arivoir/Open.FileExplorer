using System;
using System.Linq;
using Open.FileExplorer.Strings;

namespace Open.FileExplorer
{
    public class AboutViewModel
    {
        public AboutViewModel(IAppService appService)
        {
            AppService = appService;
        }

        public IAppService AppService { get; private set; }

        public string ApplicationName
        {
            get
            {
                return ApplicationResources.ApplicationName;
            }
        }

        public string AboutLabel
        {
            get
            {
                return AboutResources.AboutLabel;
            }
        }

        public string ApplicationPresentation
        {
            get
            {
                return AboutResources.ApplicationPresentation;
            }
        }

        public string ApplicationVersion
        {
            get
            {
                var version = AppService.AppVersion;
                return string.Format(AboutResources.ApplicationVersion, version);
            }
        }

        public string ContactMail
        {
            get
            {
                return AboutResources.ContactMail;
            }
        }

        public string ContactMailLink
        {
            get
            {
                return string.Format("mailto:{0}", AboutResources.ContactMail);
            }
        }

        public string Website
        {
            get
            {
                return AboutResources.Website;
            }
        }

        public string WebsiteLink
        {
            get
            {
                return string.Format("http://{0}", AboutResources.Website);
            }
        }

        public string ConnectionChargesMessage
        {
            get
            {
                return AboutResources.ConnectionChargesMessage;
            }
        }

        public string ContactMailLabel
        {
            get
            {
                return AboutResources.ContactMailLabel;
            }
        }

        public string WebsiteLabel
        {
            get
            {
                return AboutResources.WebsiteLabel;
            }
        }

        public string ProvidersList
        {
            get
            {
                return AboutResources.ProvidersList;
            }
        }

        public string[] Providers
        {
            get
            {
                return AboutResources.ProvidersList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(str => str.Trim()).ToArray();
            }
        }
    }
}
