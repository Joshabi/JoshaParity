using Newtonsoft.Json.Linq;
namespace JoshaParity.Core.BeatmapData
{
    public abstract class BeatObject {
        public float b { get; set; }
        public float ms { get; set; }
        public object? CustomData { get; set; }
    }
    public abstract class BeatGridObject : BeatObject {
        public int x { get; set; }
        public int y { get; set; }
    }

    /// <summary>
    /// Parsed Representation of a Note object
    /// </summary>
    public class Note : BeatGridObject
    {
        public override string ToString()
        {
            return $"Beat: {b}, X: {x}, Y: {y}, C: {c}, D: {d}" +
                   $"{(a.HasValue ? $", Angle Offset: {a}" : "")}" +
                   $"{(r.HasValue ? $", Rotation Lane: {r}" : "")}" +
                   $"{(i.HasValue ? $", Metadata Index: {i}" : "")}";
        }
        public CutDirection d { get; set; }
        public int c { get; set; }
        public float? a { get; set; }
        public int? r { get; set; }
        public int? i { get; set; }

        /// <summary>
        /// Deserialize a Note from V2 Beatmap Data
        /// </summary>
        public static Note DeserializeV2(JToken noteToken)
        {
            return new Note {
                b = (float)(noteToken["_time"] ?? 0),
                x = (int)(noteToken["_lineIndex"] ?? 0),
                y = (int)(noteToken["_lineLayer"] ?? 0),
                c = (int)(noteToken["_type"] ?? 0),
                d = (CutDirection)(int)(noteToken["_cutDirection"] ?? 0)
            };
        }
        /// <summary>
        /// Deserialize a Note from V3 Beatmap Data
        /// </summary>
        public static Note DeserializeV3(JToken noteToken) {
            return new Note {
                b = (float)(noteToken["b"] ?? 0),
                x = (int)(noteToken["x"] ?? 0),
                y = (int)(noteToken["y"] ?? 0),
                c = (int)(noteToken["c"] ?? 0),
                d = (CutDirection)(int)(noteToken["d"] ?? 0),
                a = (float)(noteToken["a"] ?? 0)
            };
        }
        /// <summary>
        /// Deserialize a Note from V4 Beatmap Data
        /// </summary>
        public static Note DeserializeV4(JToken noteToken, JToken dataToken)
        {
            Note note = new() {
                b = (float)(noteToken["b"] ?? 0),
                r = (int)(noteToken["r"] ?? 0),
                i = (int)(noteToken["i"] ?? 0)
            };

            var data = dataToken[note.i];
            if (data is not null) {
                note.x = (int)(data["x"] ?? 0);
                note.y = (int)(data["y"] ?? 0);
                note.c = (int)(data["c"] ?? 0);
                note.d = (CutDirection)(int)(data["d"] ?? 0);
                note.a = (float)(data["a"] ?? 0);
            }

            return note;
        }
    }
}