using System.Collections.Generic;
using System.Threading.Tasks;

namespace Open.FileExplorer
{
    public interface IAppTiles
    {
        bool AreTilesSupported { get; }
        Task<IEnumerable<TileInfo>> GetTiles();
        Task AddTile(TileInfo tile, object sourceOrigin);
        Task RemoveTile(TileInfo tile);
        Task UpdateTile(TileInfo tile);
    }
}
