using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace JoshaParity.Core.BeatmapData
{
    public static class MapLoader
    {
        public static SongData LoadMapFromFile(string mapFolder) {
            string infoDat = Directory.GetFiles(mapFolder, "info.dat", SearchOption.TopDirectoryOnly)?.First() ?? "";
            if (string.IsNullOrEmpty(infoDat)) return new();
            SongData? songData = JsonConvert.DeserializeObject<SongData>(File.ReadAllText(infoDat));
            if (songData is null) return new();
            songData.MapPath = mapFolder;
            LoadDifficulties(ref songData);
            return songData;
        }

        public static SongData LoadMapFromString(string jsonString) {
            return JsonConvert.DeserializeObject<SongData>(jsonString, new MapInfoSerializer()) ?? new();
        }

        public static DifficultyData LoadDifficulty(string jsonString) {
            return JsonConvert.DeserializeObject<DifficultyData>(jsonString, new BeatmapSerializer()) ?? new();
        }

        public static void LoadDifficulties(ref SongData songInfo)
        {
            foreach (DifficultyInfo diffInfo in songInfo.DifficultyBeatmaps)
            {
                string path = songInfo.MapPath + "/" + diffInfo.BeatmapDataFilename;
                DifficultyData difficultyData = LoadDifficulty(File.ReadAllText(path));
                diffInfo.DifficultyData = difficultyData;
            }
        }
    }
}
