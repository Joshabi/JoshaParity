using System.Text.Json;

namespace JoshaParity
{
    /// <summary>
    /// Loads Beatsaber Maps Via JSON Classes
    /// </summary>
    public static class MapLoader
    {

        /// <summary>
        /// Loads a JSON file.
        /// </summary>
        /// <typeparam name="T">Object for JSON file to load into</typeparam>
        /// <param name="fileName">File name of JSON</param>
        /// <returns></returns>
        public static T? LoadJSON<T>(string fileName)
        {
            T? obj;
            try
            {
                obj = JsonSerializer.Deserialize<T>(File.ReadAllText(fileName));
            } 
            catch 
            {
                Console.WriteLine($"Was unable to serialize JSON for path: {fileName}.\nCheck map path is correctly configured.");
                obj = default;
            }
            return obj;
        }

        /// <summary>
        /// Loads all map difficulties given a map folder, ignores 360, 90 and lightshows.
        /// </summary>
        /// <param name="mapFolder">Map Directory (Where Info.dat is)</param>
        public static MapStructure LoadMap(string mapFolder)
        {
            // Load map data
            string infoDatFile = mapFolder + "/info.dat";
            MapStructure? loadedMap = LoadJSON<MapStructure>(infoDatFile);
            if (loadedMap != null)
            {
                loadedMap._mapFolder = mapFolder;
                return loadedMap;
            } else { return new MapStructure(); }
        }

        /// <summary>
        /// Loads a specific map difficulty given a map folder, ignores 360, 90 and lightshows.
        /// </summary>
        /// <param name="mapFolder">Map Directory (Where Info.dat is)</param>
        /// <param name="specificDifficulty">Enum ID of which difficulty to load</param>
        public static MapData LoadDifficulty(string mapFolder, BeatmapDifficultyRank specificDifficulty)
        {
            MapData emptyMap = new();

            // Load map data
            string infoDatFile = mapFolder + "/info.dat";
            MapStructure? loadedMap = LoadJSON<MapStructure>(infoDatFile);

            if (loadedMap == null) { return emptyMap; }

            // Ignore Characteristics of: 360degree, 90degree and lightshows, else load all other difficulties
            foreach (MapDifficultyStructure characteristic in loadedMap._difficultyBeatmapSets)
            {
                string diffName = characteristic._beatmapCharacteristicName.ToLower();
                if (!diffName.Equals("lightshow") && !diffName.Equals("360degree") && !diffName.Equals("90degree"))
                {
                    foreach (DifficultyStructure difficulty in characteristic._difficultyBeatmaps)
                    {
                        if (difficulty._difficultyRank == specificDifficulty)
                        {
                            MapData map = LoadDifficultyData(mapFolder, difficulty, loadedMap);
                            return map;
                        }
                    }
                }
            }
            return emptyMap;
        }

        /// <summary>
        /// Loads a specific difficulty.
        /// </summary>
        /// <param name="mapFolder">Map Directory (Where Info.dat is)</param>
        /// <param name="difficulty">Difficulty information class</param>
        /// <param name="mapData">Map information class</param>
        /// <returns></returns>
        public static MapData LoadDifficultyData(string mapFolder, DifficultyStructure difficulty, MapStructure mapData)
        {
            MapData emptyMap = new();
            // Load map data
            string diffFilePath = mapFolder + "/" + difficulty._beatmapFilename;
            DifficultyV3? loadedDiff = LoadJSON<DifficultyV3>(diffFilePath);

            // If null, just return an empty map file
            if (loadedDiff == null) return emptyMap;

            // Attempt to get difficulty data via conversion from V2 to V3
            if (string.IsNullOrEmpty(loadedDiff.version))
            {
                DifficultyV2? V2Diff = LoadJSON<DifficultyV2>(diffFilePath);
                if (V2Diff != null) loadedDiff = MapStructureUtils.ConvertV2ToV3(V2Diff);
            }

            // If still null
            if (string.IsNullOrEmpty(loadedDiff.version))
            {
                loadedDiff = new();
                Console.WriteLine("Was unable to load \"" + diffFilePath + "\" as either V3 or V2 format. Both failed to parse to JSON.");
            }

            // Construct map data and send back
            MapData map = new();
            map.Metadata = difficulty;
            map.Metadata.mapName = SanitizeFilename(mapData._songName);
            map.Metadata.songFilename = mapData._songFilename;
            map.DifficultyData = loadedDiff;
            return map;
        }

        /// <summary>
        /// Sanitizes certain character names from file name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string SanitizeFilename(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "");
        }

    }

    #region METADATA

    /// <summary>
    /// Some utilities for map structure such as version conversion.
    /// </summary>
    public class MapStructureUtils
    {
        public static DifficultyV3 ConvertV2ToV3(DifficultyV2 inV2File)
        {
            DifficultyV3 returnV3File = new();
            returnV3File.version = inV2File._version;
            List<Note> newNotes = new();
            List<Bomb> newBombs = new();
            if (inV2File._notes != null)
            {
                foreach (NoteV2 note in inV2File._notes)
                {
                    if (note._type == 3)
                    {
                        Bomb bomb = new();
                        bomb.b = note._time;
                        bomb.x = note._lineIndex;
                        bomb.y = note._lineLayer;
                        newBombs.Add(bomb);
                    }
                    else if (note._type != 2)
                    {
                        Note newNote = new();
                        newNote.b = note._time;
                        newNote.c = note._type;
                        newNote.x = note._lineIndex;
                        newNote.y = note._lineLayer;
                        newNote.d = note._cutDirection;
                        newNotes.Add(newNote);
                    }
                }
            }
            returnV3File.bombNotes = newBombs.ToArray();
            returnV3File.colorNotes = newNotes.ToArray();

            List<Slider> newSliders = new();
            if (inV2File._sliders != null)
            {
                foreach (SliderV2 slider in inV2File._sliders)
                {
                    Slider newSlider = new();
                    newSlider.b = slider._headTime;
                    newSlider.x = slider._headLineIndex;
                    newSlider.y = slider._headLineLayer;
                    newSlider.mu = slider._headControlPointLengthMultiplier;
                    newSlider.d = slider._headCutDirection;
                    newSlider.tb = slider._tailTime;
                    newSlider.tx = slider._tailLineIndex;
                    newSlider.ty = slider._tailLineLayer;
                    newSlider.tmu = slider._tailControlPointLengthMultiplier;
                    newSlider.m = slider._sliderMidAnchorMode;
                    newSliders.Add(newSlider);
                }
            }
            returnV3File.sliders = newSliders.ToArray();

            List<Obstacle> newObstacles = new();
            if (inV2File._obstacles != null)
            {
                foreach (ObstacleV2 obstacle in inV2File._obstacles)
                {
                    Obstacle newOb = new();
                    newOb.b = obstacle._time;
                    newOb.x = obstacle._lineIndex;
                    newOb.y = 0;
                    newOb.w = obstacle._width;
                    newOb.h = (obstacle._type == 0) ? 3 : 1;
                    newObstacles.Add(newOb);
                }
            }
            returnV3File.obstacles = newObstacles.ToArray();
            return returnV3File;
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
        public int _shuffle { get; set; }
        public float _shufflePeriod { get; set; }
        public float _previewStartTime { get; set; }
        public float _previewDuration { get; set; }
        public string _songFilename { get; set; } = "";
        public string _coverImageFilename { get; set; } = "";  
        public string _environmentName { get; set; } = "";
        public string _allDirectionsEnvironmentName { get; set; } = "";
        public int _songTimeOffset { get; set; }
        public object _customData { get; set; } = new();
        public MapDifficultyStructure[] _difficultyBeatmapSets { get; set; } = new MapDifficultyStructure[0];
        public string _mapFolder { get; set; } = "";
    }

    /// <summary>
    /// Map Difficulties Structure.
    /// </summary>
    public class MapDifficultyStructure
    {
        public string _beatmapCharacteristicName { get; set; } = "";
        public DifficultyStructure[] _difficultyBeatmaps { get; set; } = new DifficultyStructure[0];
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
        public object _customData { get; set; } = new();
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
        public DifficultyStructure Metadata { get; set; } = new();
        public DifficultyV3 DifficultyData { get; set; } = new();
    }

    /// <summary>
    /// Difficulty format V3.
    /// </summary>
    public class DifficultyV3
    {
        public string version { get; set; } = "";
        public Note[] colorNotes { get; set; } = new Note[0]; 
        public Bomb[] bombNotes { get; set; } = new Bomb[0];
        public Obstacle[] obstacles { get; set; } = new Obstacle[0];
        public Slider[] sliders { get; set; } = new Slider[0];
        public BurstSlider[] burstSliders { get; set; } = new BurstSlider[0];
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
    public class BurstSlider
    {
        public float b { get; set; } // beat
        public int x { get; set; } // 0-3
        public int y { get; set; } // 0-2
        public int c { get; set; } // 0-1
        public int d { get; set; } // head direction
        public float tb { get; set; } // tail beat
        public int tx { get; set; } // 0-3
        public int ty { get; set; } // 0-2
        public int sc { get; set; } // segment count
        public float s { get; set; } // squish factor (should not be 0 or it crashes beat saber, apparently they never fixed this???)
    }

    #endregion

    #region V2 FORMATTING

    /// <summary>
    /// Map Data V2.
    /// </summary>
    public class BeatmapDataV2
    {
        public MapStructureUtils Metadata { get; set; } = new();
        public DifficultyV3 BeatDataV2 { get; set; } = new();
    }

    /// <summary>
    /// Difficulty format V2.
    /// </summary>
    public class DifficultyV2
    {
        public string _version { get; set; } = "";
        public NoteV2[] _notes { get; set; } = new NoteV2[0];
        public SliderV2[] _sliders { get; set; } = new SliderV2[0];
        public ObstacleV2[] _obstacles { get; set; } = new ObstacleV2[0];
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
    /// Handedness.
    /// </summary>
    public enum BeatChirality
    {
        LeftHand = 0,
        RightHand = 1,
    }

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
        ExpertPlus = 9,
        All = 99,
    }

    #endregion
}