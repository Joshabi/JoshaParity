using Newtonsoft.Json.Linq;
namespace JoshaParity.Core.BeatmapData
{
    /// <summary>
    /// Parsed representation of a Bomb object
    /// </summary>
    public class Bomb : BeatGridObject
    {
        public override string ToString()
        {
            return $"Beat: {b}, X: {x}, Y: {y}" +
                   $"{(r.HasValue ? $", Rotation Lane: {r}" : "")}" +
                   $"{(i.HasValue ? $", Metadata Index: {i}" : "")}";
        }
        public int? r { get; set; }
        public int? i { get; set; }

        /// <summary>
        /// Serialize a Bomb from V2 Beatmap Data (V2 Note of Type 3)
        /// </summary>
        public static Bomb DeserializeV2(JToken noteToken)
        {
            return new Bomb
            {
                b = (float)(noteToken["_time"] ?? 0),
                x = (int)(noteToken["_lineIndex"] ?? 0),
                y = (int)(noteToken["_lineLayer"] ?? 0)
            };
        }
        /// <summary>
        /// Serialize a Bomb from V3 Beatmap Data
        /// </summary>
        public static Bomb DeserializeV3(JToken bombToken)
        {
            return new Bomb
            {
                b = (float)(bombToken["b"] ?? 0),
                x = (int)(bombToken["x"] ?? 0),
                y = (int)(bombToken["y"] ?? 0)
            };
        }
        /// <summary>
        /// Serialize a Bomb from V4 Beatmap Data
        /// </summary>
        public static Bomb DeserializeV4(JToken bombToken, JToken dataToken)
        {
            Bomb bomb = new()
            {
                b = (float)(bombToken["b"] ?? 0),
                r = (int)(bombToken["r"] ?? 0),
                i = (int)(bombToken["i"] ?? 0)
            };

            // Extracting values from colorNotesData using index
            var dataIndex = bomb.i ?? 0;
            var data = dataToken[dataIndex];
            if (data is not null)
            {
                bomb.x = (int)(data["x"] ?? 0);
                bomb.y = (int)(data["y"] ?? 0);
            }

            return bomb;
        }
    }
}