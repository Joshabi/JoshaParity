using System;
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
    new($"{mapFolder}/Howl", new GenericParityCheck())
};

foreach (MapAnalyser analyser in analysers)
{
    Console.WriteLine(analyser.ToString()+"\n");
}