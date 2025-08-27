using Open.FileSystemAsync;
using Open.Instagram;

namespace Open.FileExplorer.Instagram
{
    public class InstagramFile : FileSystemFile
    {
        internal InstagramFile(Item file)
        {
            Id = file.Id;
            Name = file.Caption != null ? file.Caption.Text : "";
            Thumbnail = file.Images.Thumbnail.Url;
            Link = new Uri(file.Link);
            if (file.Type == "image")
            {
                Content = new Uri(file.Images.StandardResolution.Url);
            }
            else if (file.Type == "video")
            {
                Content = new Uri(file.Videos.StandardResolution.Url);
            }
            _contentType = file.Type == "image" ? "image/jpeg" : file.Type == "video" ? "video/mp4" : "";
            //Width = file.Images.StandardResolution.Width;
            //Height = file.Images.StandardResolution.Height;
            CreatedDate = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds(long.Parse(file.CreatedTime));
            Owner = new InstagramPerson(file.User);
            IsReadOnly = true;
        }

        //public double Width { get; private set; }
        //public double Height { get; private set; }
        public Uri Content { get; private set; }
        public override string ContentType
        {
            get
            {
                return _contentType;
            }
        }
    }
}
