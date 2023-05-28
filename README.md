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

Getting a list of `SwingData` or reset count specifically:
```C#
BeatmapDifficultyRank difficulty = BeatmapDifficultyRank.ExpertPlus
List<SwingData> sData = Diastropism.GetSwingData(difficulty);
int resetCount = Diastrophism.GetResetCount(difficulty);
```

In order to run this currently you will need Visual Studio, and the V2 to V3 format map you 
wish to run through the checker. 

**Thanks to:**
- Jindo, for allowing me to backport the v2 to v3 conversion function from JindoRankTool

**JoshaParity used in:**
- [JindoRankTool](https://github.com/oshannonlepper/JindoRankTool) (Outdated Iteration)

**Known Issues:**
- Some configurations of bombs will spook the checker, usually unconventional setups, if you encounter any
  let me know the map and where it occurs so I can continue to improve the bomb parity detection.
- V3 Note Types not supported (Chains, Arcs)
