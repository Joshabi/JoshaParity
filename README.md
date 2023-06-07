# JoshaParity

**Rudamentary Parity Checker for Beatsaber Maps written in C#**

Uses rotation dictionaries and math to attempt to figure out how a map is played assuming the
rule of absolute parity. Logic for parity can be hotswapped by creating a new checker class inhereting from
`IParityMethod`, with an implementation of `ParityCheck` that stores the logic.

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

Getting a list of `SwingData`:
```C#
BeatmapDifficultyRank difficulty = BeatmapDifficultyRank.ExpertPlus
List<SwingData> sData = Diastrophism.GetSwingData(difficulty);
```
Optionally, specify a characteristic:
```C#
List<SwingData> sData = Diastrophism.GetSwingData(difficulty, "lawless");
```

Other functionalities include `GetSPS` and `GetResetCount`:
```C#
int resetCount = Diastrophism.GetResetCount(difficulty, "standard", ResetType.Bomb);
float SPS = Diastrophism.GetSPS(difficulty, "standard");
```
This can currently be ran in Visual Studio or integrated into other projects as a library
via the DLL download.

**Thanks to:**
- Jindo, for allowing me to backport the v2 to v3 conversion function from the JindoRankTool previewer.

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (Non-Library version) (Original repo)

**Known Issues:**
- [Minor] Some configurations of bombs will spook the checker, usually unconventional setups, if you encounter any
  let me know the map and where it occurs so I can continue to improve the bomb parity detection.
- [Minor] The swing data's saber rotation may be in correct in some slider configurations where a dot preceeds an arrowed note.
