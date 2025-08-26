using System.Collections.Generic;

namespace Open.FileExplorer
{
    public class GoogleResponse<T>
    {
        public GoogleResponse(int totalResults, int startIndex, int itemsPerPage, IList<T> items)
        {
            TotalResults = totalResults;
            StartIndex = startIndex;
            ItemsPerPage = itemsPerPage;
            Items = items;
        }
        public IList<T> Items { get; set; }
        public int TotalResults { get; private set; }
        public int StartIndex { get; private set; }
        public int ItemsPerPage { get; private set; }

    }
}
