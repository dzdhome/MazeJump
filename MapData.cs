using System;

namespace MazeJump
{
    /// <summary>
    /// Map data represented as a grid of tile type ints.
    /// Tile type values are persisted in map.json, so they must stay stable.
    /// </summary>
    public class MapData
    {
        // Tile type constants (values are stored in map.json - do not renumber)
        public const int TileEmpty = 0;
        public const int TileSolid = 1;
        public const int TileEntrance = 2;
        public const int TileExit = 3;
        public const int TileLava = 4;
        public const int TileCheckpoint = 5;

        public int[][] Grid { get; set; }

        public MapData()
        {
            Grid = CreateEmptyGrid();
        }

        public static int Rows => 13;
        public static int Columns => 20;

        public static int[][] CreateEmptyGrid()
        {
            var grid = new int[Rows][];
            for (var row = 0; row < Rows; row++)
            {
                grid[row] = new int[Columns];
            }
            return grid;
        }

        public static MapData CreateDefault()
        {
            var map = new MapData();

            // Ground: solid on the far-left and far-right, with a gap in the middle.
            for (var x = 0; x < Columns; x++)
            {
                if (x < 3 || x > 14)
                {
                    map.Grid[Rows - 1][x] = TileSolid;
                }
            }

            // Platforms
            map.Grid[Rows - 2][4] = TileSolid;
            map.Grid[Rows - 3][4] = TileSolid;
            map.Grid[Rows - 2][6] = TileSolid;
            map.Grid[Rows - 2][7] = TileSolid;
            map.Grid[Rows - 3][8] = TileSolid;
            map.Grid[Rows - 2][10] = TileSolid;
            map.Grid[Rows - 2][11] = TileSolid;
            map.Grid[Rows - 3][12] = TileSolid;
            map.Grid[Rows - 3][13] = TileSolid;

            // Dangerous tiles
            map.Grid[Rows - 2][9] = TileLava;

            // Checkpoint (记录点) tiles - before and after the lava
            map.Grid[Rows - 1][4] = TileCheckpoint;
            map.Grid[Rows - 2][14] = TileCheckpoint;

            // Entrance (spawn point) and exit
            map.Grid[Rows - 1][1] = TileEntrance;
            map.Grid[Rows - 1][18] = TileExit;

            return map;
        }

        public MapData Clone()
        {
            var clone = new MapData();
            for (var row = 0; row < Rows; row++)
            {
                clone.Grid[row] = (int[])Grid[row].Clone();
            }
            return clone;
        }
    }
}