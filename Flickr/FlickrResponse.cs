using System.Collections.Generic;

namespace Open.FileExplorer
{
    public class FlickrResponse<T>
    {
        public FlickrResponse(int total, int page, int perPage, IList<T> items)
        {
            Total = total;
            Page = page;
            PerPage = perPage;
            Items = items;
        }
        public IList<T> Items { get; set; }
        public int Total { get; private set; }
        public int Page { get; private set; }
        public int PerPage { get; private set; }

    }
}
