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
using CmdParser;

namespace TsvImage
{
    partial class Program
    {
        class ArgsParseResult
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for label string")]
            public int labelCol = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Confidence threshold")]
            public float thresh = 0;
        }

        static void ParseResult(ArgsParseResult cmd)
        {
            int count = 0;
            int group = 0;
            var lines = File.ReadLines(cmd.inTsv)
                .AsParallel()
                .Select(line => line.Split('\t'))
                .Where (cols => cols.Length > 8)
                //col0 classLabel, col1
                .Select(cols => {
                    Console.Write("lines processed: {0}\r", ++count);
                    var f = ConvertFeature(cols[8], false);
                    var maxV = f.Max();
                    var id = Array.FindIndex(f, x => x == maxV);
                    return new
                    {
                        clsID = Convert.ToInt32(cols[1]),
                        sID = cols[0],
                        argmax = Tuple.Create(maxV, id)
                    };
                })
                .GroupBy(x => x.clsID)
                .AsParallel()
                .Select(g =>
                {
                    Console.Write("groups processed: {0}\r", ++group);
                    int total = g.AsEnumerable().Count();
                    int correct = g.AsEnumerable()
                        .Where(x => x.argmax.Item1 > cmd.thresh && x.clsID == x.argmax.Item2)
                        .Count();
                    var result = Tuple.Create(g.Key, g.AsEnumerable().First().sID, (float)correct / total, correct, total);
                    return result;
                })
                .OrderByDescending(x => x.Item3)
                .Select(x => string.Format("{0}\t{1}\t{2}\t{3}\t{4}", x.Item1, x.Item2, x.Item3, x.Item4, x.Item5));

            File.WriteAllLines(cmd.outTsv, lines);
        }

    }
}