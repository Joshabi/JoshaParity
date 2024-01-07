# JoshaParity

**Rudamentary Parity Checker Library for Beatsaber Maps written in C#**
Algorithmically attempts to predict how a map is played based on configured Parity Rules. Logic
for parity decisions can be hotswapped by creating a new checker class inhereting from
`IParityMethod`, with an implementation of `ParityCheck` that stores the logic. It is worth noting this swing data
persists to only where notes / swings are, and does not include inbetween bomb avoidance or similar.

## Usage

To analyze an entire mapset, create a `MapAnalyser` using the folder path and it will handle loading the map and difficulties.
You can then retrieve the `MapSwingContainer` for any given difficulty.
**Example:**
```C#
MapAnalyser Diastrophism = new MapAnalyser("./Maps/Diastrophism");
DiffAnalysis ExpertPlus = Diastrophism.GetDiffAnalysis(BeatmapDifficultyRank.ExpertPlus);
List<SwingData> Result = ExpertPlus.GetJointSwingData();
```

**Alternatively:** 
You can run on an individual difficulty by loading the difficulty.dat and either info.dat contents or bpm and songtime offset values, For example:

**Info.dat Contents:**
```C#
string infoContents = File.ReadAllText("File Path of Info.dat Here"); 
string difContents = File.ReadAllText("File Path of ExpertPlusStandard.dat Here for example");
DiffAnalysis ExpertPlus = new DiffAnalysis(difContents, infoContents, BeatmapDifficultyRank.ExpertPlus);
```
**BPM and Offset Info:**
```C#
float bpm, songTimeOffset
string difContents = File.ReadAllText("File Path of ExpertPlusStandard.dat Here for example");
DiffAnalysis ExpertPlus = new DiffAnalysis(difContents, bpm, BeatmapDifficultyRank.ExpertPlus, songTimeOffset);
```
## Statistics

`DiffAnalysis` functionalities include:
- `GetSPS` - Returns the Average Swings Per Second of the data;
- `GetAverageEBPM` - Returns the Average Swing EBPM for the swing data;
- `GetHandedness` - Returns a Vector with the % of swings occuring on either hand, X - Righthand, Y - Lefthand;
- `GetResetCount` - Returns the count of a specific ResetType;
- `GetSwingData` - Returns swing data for this difficulty;
- `GetSwingTypePercent` - Returns % of a given type of swing;
- `GetDoublesPercent` - Returns % of the map that are "doubles";
- `SwingContainer` - You can directly access a difficulties `SwingContainer` to retrieve LeanData, (player)`OffsetData` and more;

With several of the above functionalities, you can specify a `HandResult` value to get data only considering one or both hands.
I intend on implementing way more statistics that currently are available in the future.

## Contributions

**Thanks to:**
- Jindo, for the initial V2->V3 Conversion and all the help whilst it was used in JindoRankTool
- Pink, for the motivation boost and help with both this and the bot
- Light and Loloppe, for all the help and for using this in AutoModder

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (As: Library)
- [AutoModder](https://github.com/LightAi39/ChroMapper-AutoModder) (As: Library)

## Issues and Goals

**Known Issues:**
- [Minor] Bomb resets and decor can trigger or not trigger bomb resets when the opposite was expected. This is relatively complex
  to fix as the same configuration of bombs can mean different things based on a lot of context. I will improve this over-time,
  hopefully through the use of simulating both outcomes.

**Important:**
- [Major] I will be depreciating use of passing in IParityCheck in favour of a more modular and customizeable approach.

**Goals:**
- Add a configuration system so parity values and other aspects can be tweaked externally
- Allow multiple paths to be tried for bomb resets to improve reset detection
- Allow multiple paths to be tried with some metric for success to allow more realistic playing for JoshaBot
- Add Lean to and overhaul Parity Calculation using a shifting dictionary
- Parity path of least resistance and least strain calculations
- Able to interface directly with both base-game object types, MapLoader types and BL-Parser types
