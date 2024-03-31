using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JoshaParity
{
    /// <summary>
    /// Parsed Difficulty Information
    /// </summary>
    public class DifficultyData {
        public string Version { get; set; } = "";
        public List<Note> Notes { get; set; } = [];
        public List<Bomb> Bombs { get; set; } = [];
        public List<Obstacle> Obstacles { get; set; } = [];
        public List<Arc> Arcs { get; set; } = [];
        public List<Chain> Chains { get; set; } = [];
        public List<BPMEvent> BPMChanges { get; set; } = [];
        public Dictionary<string, JToken> UnloadedAttributes { get; set; } = [];
    }

    /// <summary>
    /// Handles deserialization of V2, V3 and V4 BeatmapData
    /// </summary>
    public class BeatmapSerializer : JsonConverter<DifficultyData>
    {
        /// <summary>
        /// Converts Version string to BeatmapRevision Enum
        /// </summary>
        public static BeatmapRevision VersionToBeatmapRev(string version)
        {
            return version switch
            {
                "2.0.0" => BeatmapRevision.V200,
                "2.2.0" => BeatmapRevision.V220,
                "2.4.0" => BeatmapRevision.V240,
                "2.5.0" => BeatmapRevision.V250,
                "2.6.0" => BeatmapRevision.V260,
                "3.0.0" => BeatmapRevision.V300,
                "3.1.0" => BeatmapRevision.V310,
                "3.2.0" => BeatmapRevision.V320,
                "3.3.0" => BeatmapRevision.V330,
                "4.0.0" => BeatmapRevision.V400,
                _ => BeatmapRevision.Unknown,
            };
        }

        /// <summary>
        /// Handles reading difficulty dat json
        /// </summary>
        public override DifficultyData? ReadJson(JsonReader reader, Type objectType, DifficultyData? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            DifficultyData data = new();
            JObject jsonObj = JObject.Load(reader);
            JToken verToken = jsonObj["_version"] ?? jsonObj["version"] ?? "";
            if (string.IsNullOrEmpty(verToken.ToString())) return null;
            data.Version = verToken.ToString();
            BeatmapRevision version = VersionToBeatmapRev(data.Version);

            switch (version)
            {
                case BeatmapRevision.Unknown:
                    Debug.WriteLine("Unsupported Version"); return null;
                case var v when v < BeatmapRevision.V300:
                    DeserializeV2(jsonObj, data, version);
                    break;
                case var v when v is < BeatmapRevision.V400 and >= BeatmapRevision.V300:
                    DeserializeV3(jsonObj, data);
                    break;
                case var v when v >= BeatmapRevision.V400:
                    DeserializeV4(jsonObj, data);
                    break;
            }

            // Cache remaining json data
            HashSet<string> loadedProperties =
            [ "_version", "version", "_notes", "colorNotes", "bombNotes", "colorNotesData", "bombNotesData", "obstacles", "obstaclesData",
                "_obstacles", "sliders", "_sliders", "burstSliders", "arcs", "arcsData", "chains", "chainsData", "bpmEvents" ];

            data.UnloadedAttributes = jsonObj.Properties()
                .Where(property => !loadedProperties.Contains(property.Name))
                .ToDictionary(property => property.Name, property => property.Value);

            return data;
        }
        public override void WriteJson(JsonWriter writer, DifficultyData? value, JsonSerializer serializer) {
            Debug.WriteLine("Serializing back to file is not supported");
            writer.WriteNull();
            return;
        }

        /// <summary>
        /// Handles Deserializing V2 Data
        /// </summary>
        private static void DeserializeV2(JObject jsonObj, DifficultyData data, BeatmapRevision version)
        {
            JToken notesArray = jsonObj["_notes"] ?? new JArray();
            JToken obstaclesArray = jsonObj["_obstacles"] ?? new JArray();
            List<EventV2> events = jsonObj["_events"]?.ToObject<List<EventV2>>() ?? [];

            data.Bombs.AddRange(notesArray.Where(noteToken => (int)(noteToken["_type"] ?? 0) == 3).Select(Bomb.DeserializeV2));
            data.Notes.AddRange(notesArray.Where(noteToken => (int)(noteToken["_type"] ?? 0) != 3).Select(Note.DeserializeV2));
            data.Obstacles.AddRange(obstaclesArray.Select(Obstacle.DeserializeV2));
            data.BPMChanges.AddRange(events.Where(v2event => v2event.Type == 100).Select(v2event => new BPMEvent { b = v2event.Beat, m = v2event.FloatValue }));
            if (version is BeatmapRevision.V260) {
                JToken slidersArray = jsonObj["_sliders"] ?? new JArray();
                data.Arcs.AddRange(slidersArray.Select(Arc.DeserializeV2));
            }
        }
        /// <summary>
        /// Handles Deserializing V3 Data
        /// </summary>
        private static void DeserializeV3(JObject jsonObj, DifficultyData data)
        {
            JToken notesArrayV3 = jsonObj["colorNotes"] ?? new JArray();
            JToken bombsArrayV3 = jsonObj["bombNotes"] ?? new JArray();
            JToken obstaclesArrayV3 = jsonObj["obstacles"] ?? new JArray();
            JToken arcsArrayV3 = jsonObj["sliders"] ?? new JArray();
            JToken chainsArrayV3 = jsonObj["burstSliders"] ?? new JArray();
            List<BPMEvent> bpmChangesV3 = jsonObj["bpmEvents"]?.ToObject<List<BPMEvent>>() ?? [];

            data.Notes.AddRange(notesArrayV3.Select(Note.DeserializeV3));
            data.Bombs.AddRange(bombsArrayV3.Select(Bomb.DeserializeV3));
            data.Obstacles.AddRange(obstaclesArrayV3.Select(Obstacle.DeserializeV3));
            data.Arcs.AddRange(arcsArrayV3.Select(Arc.DeserializeV3));
            data.Chains.AddRange(chainsArrayV3.Select(Chain.DeserializeV3));
            data.BPMChanges.AddRange(bpmChangesV3);
        }
        /// <summary>
        /// Handles Deserializing V4 Data
        /// </summary>
        private static void DeserializeV4(JObject jsonObj, DifficultyData data)
        {
            JToken colorNotesMeta = jsonObj["colorNotesData"] ?? new JArray();
            JToken arcMeta = jsonObj["arcsData"] ?? new JArray();
            JToken chainMeta = jsonObj["chainsData"] ?? new JArray();
            JToken bombMeta = jsonObj["bombNotesData"] ?? new JArray();
            JToken obstaclesMeta = jsonObj["obstaclesData"] ?? new JArray();

            JToken notesArrayV4 = jsonObj["colorNotes"] ?? new JArray();
            JToken bombsArrayV4 = jsonObj["bombNotes"] ?? new JArray();
            JToken obstaclesArrayV4 = jsonObj["obstacles"] ?? new JArray();
            JToken arcsArrayV4 = jsonObj["arcs"] ?? new JArray();
            JToken chainsArrayV4 = jsonObj["chains"] ?? new JArray();

            data.Notes.AddRange(notesArrayV4.Select(noteToken => Note.DeserializeV4(noteToken, colorNotesMeta)));
            data.Bombs.AddRange(bombsArrayV4.Select(bombToken => Bomb.DeserializeV4(bombToken, bombMeta)));
            data.Obstacles.AddRange(obstaclesArrayV4.Select(obstacleToken => Obstacle.DeserializeV4(obstacleToken, obstaclesMeta)));
            data.Arcs.AddRange(arcsArrayV4.Select(arcToken => Arc.DeserializeV4(arcToken, arcMeta, colorNotesMeta)));
            data.Chains.AddRange(chainsArrayV4.Select(chainToken => Chain.DeserializeV4(chainToken, chainMeta, colorNotesMeta)));
        }
    }

    /// <summary>
    /// Representation of Cut Directions for Beat Objects
    /// </summary>
    public enum CutDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3,
        UpLeft = 4,
        UpRight = 5,
        DownLeft = 6,
        DownRight = 7,
        Any = 8
    }

    /// <summary>
    /// Represents the version of the BeatmapData
    /// </summary>
    public enum BeatmapRevision
    {
        Unknown = 0,
        V200 = 1,
        V220 = 2,
        V240 = 3,
        V250 = 4,
        V260 = 5,
        V300 = 6,
        V310 = 7,
        V320 = 8,
        V330 = 9,
        V400 = 10
    }
}