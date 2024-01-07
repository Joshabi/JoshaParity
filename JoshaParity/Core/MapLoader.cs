using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JoshaParity
{
    /// <summary>
    /// Loads Beatsaber Maps Via JSON Classes
    /// </summary>
    public static class MapLoader
    {
        /// <summary>
        /// Loads a JSON file and attempts to serialize it
        /// </summary>
        /// <typeparam name="T">Object for JSON file to serialize into</typeparam>
        /// <param name="fileName">File name of JSON</param>
        /// <returns></returns>
        public static T LoadJSONFromFile<T>(string fileName)
        {
            T obj = LoadJSON<T>(File.ReadAllText(fileName));
            return obj;
        }

        /// <summary>
        /// Attempts to serialize a string of text to type T
        /// </summary>
        /// <typeparam name="T">Object for JSON file to serialize into</typeparam>
        /// <param name="fileContents">Contents of a file loaded in</param>
        /// <returns></returns>
        public static T LoadJSON<T>(string fileContents)
        {
            T? obj;
            try
            { 
                obj = JsonConvert.DeserializeObject<T>(fileContents);
            }
            catch
            {
                Console.WriteLine($"Was unable to serialize JSON: {fileContents}.\nCheck map path is correctly configured or file is valid.");
                obj = default;
            }
            
            // If null, use reflection to make new type T, else return obj
            if (obj == null) { return (T)Activator.CreateInstance(typeof(T)); }
            else { return obj; }
        }

        /// <summary>
        /// Loads map info.dat from its folder location
        /// </summary>
        /// <param name="mapFolder">Map Directory (Where Info.dat is)</param>
        public static MapStructure LoadMapFromFile(string mapFolder) {
            string infoDatFile = mapFolder + "/info.dat";
            MapStructure loadedMap = LoadJSONFromFile<MapStructure>(infoDatFile);
            loadedMap._mapFolder = mapFolder;
            return loadedMap;
        }

        /// <summary>
        /// Loads map data from a string containing info.dat contents
        /// </summary>
        /// <param name="infoDatContents">String containing info.dat contents</param>
        public static MapStructure LoadMap(string infoDatContents)
        {
            // Load map data
            MapStructure loadedMap = LoadJSON<MapStructure>(infoDatContents);
            loadedMap._mapFolder = string.Empty;
            return loadedMap;
        }

        /// <summary>
        /// Loads a specific map difficulty given a map folder, ignores 360, 90 and lightshows.
        /// </summary>
        /// <param name="mapFolder">Map Directory (Where Info.dat is)</param>
        /// <param name="difficultyRank">Which difficulty rank to load</param>
        public static MapData LoadDifficulty(string mapFolder, BeatmapDifficultyRank difficultyRank)
        {
            MapData emptyMap = new MapData();

            // Load map data
            string infoDatFile = mapFolder + "/info.dat";
            MapStructure? loadedMap = LoadJSONFromFile<MapStructure>(infoDatFile);

            if (loadedMap == null) { return emptyMap; }

            // Ignore Characteristics of: 360degree, 90degree and lightshows, else load all other difficulties
            foreach (MapDifficultyStructure characteristic in loadedMap._difficultyBeatmapSets)
            {
                string diffName = characteristic._beatmapCharacteristicName.ToLower();
                if (!diffName.Equals("lightshow") && !diffName.Equals("360degree") && !diffName.Equals("90degree"))
                {
                    foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                    {
                        if (difficulty._difficultyRank == difficultyRank)
                        {
                            MapData map = LoadDifficultyDataFromFolder(mapFolder, difficulty);
                            return map;
                        }
                    }
                }
            }
            return emptyMap;
        }

        /// <summary>
        /// Loads a specific map difficulty given its info and map folder
        /// </summary>
        /// <param name="mapFolder">Map Directory (Where diffName.dat is)</param>
        /// <param name="difficulty">Difficulty metadata</param>
        public static MapData LoadDifficultyDataFromFolder(string mapFolder, DifficultyStructure difficulty)
        {
            string diffFilePath = mapFolder + "/" + difficulty._beatmapFilename;
            MapData map = LoadDifficultyData(File.ReadAllText(diffFilePath));
            return map;
        }

        /// <summary>
        /// Loads a specific difficulty.
        /// </summary>
        /// <param name="diffContents">Contents of the diff's .dat file</param>
        /// <returns></returns>
        public static MapData LoadDifficultyData(string diffContents)
        {
            // Extract difficulty format version then convert or load accordingly
            string version = ExtractVersion(diffContents);
            Version? ver = string.IsNullOrEmpty(version) ? new Version("0.0.0") : new Version(version);
            DifficultyV3? loadedDiff = null;

            // Depending on major revision, parse difficulty data
            if (ver <= new Version("2.6.0")) {
                DifficultyV2? V2Diff = LoadJSON<DifficultyV2>(diffContents);
                loadedDiff = (V2Diff != null) ? MapStructureUtils.ConvertV2ToV3(V2Diff) : null;
                if (V2Diff != null && loadedDiff != null)
                {
                    if (V2Diff._events != null && V2Diff._events.Length != 0)
                    {
                        Console.WriteLine("Attempting to find BPM Events");
                        List<EventsV2> BPMEvents = V2Diff._events.ToList();
                        BPMEvents.RemoveAll(x => x._type != 100);

                        List<BPMEvent> BPMEventsToAdd = new List<BPMEvent>();
                        foreach (EventsV2 mapEvent in BPMEvents) {
                            BPMEvent newEvent = new BPMEvent { b = mapEvent._time, m = mapEvent._floatValue };
                            BPMEventsToAdd.Add(newEvent);
                        }

                        loadedDiff.bpmEvents = BPMEventsToAdd.ToArray();
                    }
                }
            } else if (ver >= new Version("3.0.0")) {
                loadedDiff = LoadJSON<DifficultyV3>(diffContents);
            }

            // If still null, return empty
            if (loadedDiff == null) return new();
            MapData map = new MapData { DifficultyData = loadedDiff };
            return map;
        }

        /// <summary>
        /// Given a JSON String, attempts to parse the "version" field
        /// </summary>
        /// <param name="jsonString">String to parse</param>
        /// <returns></returns>
        internal static string ExtractVersion(string jsonString)
        {
            try {
                var jsonObj = JObject.Parse(jsonString);
                var v3ver = jsonObj["version"]?.ToString() ?? string.Empty;
                if (v3ver == string.Empty) {
                    return jsonObj["_version"]?.ToString() ?? string.Empty;
                } else { return v3ver; }
            } catch {
                return string.Empty;
            }
        }
    }

    #region METADATA

    /// <summary>
    /// Some utilities for map structure such as version conversion.
    /// </summary>
    public class MapStructureUtils
    {
        /// <summary>
        /// Converts V2 difficulty to V3
        /// </summary>
        public static DifficultyV3 ConvertV2ToV3(DifficultyV2 inV2File)
        {
            return new() {
                version = inV2File._version,
                bombNotes = ConvertBombs(inV2File._notes),
                colorNotes = ConvertNotes(inV2File._notes),
                sliders = ConvertSliders(inV2File._sliders),
                obstacles = ConvertObstacles(inV2File._obstacles)
            };
        }

        /// <summary>
        /// Converts V2 Notes to V3
        /// </summary>
        public static Note[] ConvertNotes(NoteV2[] notes)
        {
            return (notes == null) ? 
                Array.Empty<Note>() :
                notes.Where(note => note._type != 2 && note._type != 3)
                        .Select(note => new Note {
                            b = note._time,
                            c = note._type,
                            x = note._lineIndex,
                            y = note._lineLayer,
                            d = note._cutDirection
                        }).ToArray();
        }

        public static Bomb[] ConvertBombs(NoteV2[] notes)
        {
            return (notes == null) ?
                Array.Empty<Bomb>() :
                notes.Where(note => note._type == 3)
                    .Select(note => new Bomb
                    {
                        b = note._time,
                        x = note._lineIndex,
                        y = note._lineLayer
                    }).ToArray();
        }

        public static Slider[] ConvertSliders(SliderV2[] sliders)
        {
            return (sliders == null) ?
                Array.Empty<Slider>() :
                sliders.Select(slider => new Slider
                {
                    b = slider._headTime,
                    x = slider._headLineIndex,
                    y = slider._headLineLayer,
                    mu = slider._headControlPointLengthMultiplier,
                    d = slider._headCutDirection,
                    tb = slider._tailTime,
                    tx = slider._tailLineIndex,
                    ty = slider._tailLineLayer,
                    tmu = slider._tailControlPointLengthMultiplier,
                    m = slider._sliderMidAnchorMode
                }).ToArray();
        }

        public static Obstacle[] ConvertObstacles(ObstacleV2[] obstacles)
        {
            return (obstacles == null) ?
                Array.Empty<Obstacle>() :
                obstacles.Select(obstacle => new Obstacle
                {
                    b = obstacle._time,
                    x = obstacle._lineIndex,
                    w = obstacle._width,
                    h = obstacle._type == 0 ? 3 : 1
                }).ToArray();
        }
    }

    /// <summary>
    /// Map Structure.
    /// </summary>
    public class MapStructure
    {
        public string _version { get; set; } = "";
        public string _songName { get; set; } = "";
        public string _songSubName { get; set; } = "";
        public string _songAuthorName { get; set; } = "";
        public string _levelAuthorName { get; set; } = "";
        public float _beatsPerMinute { get; set; } = 120;
        public float _shuffle { get; set; }
        public float _shufflePeriod { get; set; }
        public float _previewStartTime { get; set; }
        public float _previewDuration { get; set; }
        public string _songFilename { get; set; } = "";
        public string _coverImageFilename { get; set; } = "";
        public string _environmentName { get; set; } = "";
        public string _allDirectionsEnvironmentName { get; set; } = "";
        public float _songTimeOffset { get; set; }
        public MapDifficultyStructure[] _difficultyBeatmapSets { get; set; } = Array.Empty<MapDifficultyStructure>();
        public string _mapFolder { get; set; } = "";
    }

    /// <summary>
    /// Map Difficulties Structure.
    /// </summary>
    public class MapDifficultyStructure
    {
        public string _beatmapCharacteristicName { get; set; } = "";
        public DifficultyStructure[] _difficultyBeatmaps { get; set; } = Array.Empty<DifficultyStructure>();
    }

    /// <summary>
    /// Map File Structure.
    /// </summary>
    public class DifficultyStructure
    {
        public string _difficulty { get; set; } = "";
        public BeatmapDifficultyRank _difficultyRank { get; set; } = BeatmapDifficultyRank.ExpertPlus;
        public string _beatmapFilename { get; set; } = "";
        public float _noteJumpMovementSpeed { get; set; }
        public float _noteJumpStartBeatOffset { get; set; }
        public string hash { get; set; } = "";
        public string mapName { get; set; } = "";
        public string songFilename { get; set; } = "";
    }

    #endregion

    #region V3 FORMATTING

    /// <summary>
    /// Map Data V3.
    /// </summary>
    public class MapData
    {
        public DifficultyStructure Metadata { get; set; } = new DifficultyStructure();
        public DifficultyV3 DifficultyData { get; set; } = new DifficultyV3();
    }

    /// <summary>
    /// Difficulty format V3.
    /// </summary>
    public class DifficultyV3
    {
        public string version { get; set; } = "";
        public BPMEvent[] bpmEvents { get; set; } = Array.Empty<BPMEvent>();
        public Note[] colorNotes { get; set; } = Array.Empty<Note>();
        public Bomb[] bombNotes { get; set; } = Array.Empty<Bomb>();
        public Obstacle[] obstacles { get; set; } = Array.Empty<Obstacle>();
        public Slider[] sliders { get; set; } = Array.Empty<Slider>();
        public BurstSlider[] burstSliders { get; set; } = Array.Empty<BurstSlider>();
    }

    /// <summary>
    /// Note V3.
    /// </summary>
    public class Note
    {
        public float b { get; set; }  // beat
        public int x { get; set; } // 0-3
        public int y { get; set; } // 0-2
        public int c { get; set; } // 0-1
        public int d { get; set; } // 0-8 direction
        public int a { get; set; } // counter-clockwise angle in degrees
        public float ms { get; set; } = 0;
    }

    /// <summary>
    /// Bomb V3.
    /// </summary>
    public class Bomb
    {
        public float b { get; set; } // beat
        public int x { get; set; } // 0-3
        public int y { get; set; } // 0-2
    }

    /// <summary>
    /// Obstacle V3.
    /// </summary>
    public class Obstacle
    {
        public float b { get; set; } // beat
        public int x { get; set; } // 0-3
        public int y { get; set; } // 0-2
        public float d { get; set; } // duration in beats
        public int w { get; set; } // width
        public int h { get; set; } // height
    }

    /// <summary>
    /// Arcs V3.
    /// </summary>
    public class Slider
    {
        public float b { get; set; } // beat
        public int c { get; set; } // 0-1
        public int x { get; set; } // 0-3
        public int y { get; set; } // 0-2
        public int d { get; set; } // 0-8 (head direction)
        public float mu { get; set; } // head multiplier (how far the arc goes from the head)
        public float tb { get; set; } // tail beat
        public int tx { get; set; } // 0-3
        public int ty { get; set; } // 0-2
        public int tc { get; set; } // 0-1
        public float tmu { get; set; } // tail multiplier (how far the arc goes from the tail)
        public int m { get; set; } // mid-anchor mode
    }

    /// <summary>
    /// Chains V3.
    /// </summary>
    public class BurstSlider : Note
    {
        public float tb { get; set; } // tail beat
        public int tx { get; set; } // 0-3
        public int ty { get; set; } // 0-2
        public int sc { get; set; } // segment count
        public float s { get; set; } // squish factor (should not be 0 or it crashes beat saber, apparently they never fixed this???)
    }

    public class BPMEvent
    {
        public float b { get; set; } // Time in beats where it is
        public float m { get; set; } // Represents new BPM
    }

    #endregion

    #region V2 FORMATTING

    /// <summary>
    /// Map Data V2.
    /// </summary>
    public class BeatmapDataV2
    {
        public MapStructureUtils Metadata { get; set; } = new MapStructureUtils();
        public DifficultyV2 BeatDataV2 { get; set; } = new DifficultyV2();
    }

    /// <summary>
    /// Difficulty format V2.
    /// </summary>
    public class DifficultyV2
    {
        public string _version { get; set; } = "";
        public NoteV2[] _notes { get; set; } = Array.Empty<NoteV2>();
        public SliderV2[] _sliders { get; set; } = Array.Empty<SliderV2>();
        public ObstacleV2[] _obstacles { get; set; } = Array.Empty<ObstacleV2>();
        public EventsV2[] _events { get; set; } = Array.Empty<EventsV2>();
    }

    /// <summary>
    /// Note and Bombs V2.
    /// </summary>
    public class NoteV2
    {
        public float _time { get; set; }  // beat (b)
        public int _lineIndex { get; set; } // 0-3 (x)
        public int _lineLayer { get; set; } // 0-2 (y)
        public int _type { get; set; } // 0=left,1=right,2=unused,3=bomb
        public int _cutDirection { get; set; } // 0-8 (d)
    }

    /// <summary>
    /// Obstacles V2.
    /// </summary>
    public class ObstacleV2
    {
        public float _time { get; set; } // beat (b)
        public int _lineIndex { get; set; } // 0-3 (x)
        public int _type { get; set; } // 0 = full height, 1 = crouch/duck wall
        public float _duration { get; set; } // duration in beats (d)
        public int _width { get; set; } // width (w)
    }

    public class EventsV2
    {
        public float _time { get; set; } // beat (b)
        public int _type { get; set; } // type
        public float _value { get; set; } // value of event
        public float _floatValue { get; set;  }
    }

    /// <summary>
    /// Arcs V2.
    /// </summary>
    public class SliderV2
    {
        public int colorType { get; set; } // c
        public float _headTime { get; set; } // b
        public int _headLineIndex { get; set; } // x
        public int _headLineLayer { get; set; } // y
        public float _headControlPointLengthMultiplier { get; set; } // mu
        public int _headCutDirection { get; set; } // d
        public float _tailTime { get; set; } // tb
        public int _tailLineIndex { get; set; } // tx
        public int _tailLineLayer { get; set; } // ty
        public float _tailControlPointLengthMultiplier { get; set; } // tmu
        public int _tailCutDirection { get; set; } // not a thing in v3
        public int _sliderMidAnchorMode { get; set; } // m
    }

    #endregion

    #region Enums

    /// <summary>
    /// Cut Direction Enum.
    /// </summary>
    public enum BeatCutDirection
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

    #endregion
}