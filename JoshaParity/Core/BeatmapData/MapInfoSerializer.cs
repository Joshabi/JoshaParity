using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace JoshaParity.Core.BeatmapData
{
    /// <summary>
    /// Class representing Info.dat map data
    /// </summary>
    public class SongData
    {
        public string MapPath { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string SubTitle { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Mapper { get; set; } = string.Empty;
        public AudioData Song { get; set; } = new();
        public float Shuffle { get; set; }
        public float ShufflePeriod { get; set; }
        public float SongTimeOffset { get; set; }
        public string CoverImageFilename { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string AllDirectionsEnvironmentName { get; set; } = string.Empty;
        public List<string> EnvironmentNames { get; set; } = [];
        public List<DifficultyInfo> DifficultyBeatmaps { get; set; } = [];

        public override string ToString() {
            return $"Version: {Version}, Title: {Title}, SubTitle: {SubTitle}, Artist: {Artist}, Mapper: {Mapper}, Song: {Song} Shuffle: {Shuffle}, ShufflePeriod: {ShufflePeriod}, CoverImageFilename: {CoverImageFilename}, EnvironmentNames: [{string.Join(", ", EnvironmentNames)}], DifficultyBeatmaps: [{string.Join(", ", DifficultyBeatmaps)}]";
        }
    }

    /// <summary>
    /// Represents the metadata for a given Difficulty
    /// </summary>
    public class DifficultyInfo
    {
        public string Characteristic { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public BeatmapDifficultyRank Rank { get; set; } = 0;
        public BeatmapAuthors BeatmapAuthors { get; set; } = new();
        public int EnvironmentNameIdx { get; set; }
        public int BeatmapColorSchemeIdx { get; set; }
        public float NoteJumpMovementSpeed { get; set; }
        public float NoteJumpStartBeatOffset { get; set; }
        public string BeatmapDataFilename { get; set; } = string.Empty;
        public string LightshowDataFilename { get; set; } = string.Empty;
        public DifficultyData DifficultyData { get; set; } = new();

        public override string ToString()
        {
            return $"\nCharacteristic: {Characteristic}, Difficulty: {Difficulty}, Mappers: [{string.Join(", ", BeatmapAuthors.Mappers)}] Lighters: [{string.Join(", ", BeatmapAuthors.Lighters)}], EnvironmentNameIdx: {EnvironmentNameIdx}, BeatmapColorSchemeIdx: {BeatmapColorSchemeIdx}, NoteJumpMovementSpeed: {NoteJumpMovementSpeed}, NoteJumpStartBeatOffset: {NoteJumpStartBeatOffset}, BeatmapDataFilename: {BeatmapDataFilename}, LightshowDataFilename: {LightshowDataFilename}";
        }
    }

    /// <summary>
    /// Difficulty ID Enum.
    /// </summary>
    public enum BeatmapDifficultyRank
    {
        Easy = 1,
        Normal = 3,
        Hard = 5,
        Expert = 7,
        ExpertPlus = 9
    }

    /// <summary>
    /// Represents Mapper/Lighter metadata
    /// </summary>
    public class BeatmapAuthors
    {
        public List<string> Mappers { get; set; } = [];
        public List<string> Lighters { get; set; } = [];
        public override string ToString()
        {
            return $"Mappers: {Mappers}, Lighters:{Lighters}";
        }
    }

    /// <summary>
    /// Represents Song metadata
    /// </summary>
    public class AudioData
    {
        public string SongFilename { get; set; } = string.Empty;
        public float SongDuration { get; set; }
        public string AudioDataFilename { get; set; } = string.Empty;
        public float BPM { get; set; }
        public float LUFS { get; set; }
        public float PreviewStartTime { get; set; }
        public float PreviewDuration { get; set; }
        public string SongPreviewFilename { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"Song Filename: {SongFilename}, Duration:{SongDuration}, Audio Data Filename:{AudioDataFilename}, BPM:{BPM}, LUFS:{LUFS}, Preview Start:{PreviewStartTime}, Preview Duration:{PreviewDuration}";
        }
    }

    /// <summary>
    /// Handles deserialization of V2, V2.1 and V4 Beatmap Info.dat
    /// </summary>
    public class MapInfoSerializer : JsonConverter<SongData> {

        /// <summary>
        /// Handles reading difficulty dat json
        /// </summary>
        public override SongData? ReadJson(JsonReader reader, Type objectType, SongData? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            SongData data = new();
            JObject jsonObj = JObject.Load(reader);
            JToken verToken = jsonObj["_version"] ?? jsonObj["version"] ?? "";
            if (string.IsNullOrEmpty(verToken.ToString())) return null;
            data.Version = verToken.ToString();

            switch (data.Version) {
                case "2.0.0" or "2.1.0":
                    DeserializeV2(jsonObj, ref data);
                    break;
                case "4.0.0":
                    DeserializeV4(jsonObj, ref data);
                    break;
                default: Debug.WriteLine("Unsupported Version"); return null;
            }
            return data;
        }

        private static void DeserializeV2(JObject jObject, ref SongData data)
        {
            data.Title = jObject["_songName"]?.ToString() ?? "";
            data.SubTitle = jObject["_songSubName"]?.ToString() ?? "";
            data.Artist = jObject["_songAuthorName"]?.ToString() ?? "";
            data.Mapper = jObject["_levelAuthorName"]?.ToString() ?? "";
            data.Song = new()
            {
                SongFilename = jObject["_songFilename"]?.ToString() ?? "",
                BPM = (float)(jObject["_beatsPerMinute"] ?? 0),
                PreviewStartTime = (float)(jObject["_previewStartTime"] ?? 0),
                PreviewDuration = (float)(jObject["_previewDuration"] ?? 0)
            };
            data.CoverImageFilename = jObject["_coverImageFilename"]?.ToString() ?? "";
            data.EnvironmentNames = jObject["_environmentNames"]?.ToObject<List<string>>() ?? [];
            data.EnvironmentName = jObject["_environmentName"]?.ToString() ?? "";
            data.AllDirectionsEnvironmentName = jObject["_allDirectionsEnvironmentName"]?.ToString() ?? "";
            data.DifficultyBeatmaps = [];

            // Parse DifficultyBeatmaps
            var difficultyBeatmapSets = jObject["_difficultyBeatmapSets"]?.ToObject<List<JObject>>() ?? [];
            foreach (var difficultyBeatmapSet in difficultyBeatmapSets) {
                var characteristicName = difficultyBeatmapSet["_beatmapCharacteristicName"]?.ToString();
                var difficultyBeatmaps = difficultyBeatmapSet["_difficultyBeatmaps"]?.ToObject<List<JObject>>() ?? [];
                foreach (var difficultyBeatmap in difficultyBeatmaps)
                {
                    data.DifficultyBeatmaps.Add(new DifficultyInfo
                    {
                        Characteristic = characteristicName ?? "",
                        Difficulty = difficultyBeatmap["_difficulty"]?.ToString() ?? "",
                        Rank = (BeatmapDifficultyRank)(int)(difficultyBeatmap["_difficultyRank"] ?? 9),
                        BeatmapDataFilename = difficultyBeatmap["_beatmapFilename"]?.ToString() ?? "",
                        NoteJumpMovementSpeed = (int)(difficultyBeatmap["_noteJumpMovementSpeed"] ?? 10),
                        NoteJumpStartBeatOffset = (int)(difficultyBeatmap["_noteJumpStartBeatOffset"] ?? 0),
                        BeatmapColorSchemeIdx = (int)(difficultyBeatmap["_beatmapColorSchemeIdx"] ?? 0),
                        EnvironmentNameIdx = (int)(difficultyBeatmap["_environmentNameIdx"] ?? 0)
                    });
                }
            }
        }
        
        private static void DeserializeV4(JObject jObject, ref SongData data) {
            var difficultyBeatmaps = jObject["difficultyBeatmaps"]?.ToObject<List<DifficultyInfo>>() ?? [];
            var songData = new SongData {
                Version = jObject["version"]?.ToString() ?? "",
                Title = jObject["song"]?["title"]?.ToString() ?? "",
                SubTitle = jObject["song"]?["subTitle"]?.ToString() ?? "",
                Artist = jObject["song"]?["author"]?.ToString() ?? "",
                Song = new AudioData
                {
                    SongFilename = jObject["audio"]?["songFilename"]?.ToString() ?? "",
                    SongDuration = (float)(jObject["audio"]?["songDuration"] ?? 0),
                    AudioDataFilename = jObject["audio"]?["audioDataFilename"]?.ToString() ?? "",
                    BPM = (float)(jObject["audio"]?["bpm"] ?? 0),
                    LUFS = (float)(jObject["audio"]?["lufs"] ?? 0),
                    PreviewStartTime = (float)(jObject["audio"]?["previewStartTime"] ?? 0),
                    PreviewDuration = (float)(jObject["audio"]?["previewDuration"] ?? 0),
                    SongPreviewFilename = jObject["songPreviewFilename"]?.ToString() ?? ""
                },
                CoverImageFilename = jObject["coverImageFilename"]?.ToString() ?? "",
                EnvironmentNames = jObject["environmentNames"]?.ToObject<List<string>>() ?? new List<string>(),
                DifficultyBeatmaps = difficultyBeatmaps
            };

            songData.Mapper = songData.DifficultyBeatmaps?.FirstOrDefault()?.BeatmapAuthors?.Mappers?.FirstOrDefault() ?? "";
        }

        public override void WriteJson(JsonWriter writer, SongData? value, JsonSerializer serializer)
        {
            Debug.WriteLine("Serializing back to file is not supported");
            writer.WriteNull();
            return;
        }
    }
}