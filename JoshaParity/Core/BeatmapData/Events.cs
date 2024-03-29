using Newtonsoft.Json;
namespace JoshaParity.Core.BeatmapData
{
    /// <summary>
    /// BPM Event
    /// </summary>
    public class BPMEvent
    {
        public override string ToString() { return $"Beat: {Beat}, BPM: {BPM}"; }
        [JsonProperty("b")] public float Beat { get; set; }
        [JsonProperty("m")] public float BPM { get; set; }
    }

    /// <summary>
    /// Events (Contains BPMEvents, Type 100)
    /// </summary>
    public class EventV2
    {
        [JsonProperty("_time")] public float Beat { get; set; }
        [JsonProperty("_type")] public int Type { get; set; }
        [JsonProperty("_value")] public int Value { get; set; }
        [JsonProperty("_floatValue")] public float FloatValue { get; set; }
    }
}
