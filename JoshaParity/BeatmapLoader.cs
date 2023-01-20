using System.Text.Json;

namespace JoshaUtils
{
    /// <summary>
    /// Loads Beatsaber Maps Via JSON Classes
    /// </summary>
    internal class BeatmapLoader
    {

        /// <summary>
        /// Loads a JSON file
        /// </summary>
        /// <typeparam name="T">Object for JSON file to load into</typeparam>
        /// <param name="fileName">File name of JSON</param>
        /// <returns></returns>
        public static T? LoadJSON<T>(string fileName)
        {
            T? obj = JsonSerializer.Deserialize<T>(File.ReadAllText(fileName));
            return obj;
        }

        /// <summary>
        /// Returns a Beatmap? given a map directory
        /// </summary>
        /// <param name="filePath">Directory of all the map files</param>
        /// <returns></returns>
        public static Beatmap? LoadMap(string filePath)
        {
            Beatmap? map = LoadJSON<Beatmap>(filePath + "info.dat");
            if(map != null)
            {
                foreach(Characteristic chara in map._difficultyBeatmapSets)
                {
                    foreach(DifficultyInfo dif in chara._difficultyBeatmaps)
                    {
                        MapDifficulty? diffFile = LoadJSON<MapDifficulty>(filePath + dif._beatmapFilename);
                        if (diffFile != null)
                            map.Difficulties.Add(dif._beatmapFilename, diffFile);
                    }
                }
                return map;
            }
            return null;
        }
    }

    /// <summary>
    /// JSON Class containing Beatmap Information
    /// </summary>
    public class Beatmap
    {
        public string _version { get; set; } = " ";
        public string _songName { get; set; } = " ";
        public string _songSubName { get; set; } = " ";
        public string _songAuthorName { get; set; } = " ";
        public string _levelAuthorName { get; set; } = " ";
        public float _beatsPerMinute { get; set; } = 120;
        public string _coverImageFilename { get; set; } = " ";
        public Characteristic[] _difficultyBeatmapSets { get; set; }
        public Dictionary<string, MapDifficulty> Difficulties { get; set; } = new Dictionary<string, MapDifficulty>();
    }

    /// <summary>
    /// JSON Class containing Difficulty Notes, Obstacles and BPM Changes
    /// </summary>
    public class MapDifficulty
    {
        public string _version { get; set; }
        public Note[] _notes { get; set; }
        public Obstacle[] _obstacles { get; set; }
        public Customdata _customData { get; set; }
    }

    /// <summary>
    /// JSON Class containing BPM Changes
    /// </summary>
    public class Customdata
    {
        public Bpmchange[] _BPMChanges { get; set; }
        public float _time { get; set; }
    }

    /// <summary>
    /// JSON Class with Information about a BPM change
    /// </summary>
    public class Bpmchange
    {
        public float _time { get; set; }
        public float _BPM { get; set; }
        public int _beatsPerBar { get; set; }
        public int _metronomeOffset { get; set; }
    }

    /// <summary>
    /// JSON Class for individual notes
    /// </summary>
    public class Note
    {
        public float _time { get; set; }
        public int _lineIndex { get; set; }
        public int _lineLayer { get; set; }
        public int _type { get; set; }
        public int _cutDirection { get; set; }
    }

    /// <summary>
    /// JSON Class for individual obstacles
    /// </summary>
    public class Obstacle
    {
        public float _time { get; set; }
        public int _lineIndex { get; set; }
        public int _type { get; set; }
        public float _duration { get; set; }
        public int _width { get; set; }
    }

    /// <summary>
    /// JSON Class for Characteristic information
    /// </summary>
    public class Characteristic
    {
        public string _beatmapCharacteristicName { get; set; }
        public DifficultyInfo[] _difficultyBeatmaps { get; set; }
    }

    /// <summary>
    /// JSON Class with metadata about a difficulty
    /// </summary>
    public class DifficultyInfo
    {
        public string _difficulty { get; set; }
        public int _difficultyRank { get; set; }
        public string _beatmapFilename { get; set; }
        public float _noteJumpMovementSpeed { get; set; }
    }
}

