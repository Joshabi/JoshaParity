using System;
using System.Collections.Generic;
using JoshaParity;
using System.IO;
using System.Linq;

Console.WriteLine("<< Joshaparity Check! >>");

const string mapFolder = "./Maps";
List<MapAnalyser> analysers = new()
{
   // new($"{mapFolder}/Compute"),
   // new($"{mapFolder}/Diastrophism"),
   // new($"{mapFolder}/Blood Moon"),
   // new($"{mapFolder}/BS Recall"),
    new($"{mapFolder}/Internet", true, new GenericParityCheck())
};

foreach (MapAnalyser analyser in analysers)
{
    Console.WriteLine(analyser.ToString()+"\n");

    var diff = analyser.GetDiffAnalysis(BeatmapDifficultyRank.ExpertPlus);
    foreach(SwingData data in diff.swingData) { Console.WriteLine(data.ToString()); }
}

//string mapInfoContents = File.ReadAllText($"{mapFolder}/Internet/Info.dat");
//string diffContents = File.ReadAllText($"{mapFolder}/Internet/ExpertPlusStandard.dat");
//DiffAnalysis expertPlus = new DiffAnalysis(mapInfoContents, diffContents, BeatmapDifficultyRank.ExpertPlus);

//Console.WriteLine(expertPlus.GetSwingData().Count());
//Console.WriteLine(expertPlus.GetResetCount(ResetType.Rebound));
//Console.WriteLine(expertPlus.GetSPS());