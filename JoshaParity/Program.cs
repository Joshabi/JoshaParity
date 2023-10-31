﻿using System;
using System.Collections.Generic;
using JoshaParity;
Console.WriteLine("<< Joshaparity Check! >>");

const string mapFolder = "./Maps";
List<MapAnalyser> analysers = new()
{
   // new($"{mapFolder}/Compute"),
   // new($"{mapFolder}/Diastrophism"),
   // new($"{mapFolder}/Blood Moon"),
   // new($"{mapFolder}/BS Recall"),
    new($"{mapFolder}/Internet", new GenericParityCheck())
};

foreach (MapAnalyser analyser in analysers)
{
    Console.WriteLine(analyser.ToString()+"\n");

    var diff = analyser.GetDiffAnalysis(BeatmapDifficultyRank.ExpertPlus);
    foreach(SwingData data in diff.swingData) { Console.WriteLine(data.ToString()); }
}