using System.Collections.Generic;

namespace MazeJump
{
    /// <summary>
    /// A collection of maps stored in a single JSON file.
    /// </summary>
    public class MapCollection
    {
        public List<MapData> Maps { get; set; } = new();

        public static MapCollection CreateDefault(int count = 9)
        {
            var collection = new MapCollection();
            for (int i = 0; i < count; i++)
            {
                var map = MapData.CreateDefault();

                // Map 2 (index 1) demonstrates the portal pair feature.
                // A tall wall (columns 6-8) blocks the path, so the player MUST use the
                // portal entry before the wall to teleport past it to the portal exit.
                if (i == 1)
                {
                    map = CreatePortalDemoMap();
                }

                collection.Maps.Add(map);
            }
            return collection;
        }

        /// <summary>
        /// Builds a purpose-built demo map that showcases the one-way portal pair.
        /// A solid wall forces the player onto the portal entry, which teleports them
        /// to the portal exit on the far side of the wall.
        /// </summary>
        private static MapData CreatePortalDemoMap()
        {
            var map = new MapData();
            var grid = map.Grid;

            // Solid ground all the way across so the player can walk anywhere at the bottom
            for (int col = 0; col < MapData.Columns; col++)
            {
                grid[MapData.Rows - 1][col] = MapData.TileSolid;
            }

            // Wall: columns 6-8 spanning rows 1..12 (blocks the mid-map path)
            for (int col = 6; col <= 8; col++)
            {
                for (int row = 1; row <= MapData.Rows - 1; row++)
                {
                    grid[row][col] = MapData.TileSolid;
                }
            }

            // Entrance (spawn) and exit
            grid[MapData.Rows - 1][1] = MapData.TileEntrance;
            grid[MapData.Rows - 1][17] = MapData.TileExit;

            // Portal pair on the ground:
            // entry before the wall, exit after the wall
            grid[MapData.Rows - 1][4] = MapData.TilePortalEntry;
            grid[MapData.Rows - 1][13] = MapData.TilePortalExit;

            // A checkpoint so players get a respawn point on the far side
            grid[MapData.Rows - 1][14] = MapData.TileCheckpoint;

            return map;
        }
    }
}