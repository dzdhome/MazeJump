using System.Collections.Generic;

namespace JumpGameMonoGame
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
                collection.Maps.Add(MapData.CreateDefault());
            }
            return collection;
        }
    }
}