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
using TsvTool.Utility;
using CmdParser;
using CEERecognition;

namespace TsvImage
{
    partial class Program
    {
        class ArgsDedup
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for group label")]
            public int label = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for feature")]
            public int feature = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Dup distance threshold (default: 0.5)")]
            public float thresh = 0.5f;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace inTsv file .ext with .dedup.tsv)")]
            public string outTsv = null;
        }

        static int[] PairwiseDedup(float[][] featureList, float dupThreshold)
        {
            int[] dupFlag = new int[featureList.Length];
            for (int i = 0; i < dupFlag.Length; i++)
                dupFlag[i] = -1;

            for (int i = 0; i < featureList.Length; i++)
            {
                if (dupFlag[i] >= 0)
                    continue;

                for (int j = i + 1; j < featureList.Length; j++)
                {
                    if (dupFlag[j] >= 0)
                        continue;

                    float diff = Distance.L2Distance(featureList[i], featureList[j]);
                    if (diff <= dupThreshold)
                        dupFlag[j] = i;
                }
            }

            return dupFlag;
        }

        static void Dedup(ArgsDedup cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".dedup.tsv");

            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .Select(line => line.Split('\t'))
                .Select(cols => new { cols, f = ConvertFeature(cols[cmd.feature], false) })
                .GroupBy(x => x.cols[cmd.label])
                .ReportProgress("Groups processed")
                .SelectMany(g => 
                {
                    var featureList = g.AsEnumerable()
                        .Select(x => x.f)
                        .ToArray();
                    var dupFlagList = PairwiseDedup(featureList, cmd.thresh);
                    var cols_enumerable = Enumerable.Zip(g.AsEnumerable(), dupFlagList, (x, dupFlag) =>
                        {
                            x.cols[cmd.feature] = dupFlag.ToString();
                            return x.cols;
                        })
                        .Where(cols => Convert.ToInt32(cols[cmd.feature]) < 0);
                    return cols_enumerable;
                })
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("Done.");
        }
    }
}