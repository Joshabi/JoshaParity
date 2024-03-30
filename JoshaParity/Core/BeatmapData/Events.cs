using Newtonsoft.Json;
namespace JoshaParity
{
    /// <summary>
    /// BPM Event
    /// </summary>
    public class BPMEvent
    {
        public override string ToString() { return $"Beat: {b}, BPM: {m}"; }
        [JsonProperty("b")] public float b { get; set; }
        [JsonProperty("m")] public float m { get; set; }
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
