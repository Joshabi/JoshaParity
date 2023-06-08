# JoshaParity

**Rudamentary Parity Checker for Beatsaber Maps written in C#**

Uses rotation dictionaries and maths to attempt to figure out how a map is played assuming the
rule of absolute parity. Logic for parity can be hotswapped by creating a new checker class inhereting from
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
MapAnalyser SetsunaImitation = new MapAnalyser("./Maps/SetsunaImitation", new RetroParityCheck());
```
You can get the list of predicted `SwingData` like so, or with a specified characteristic:
```C#
BeatmapDifficultyRank difficulty = BeatmapDifficultyRank.ExpertPlus;
List<SwingData> sData = Diastrophism.GetSwingData(difficulty);
List<SwingData> sData = Diastrophism.GetSwingData(difficulty, "lawless");
```

Other functionalities include `GetSPS` and `GetResetCount`:
```C#
int resetCount = Diastrophism.GetResetCount(difficulty, "standard", ResetType.Bomb);
float SPS = Diastrophism.GetSPS(difficulty, "standard");
```
This a library that should be used in conjunction with other Unity or .NET projects, more
features will be added over time.

**Thanks to:**
- Jindo, for allowing me to backport the v2 to v3 conversion function from the JindoRankTool previewer.

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (Non-Library version) (Original repo)

**Known Issues:**
- [Minor] Some configurations of bombs will spook the checker due to the way the current method works, it is slightly less
  reliable for some "standard" types of bomb resets, but handles a lot of "bomb decor" and "bomb spirals" fine.
  This will be worked on and improved over time. Since bomb behaviour is written in `ParityCheck` you can change how it works in your
  own version, this may later be seperated into its own type `BombBehaviour` to modularise that logic more.
