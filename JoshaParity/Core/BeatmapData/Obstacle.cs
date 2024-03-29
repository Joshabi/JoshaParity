using Newtonsoft.Json.Linq;
namespace JoshaParity.Core.BeatmapData
{
    /// <summary>
    /// Parsed representation of a Obstacle object
    /// </summary>
    public class Obstacle : BeatGridObject
    {
        public override string ToString()
        {
            return $"Beat: {b}, Duration: {d}, X: {x}, Y: {y}, W: {w}, H: {h}" +
                   $"{(t.HasValue ? $", Type: {t}" : "")}" +
                   $"{(r.HasValue ? $", Rotation Lane: {r}" : "")}" +
                   $"{(i.HasValue ? $", Metadata Index: {i}" : "")}";
        }
        public float d { get; set; }
        public int w { get; set; }
        public int h { get; set; }
        public int? t { get; set; }
        public int? r { get; set; }
        public int? i { get; set; }

        /// <summary>
        /// Serialize a Obstacle from V2 Beatmap Data
        /// </summary>
        public static Obstacle DeserializeV2(JToken noteToken)
        {
            Obstacle obstacle = new()
            {
                b = (float)(noteToken["_time"] ?? 0),
                d = (float)(noteToken["_duration"] ?? 0),
                x = (int)(noteToken["_lineIndex"] ?? 0),
                y = (int)(noteToken["_lineLayer"] ?? 0),
                t = (int)(noteToken["Type"] ?? 0)
            };

            if (obstacle.t == 0) { obstacle.y = 0; obstacle.h = 5; }
            else if (obstacle.t == 1) { obstacle.y = 2; obstacle.h = 3; }
            return obstacle;
        }
        /// <summary>
        /// Serialize a Obstacle from V3 Beatmap Data
        /// </summary>
        public static Obstacle DeserializeV3(JToken noteToken)
        {
            return new Obstacle
            {
                b = (float)(noteToken["b"] ?? 0),
                d = (float)(noteToken["d"] ?? 0),
                x = (int)(noteToken["x"] ?? 0),
                y = (int)(noteToken["y"] ?? 0),
                w = (int)(noteToken["w"] ?? 0),
                h = (int)(noteToken["h"] ?? 0)
            };
        }
        /// <summary>
        /// Serialize a Obstacle from V4 Beatmap Data
        /// </summary>
        public static Obstacle DeserializeV4(JToken noteToken, JToken dataToken)
        {
            Obstacle obstacle = new()
            {
                b = (float)(noteToken["b"] ?? 0),
                r = (int)(noteToken["r"] ?? 0),
                i = (int)(noteToken["i"] ?? 0)
            };

            // Extracting values from colorNotesData using index
            var dataIndex = obstacle.i ?? 0;
            var data = dataToken[dataIndex];
            if (data is not null)
            {
                obstacle.d = (float)(data["d"] ?? 0);
                obstacle.x = (int)(data["x"] ?? 0);
                obstacle.y = (int)(data["y"] ?? 0);
                obstacle.w = (int)(data["w"] ?? 0);
                obstacle.h = (int)(data["h"] ?? 0);
            }

            return obstacle;
        }
    }
}