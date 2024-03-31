using Newtonsoft.Json.Linq;
namespace JoshaParity
{
    /// <summary>
    /// Base Parsed representation of a "Slider"
    /// </summary>
    public class Slider : Note
    {
        public override string ToString()
        {
            return $"HeadBeat: {b}, HeadX: {x}, HeadY: {y}, Color: {c}, HeadCutDirection: {d}" +
                   $", TailBeat: {tb}, TailX: {tx}, TailY: {ty}" +
                   $"{(hi.HasValue ? $", HeadMetadataIndex: {hi}" : "")}" +
                   $"{(hr.HasValue ? $", HeadRotationLane: {hr}" : "")}" +
                   $"{(tr.HasValue ? $", TailRotationLane: {tr}" : "")}";
        }
        public float tb { get; set; }
        public int tx { get; set; }
        public int ty { get; set; }
        public int? hi { get; set; }
        public float? hr { get; set; }
        public float? tr { get; set; }
        public float tms { get; set; }
    }

    /// <summary>
    /// Parsed representation of an Arc
    /// </summary>
    public class Arc : Slider
    {
        public override string ToString()
        {
            return base.ToString() +
                   $"\nHeadLengthMult: {mu}, TailCutDirection: {tc}, TailLengthMult: {tmu}, MidAnchorMode: {m}" +
                   $"{(ti.HasValue ? $", Tail Metadata Index: {ti}" : "")}" +
                   $"{(ai.HasValue ? $", Arc Metadata Index: {ai}" : "")}";
        }
        public float mu { get; set; }
        public CutDirection tc { get; set; }
        public float tmu { get; set; }
        public int m { get; set; }
        public int? ti { get; set; }
        public int? ai { get; set; }

        /// <summary>
        /// Deserialize an Arc from V2 Beatmap Data
        /// </summary>
        public static new Arc DeserializeV2(JToken arcToken)
        {
            return new Arc
            {
                c = (int)(arcToken["_colorType"] ?? 0),
                b = (float)(arcToken["_headTime"] ?? 0),
                x = (int)(arcToken["_headLineIndex"] ?? 0),
                y = (int)(arcToken["_headLineLayer"] ?? 0),
                d = (int)(arcToken["_headCutDirection"] ?? 0),
                mu = (float)(arcToken["_headControlPointLengthMultiplier"] ?? 0),
                tb = (float)(arcToken["_tailTime"] ?? 0),
                tx = (int)(arcToken["_tailLineIndex"] ?? 0),
                ty = (int)(arcToken["_tailLineLayer"] ?? 0),
                tc = (CutDirection)(int)(arcToken["_tailCutDirection"] ?? 0),
                tmu = (float)(arcToken["_tailControlPointLengthMultiplier"] ?? 0),
                m = (int)(arcToken["_sliderMidAnchorMode"] ?? 0)
            };
        }
        /// <summary>
        /// Deserialize an Arc from V3 Beatmap Data
        /// </summary>
        public static new Arc DeserializeV3(JToken arcToken)
        {
            return new Arc
            {
                c = (int)(arcToken["c"] ?? 0),
                b = (float)(arcToken["b"] ?? 0),
                x = (int)(arcToken["x"] ?? 0),
                y = (int)(arcToken["y"] ?? 0),
                d = (int)(arcToken["d"] ?? 0),
                mu = (float)(arcToken["mu"] ?? 0),
                tb = (float)(arcToken["tb"] ?? 0),
                tx = (int)(arcToken["tx"] ?? 0),
                ty = (int)(arcToken["ty"] ?? 0),
                tc = (CutDirection)(int)(arcToken["tc"] ?? 0),
                tmu = (float)(arcToken["tmu"] ?? 0),
                m = (int)(arcToken["m"] ?? 0)
            };
        }
        /// <summary>
        /// Deserialize an Arc from V4 Beatmap Data
        /// </summary>
        public static Arc DeserializeV4(JToken arcToken, JToken arcMetaToken, JToken ColorNoteToken)
        {
            Arc arc = new()
            {
                b = (float)(arcToken["hb"] ?? 0),
                tb = (float)(arcToken["tb"] ?? 0),
                hr = (float)(arcToken["hr"] ?? 0),
                tr = (float)(arcToken["tr"] ?? 0),
                hi = (int)(arcToken["hi"] ?? 0),
                ti = (int)(arcToken["ti"] ?? 0),
                ai = (int)(arcToken["ai"] ?? 0)
            };

            JToken? data = ColorNoteToken[arc.hi];
            if (data is not null)
            {
                arc.c = (int)(data["c"] ?? 0);
                arc.x = (int)(data["x"] ?? 0);
                arc.y = (int)(data["y"] ?? 0);
                arc.d = (int)(data["d"] ?? 0);
            }

            data = ColorNoteToken[arc.ti];
            if (data is not null)
            {
                arc.x = (int)(data["x"] ?? 0);
                arc.y = (int)(data["y"] ?? 0);
                arc.tc = (CutDirection)(int)(data["d"] ?? 0);
            }

            data = arcMetaToken[arc.ai];
            if (data is not null)
            {
                arc.mu = (float)(data["m"] ?? 0);
                arc.tmu = (float)(data["tm"] ?? 0);
                arc.m = (int)(data["a"] ?? 0);
            }

            return arc;
        }
    }

    /// <summary>
    /// Parsed representation of a Chain
    /// </summary>
    public class Chain : Slider
    {
        public override string ToString() {
            return base.ToString() +
                   $"\nSliceCount: {sc}, SquishFactor: {sf}" +
                   $"{(ci.HasValue ? $", Chain Metadata Index: {ci}" : "")}";
        }
        public int sc { get; set; }
        public float sf { get; set; }
        public int? ci { get; set; }

        /// <summary>
        /// Deserialize an Chain from V3 Beatmap Data
        /// </summary>
        public static new Chain DeserializeV3(JToken chainToken)
        {
            return new Chain
            {
                c = (int)(chainToken["c"] ?? 0),
                b = (float)(chainToken["b"] ?? 0),
                x = (int)(chainToken["x"] ?? 0),
                y = (int)(chainToken["y"] ?? 0),
                d = (int)(chainToken["d"] ?? 0),
                tb = (float)(chainToken["tb"] ?? 0),
                tx = (int)(chainToken["tx"] ?? 0),
                ty = (int)(chainToken["ty"] ?? 0),
                sc = (int)(chainToken["sc"] ?? 0),
                sf = (float)(chainToken["s"] ?? 1.0f)
            };
        }
        /// <summary>
        /// Deserialize an Chain from V4 Beatmap Data
        /// </summary>
        public static Chain DeserializeV4(JToken chainToken, JToken chainMetaToken, JToken ColorNoteToken)
        {
            Chain chain = new()
            {
                b = (float)(chainToken["hb"] ?? 0),
                tb = (float)(chainToken["tb"] ?? 0),
                hr = (float)(chainToken["hr"] ?? 0),
                tr = (float)(chainToken["tr"] ?? 0),
                hi = (int)(chainToken["i"] ?? 0),
                ci = (int)(chainToken["ci"] ?? 0)
            };

            JToken? data = ColorNoteToken[chain.hi];
            if (data is not null)
            {
                chain.c = (int)(chainToken["c"] ?? 0);
                chain.x = (int)(chainToken["x"] ?? 0);
                chain.y = (int)(chainToken["y"] ?? 0);
                chain.d = (int)(chainToken["d"] ?? 0);
            }

            data = chainMetaToken[chain.ci];
            if (data is not null)
            {
                chain.x = (int)(chainToken["tx"] ?? 0);
                chain.y = (int)(chainToken["ty"] ?? 0);
                chain.sc = (int)(chainToken["c"] ?? 0);
                chain.sf = (float)(chainToken["s"] ?? 1.0f);
            }

            return chain;
        }
    }
}