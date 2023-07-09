# JoshaParity

**Rudamentary Parity Checker Library for Beatsaber Maps written in C#**

Uses rotation dictionaries and maths to attempt to predict how a map is played assuming the
rule of absolute parity. Logic for parity decisions can be hotswapped by creating a new checker class inhereting from
`IParityMethod`, with an implementation of `ParityCheck` that stores the logic. It is worth noting this swing data
persists to only where notes are and resets.

To analyze a map, you create a `MapAnalyser` with the map's folder path, and
it will load the necessary files. Then, it generates a list of `SwingData` that predict how the map is played.
**Example:**
```C#
MapAnalyser Diastrophism = new MapAnalyser("./Maps/Diastrophism");
```
**Alternatively,** pass in your own `IParityCheck` implementation to change the parity logic of this analyser:
```C#
MapAnalyser SetsunaImitation = new MapAnalyser("./Maps/SetsunaImitation", new ExperimentalBombTest());
```
You can get a `DiffAnalysis` results object containing the swings, BPM Object and some Metadata:
```C#
BeatmapDifficultyRank difficulty = BeatmapDifficultyRank.ExpertPlus;
List<SwingData> sData = Diastrophism.GetDiffAnalysis(difficulty);
```
Alternatively, you can get the list of `SwingData` via `GetSwingData()`. Both accept a characteristic argument:
```C#
List<SwingData> sData = Diastrophism.GetSwingData(difficulty);
List<SwingData> sData = Diastrophism.GetSwingData(difficulty, "lawless");
```
Other `MapAnalyser` functionalities include:
- `GetSps` - Returns the Average Swings Per Second of the data;
- `GetAverageEBPM` - Returns the Average Swing EBPM for the swing data;
- `GetHandedness` - Returns a Vector with the % of swings occuring on either hand, X - Righthand, Y - Lefthand
- `GetResetCount` - Returns the count of a specific ResetType;

This a library that should be used in conjunction with other Unity or .NET projects, more
features will be added over time.

**Thanks to:**
- Jindo, for allowing me to transfer v2 to v3 conversion function from the JindoRankTool.

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (As: Library)
- [AutoModder](https://github.com/LightAi39/ChroMapper-AutoModder) (As: Library)

**Known Issues:**
- [Minor] Using fixed definitions of bomb resets tends to result in some weirdness with excess bombs, so I've been experimenting with other ways to detect resets. Some setups will spook the checker at times especially concerning dots.

**Goals:**
- Overhaul core function so it composes all the swings first, then attempts to play them. Will help a lot with having more foresight for parity decisions. Furthermore, I will refactor it so you can feed information into an object continuously that calculates both hands at once, allowing for lean detection and live-updating of the swingData list.
- Overhaul swing composition so that slider detection is better
- Add a configuration system so parity values and other aspects can be tweaked externally
