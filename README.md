# JoshaParity

**Rudamentary Parity Checker Library for Beatsaber Maps written in C#**
Algorithmically attempts to predict how a map is played based on configured Parity Rules. Logic
for parity decisions can be hotswapped by creating a new checker class inhereting from
`IParityMethod`, with an implementation of `ParityCheck` that stores the logic. It is worth noting this swing data
persists to only where notes are and resets.

To analyze an entire mapset, you create a `MapAnalyser` with the map's folder path, and
it will load the necessary files. Then, it generates a list of `SwingData` that predict how the map is played.
**Example:**
```C#
MapAnalyser Diastrophism = new MapAnalyser("./Maps/Diastrophism");
```
You can pass in your own `IParityCheck` implementation to change the parity logic of this analyser:
```C#
MapAnalyser SetsunaImitation = new MapAnalyser("./Maps/SetsunaImitation", true, new ExperimentalBombTest());
```

**Alternatively,** you can run on an individual difficulty by loading the difficulty.dat and info.dat files into strings. For example:
```C#
string infoContents = File.ReadAllText("File Path of Info.dat Here"); 
string difContents = File.ReadAllText("File Path of ExpertPlusStandard.dat Here for example");
DiffAnalysis expertPlus = new DiffAnalysis(infoContents, difContents, BeatmapDifficultyRank.ExpertPlus);
```

This `DiffAnalysis` can be retrieved if you use a `MapAnalyser` via:
```C#
DiffAnalysis expertPlus = mapAnalyser.GetDiffAnalysis(BeatmapDifficultyRank.ExpertPlus);
```

`DiffAnalysis` functionalities include:
- `GetSPS` - Returns the Average Swings Per Second of the data;
- `GetAverageEBPM` - Returns the Average Swing EBPM for the swing data;
- `GetHandedness` - Returns a Vector with the % of swings occuring on either hand, X - Righthand, Y - Lefthand;
- `GetResetCount` - Returns the count of a specific ResetType;
- `GetSwingData` - Returns swing data for this difficulty;
- `GetSwingTypePercent` - Returns % of a given type of swing;
- `GetDoublesPercent` - Returns % of the map that are "doubles";
- `SwingContainer` - You can directly access a difficulties `SwingContainer` to retrieve LeanData, (player)`OffsetData` and more;

**Thanks to:**
- Jindo, for allowing me to transfer v2 to v3 conversion function from the JindoRankTool.

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (As: Library)
- [AutoModder](https://github.com/LightAi39/ChroMapper-AutoModder) (As: Library)

**Known Issues:**
- [Minor] Using fixed definitions of bomb resets isn't always correct, and more complex solutions also have issues. Currently, I am
  attempting to rework and reapproach this problem by making it try resetting and not resetting, then seeing which path is better.

**Goals:**
- Add a configuration system so parity values and other aspects can be tweaked externally
- Allow multiple paths to be tried for bomb resets to improve reset detection
- Allow multiple paths to be tried with some metric for success to allow more realistic playing for JoshaBot
