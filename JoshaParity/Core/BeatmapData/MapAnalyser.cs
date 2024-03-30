using System.Collections.Generic;
using System.Linq;

namespace JoshaParity
{
    /// <summary>
    /// Handles loading and analysing a map
    /// </summary>
    public class MapAnalyser {
        private readonly Dictionary<string, List<DiffAnalysis>> _mapDiffAnalyse = [];

        public SongData MapData { get; set; }
        public Dictionary<string, List<DiffAnalysis>> MapDiffAnalyse => _mapDiffAnalyse;

        public MapAnalyser(string mapPath, bool preRun = true, IParityMethod? parityMethod = null) {
            parityMethod ??= new GenericParityCheck();
            MapData = MapLoader.LoadMapFromFile(mapPath);
            if (preRun) AnalyseMap(parityMethod);
        }

        public void AnalyseMap(IParityMethod? parityMethod = null)
        {
            foreach (DifficultyInfo diffInfo in MapData.DifficultyBeatmaps)
            {
                DifficultyData data = diffInfo.DifficultyData;
                BPMHandler bpmHandler = BPMHandler.CreateBPMHandler(MapData.Song.BPM, data.BPMChanges, MapData.SongTimeOffset);
                MapObjects mapObjects = MapObjectsFromDiff(data, bpmHandler);
                MapSwingContainer swingContainer = SwingDataGeneration.Run(mapObjects, bpmHandler, parityMethod);

                string charID = diffInfo.Characteristic.ToLower();

                if (!_mapDiffAnalyse.ContainsKey(charID)) {
                    _mapDiffAnalyse.Add(charID, []);
                }

                _mapDiffAnalyse[charID].Add(new DiffAnalysis(diffInfo.Rank, swingContainer, bpmHandler, mapObjects));
            }
        }

        internal static MapObjects MapObjectsFromDiff(DifficultyData data, BPMHandler bpmHandler)
        {
            List<Note> notes = new(data.Notes);
            List<Bomb> bombs = new(data.Bombs);
            List<Obstacle> obstacles = new(data.Obstacles);
            List<Arc> arcs = new(data.Arcs);
            List<Chain> chains = new(data.Chains);

            notes = notes.Select(x => { x.ms = (bpmHandler.ToRealTime(x.b) * 1000); return x; }).ToList();
            bombs = bombs.Select(x => { x.ms = (bpmHandler.ToRealTime(x.b) * 1000); return x; }).ToList();
            obstacles = obstacles.Select(x => { x.ms = (bpmHandler.ToRealTime(x.b) * 1000); return x; }).ToList();
            arcs = arcs.Select(x => { x.ms = (bpmHandler.ToRealTime(x.b) * 1000); return x; }).ToList();
            chains = chains.Select(x => { x.ms = (bpmHandler.ToRealTime(x.b) * 1000); return x; }).ToList();

            return new MapObjects(notes, bombs, obstacles, arcs, chains);
        }

        /// <summary>
        /// Returns all difficulty analysis objects
        /// </summary>
        /// <returns></returns>
        public List<DiffAnalysis> GetAllDiffAnalysis()
        {
            List<DiffAnalysis> result = [];
            foreach (KeyValuePair<string, List<DiffAnalysis>> characteristicData in _mapDiffAnalyse) {
                foreach (DiffAnalysis diffAnalysis in characteristicData.Value) {
                    result.Add(diffAnalysis);
                }
            }
            return result;
        }

        /// <summary>
        /// Returns a difficulty analysis object based on Rank and Characteristic
        /// </summary>
        /// <param name="difficultyRank">Specific difficulty rank to retrieve data for</param>
        /// <param name="characteristic">Characteristic difficulty belongs to</param>
        /// <returns></returns>
        public DiffAnalysis? GetDiffAnalysis(BeatmapDifficultyRank difficultyRank, string characteristic = "standard")
        {
            characteristic = characteristic.ToLower();
            if (!_mapDiffAnalyse.ContainsKey(characteristic)) return null;

            List<DiffAnalysis> diffAnalysis = _mapDiffAnalyse[characteristic];
            foreach (DiffAnalysis analysis in diffAnalysis) {
                if (analysis.difficultyRank == difficultyRank) {
                    return analysis;
                }
            }
            return null;
        }
    }
}
