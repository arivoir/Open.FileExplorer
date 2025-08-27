using Open.FileSystemAsync;
using System;
using System.Globalization;

namespace Open.FileExplorer
{
    public class OneDriveDirectory : FileSystemDirectory
    {
        #region initialization

        public OneDriveDirectory(string id, string name)
        {
            Id = id;
            Name = name;
        }

        internal OneDriveDirectory(Open.OneDrive.Item folder, string parentDirId = null)
        {
            Id = folder.Id;
            Name = folder.Name;
            Description = folder.Description;
            if (folder.Folder != null)
                Count = folder.Folder.ChildCount;
            Size = folder.Size;
            Link = new Uri("https://onedrive.live.com/redir?resid=" + folder.Id);
            if (parentDirId != null)
            {
                ParentDirId = parentDirId;
            }
            else if (folder.ParentReference != null)
            {
                ParentDirId = OneDriveFileSystem.GetDirPath(folder.ParentReference.Path, folder.ParentReference.Id);
            }
            if (folder.SpecialFolder != null)
            {
                SpecialFolder = folder.SpecialFolder.Name;
            }
            //switch (folder.SharedWith.Access)
            //{
            //    case "Public":
            //        Permissions = "Public";
            //        break;
            //    case "Just me":
            //    default:
            //        Permissions = "";
            //        break;
            //}
            if (!string.IsNullOrWhiteSpace(folder.CreatedDateTime))
                CreatedDate = DateTime.Parse(folder.CreatedDateTime, CultureInfo.InvariantCulture.DateTimeFormat);
            if (!string.IsNullOrWhiteSpace(folder.LastModifiedDateTime))
                ModifiedDate = DateTime.Parse(folder.LastModifiedDateTime, CultureInfo.InvariantCulture.DateTimeFormat);
            if (folder.CreatedBy != null && folder.CreatedBy.User != null)
                Owner = new OneDrivePerson(folder.CreatedBy.User);
            IsReadOnly = true;
        }

        #endregion

        #region object model

        /// <summary>
        /// The Documents folder.
        /// </summary>
        public const string Documents = "documents";

        /// <summary>
        /// The Photos folder.
        /// </summary>
        public const string Photos = "photos";

        /// <summary>
        /// The Camera Roll Backup folder.
        /// </summary>
        public const string CameraRol = "cameraRoll";

        /// <summary>
        /// The application's personal folder. Usually in /Apps/{Application Name}
        /// </summary>
        public const string AppRoot = "approot";

        /// <summary>
        /// The Music folder.
        /// </summary>
        public const string Music = "music";

        /// <summary>
        /// The Favorites folder.
        /// </summary>
        public const string Favorites = "favorites";

        public string Description { get; set; }
        public string ParentDirId { get; private set; }
        public string SpecialFolder { get; private set; }

        #endregion
    }
}
