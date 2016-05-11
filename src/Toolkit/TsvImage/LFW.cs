using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using CmdParser;
using CEERecognition;

namespace TsvImage
{
    partial class Program
    {
        class ArgsLfwEval
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file for face feature")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Pair list file")]
            public string pairList = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace inTsv file .ext with .pair.tsv)")]
            public string outTsv = null;
        }

        static void LfwEval(ArgsLfwEval cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".pair.tsv");

            Console.WriteLine("Loading face feature file...");
            var featureDict = File.ReadLines(cmd.inTsv)
                .Select(line => line.Split('\t'))
                .ToDictionary(cols => cols[0], cols => ConvertFeature(cols[1], true), StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("Faces loaded: {0}", featureDict.Count());

            int count = 0;
            var lines = File.ReadLines(cmd.pairList)
                .Select(line => line.Split('\t').ToList())
                .Select(cols =>
                {
                    //Input: pairInx:int, aFace:string, bFace:string, label:int
                    //Output: pairInx:int, aMUrlKey:string, bMUrlKey:string, label:int, results:float
                    float[] f1 = featureDict[cols[1] + ".jpg"];
                    float[] f2 = featureDict[cols[2] + ".jpg"];
                    float dist = Distance.L2Distance(f1, f2);
                    cols.Add(dist.ToString());
                    Console.Write("Pairs processed: {0}\r", ++count);
                    return cols;
                })
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");
        }
    }
}