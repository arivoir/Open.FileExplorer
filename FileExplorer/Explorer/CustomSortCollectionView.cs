using C1.DataCollection;
using Open.FileSystemAsync;
using System.Collections.Generic;
using System.Linq;

namespace Open.FileExplorer
{
    public class CustomSortCollectionView<T> : C1SortDataCollection<T>
        where T : class
    {
        public CustomSortCollectionView(IEnumerable<T> source, IEnumerable<string> allowedSortPaths)
            : base(source)
        {
            AllowedSortPaths = allowedSortPaths ?? new string[0];
        }

        public IEnumerable<string> AllowedSortPaths { get; private set; }

        public override bool CanSort(params SortDescription[] sortDescriptions)
        {
            return sortDescriptions.All(sd => AllowedSortPaths.Contains(sd.SortPath));
        }
    }
}
