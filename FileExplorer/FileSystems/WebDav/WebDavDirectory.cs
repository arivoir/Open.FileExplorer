using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Open.FileSystemAsync;
using Open.WebDav;

namespace Open.FileExplorer
{
    public class WebDavDirectory : FileSystemDirectory
    {
        internal WebDavDirectory(string name, string fullPath)
        {
            Id = name;
            Name = name;
            FullPath = fullPath;
            IsReadOnly = true;
        }

        internal WebDavDirectory(XElement directory)
        {
            var name = WebDavFileSystem.GetName(directory);
            Id = name;
            Name = name;
            FullPath = WebDavFileSystem.GetHRef(directory);
            var creationDate = directory.Descendants(WebDavClient.CreationDate).FirstOrDefault();
            if (creationDate != null)
                CreatedDate = DateTime.Parse(creationDate.Value, CultureInfo.InvariantCulture.DateTimeFormat);
            var getLastModified = directory.Descendants(WebDavClient.GetLastModified).FirstOrDefault();
            if (getLastModified != null)
                ModifiedDate = DateTime.Parse(getLastModified.Value, CultureInfo.InvariantCulture.DateTimeFormat);
            IsReadOnly = true;
        }

        public string FullPath { get; private set; }
    }
}
