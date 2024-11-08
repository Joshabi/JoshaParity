# JoshaParity: Swing Predictor and Map Analysis Library

Notice:
**There is currently a refactored version in progress to expand functionality and ease of use. Lots of tech debt from the original version that will be cleaned up in the process. 08/11/24**

JoshaParity is a C# library designed to analyse Beat Saber maps and predict player swings based on configured Parity Rules. Currently you can hotswap parity logic via classes inheriting from the `IParityMethod` interface. This library focuses solely on swing data, mostly disregarding other elements like bomb avoidance. There are a few provided statistic functions currently integrated and I plan to implement more soon.

## Usage:

### Map Analysis:
To analyse an entire mapset, create a `MapAnalyser` using the folder path and it will handle loading the map and difficulties.
You can then retrieve the `MapSwingContainer` for any given difficulty or directly get the swing list via `GetJointSwingData()`
  
**Example:**
```C#
MapAnalyser analyser = new MapAnalyser("./Maps/Diastrophism");
DiffAnalysis expertPlus = analyser.GetDiffAnalysis(BeatmapDifficultyRank.ExpertPlus);
List<SwingData> result = expertPlus.GetJointSwingData();
```

**Alternatively, analyse individual difficulties by loading difficulty data:**
```C#
// With info.dat
string infoContents = File.ReadAllText("info.dat Path");
string difContents = File.ReadAllText("ExpertPlusStandard.dat Path");
DiffAnalysis expertPlus = new DiffAnalysis(infoContents, difContents, BeatmapDifficultyRank.ExpertPlus);

// Without info.dat (Requires BPM and SongTimeOffset)
float bpm, songTimeOffset;
string difContents = File.ReadAllText("ExpertPlusStandard.dat Path");
DiffAnalysis expertPlus = new DiffAnalysis(infoContents, difContents, BeatmapDifficultyRank.ExpertPlus, bpm, songTimeOffset);
```

### Statistics:

`DiffAnalysis` functionalities include:
- **GetSPS:** Returns the Average Swings Per Second of the data
- **GetAverageEBPM:** Returns the Average Swing EBPM for the swing data
- **GetHandedness:** Returns a Vector with the % of swings occuring on either hand, X - Righthand, Y - Lefthand
- **GetResetCount:** Returns the count of a specific ResetType
- **GetSwingData:** Returns swing data for this difficulty
- **GetSwingTypePercent:** Returns the percentage of a given type of swing
- **GetDoublesPercent:** Returns the percentage of the map that are "doubles"
- **GetAverageSpacing:** Returns the average grid distance between swings
- **GetAverageAngleChange:** Returns the average angle change between last swing end and current swing
- **SwingContainer:** Directly access a difficulty's `SwingContainer` to retrieve LeanData, (player) `OffsetData`, and more

With several of the above functionalities, you can specify a `HandResult` value to get data only considering one or both hands.
In the future, I plan to potentially restructuring allowing for rolling averages and more

## Contributions:

**Thanks to:**
- `Jindo`: For using it in JindoRankTool and assistance with getting V3 working initially
- `Pink`: For the motivation boost and help with both this and the bot
- `Light and Loloppe`: For all the help and for using this in AutoModder

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (As: Library)
- [AutoModder](https://github.com/LightAi39/ChroMapper-AutoModder) (As: Library)

## Known Issues:

- Bomb resets and decor can trigger or not trigger bomb resets when the opposite was expected. This is relatively complex
  to fix as the same configuration of bombs can mean different things based on a lot of context. I will improve this over-time,
  hopefully through the use of simulating both outcomes.
  
## Goals & Future Plans:

- Implement a config system for external tweaking and depreciate IParityCheck
- Restructure internals to allow testing multiple paths (both reset and no reset for bombs)
- Overhaul parity calculation using lean and shifting dictionaries (allows proper support of 180 inwards lean and further)
