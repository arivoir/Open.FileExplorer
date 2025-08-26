using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Open.FileExplorer
{
    public static class ConfigurationManager
    {
        public static AppSettings AppSettings
        {
            get;
            private set;
        }

        public static void Initialize(Stream stream)
        {
            try
            {
                var doc = XDocument.Load(stream);
                var dict = (from settingNode in
                                doc.Descendants("appSettings").Descendants("add")
                            select new
                            {
                                Key = settingNode.Attribute("key").Value,
                                Value = settingNode.Attribute("value").Value
                            }).ToDictionary(s => s.Key, s => s.Value);
                AppSettings = new AppSettings(dict);
            }
            finally
            {
                stream.Dispose();
            }
        }
    }

    public class AppSettings
    {
        private Dictionary<string, string> settings;

        public AppSettings(Dictionary<string, string> settings)
        {
            this.settings = settings;
        }

        public string this[string key]
        {
            get
            {
                if (settings.ContainsKey(key))
                    return settings[key];
                else
                    return null;
            }
        }
    }
}
