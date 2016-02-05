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
        class ArgsCeleb
        {
            [Argument(ArgumentType.Required, HelpText = "Sid in top impression list")]
            public string sidInTopImpression = null;
            [Argument(ArgumentType.Required, HelpText = "Sid in model")]
            public string sidInModel = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Entity info")]
            public string entityInfo = null;
        }

        static void Celeb(ArgsCeleb cmd)
        {
            var entityInfo = File.ReadLines(cmd.entityInfo)
                .Select((line, i) =>
                {
                    if ((i + 1) % 10000 == 0)
                        Console.Write("Entity info loaded: {0}\r", i + 1);
                    return line.Split('\t');
                })
                .ToDictionary(cols => cols[0], cols => cols[1], StringComparer.Ordinal);
            Console.WriteLine("\nSid in entity info: {0}", entityInfo.Count());

            var topImpression = File.ReadLines(cmd.sidInTopImpression)
                .Select(line => line.Split('\t'))
                .Select(cols => 
                {
                    string sid = cols[0].Substring("http://knowledge.microsoft.com/".Length);
                    string name;
                    if (!entityInfo.TryGetValue(sid, out name))
                        name = "";
                    return Tuple.Create(sid, name, Convert.ToInt32(cols[1]));
                })
                .ToList();
            Console.WriteLine("Sid in impression: {0}", topImpression.Count());

            var sidSetInModel = new HashSet<string>(File.ReadLines(cmd.sidInModel)
                .Select(line => line.Split('\t')[0]), StringComparer.Ordinal);
            Console.WriteLine("Sid in model: {0}", sidSetInModel.Count());

            var top1K = topImpression.Take(1000).ToList();
            top1K.RemoveAll(tp => sidSetInModel.Contains(tp.Item1));
            Console.WriteLine("Sid in impression top1K but not in model: {0}", top1K.Count());
            File.WriteAllLines(Path.ChangeExtension(cmd.sidInModel, "missed_in_top1k.tsv"),
                top1K.Select(tp => tp.Item1 + "\t" + tp.Item2 + "\t" + tp.Item3));

            var top10K = topImpression.Take(10000).ToList();
            top10K.RemoveAll(tp => sidSetInModel.Contains(tp.Item1));
            Console.WriteLine("Sid in impression top10K but not in model: {0}", top10K.Count());
            File.WriteAllLines(Path.ChangeExtension(cmd.sidInModel, "missed_in_top10k.tsv"),
                top10K.Select(tp => tp.Item1 + "\t" + tp.Item2 + "\t" + tp.Item3));

            var top100K = topImpression.Take(100000).ToList();
            top100K.RemoveAll(tp => sidSetInModel.Contains(tp.Item1));
            Console.WriteLine("Sid in impression top100K but not in model: {0}", top100K.Count());
            File.WriteAllLines(Path.ChangeExtension(cmd.sidInModel, "missed_in_top100K.tsv"),
                top100K.Select(tp => tp.Item1 + "\t" + tp.Item2 + "\t" + tp.Item3));

        }

         class ArgsCelebRemove
        {
            [Argument(ArgumentType.Required, HelpText = "Entity info with date of birth info")]
            public string entityInfo = null;
        }
        
        static void CelebRemove(ArgsCelebRemove cmd)
        {
            var black_list = File.ReadLines(cmd.entityInfo)
                .Where(line =>
                {
                    string dob = line.Split('\t')[3];
                    if (string.IsNullOrEmpty(dob))
                        return false;
                    if (dob.StartsWith("-"))    // B.C.
                        return true;

                    dob = dob.Substring(0, dob.Length - 1);
                    int year = Convert.ToInt32(dob.Split('-')[0]);
                    if (year < 1800)
                        return true;
                    return false;
                });

            File.WriteAllLines(Path.ChangeExtension(cmd.entityInfo, ".black_list.tsv"), black_list);
        }

        class ArgsClassVariance
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace InTsv .ext with .var.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for feature")]
            public int feature = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for label")]
            public int label = -1;
        }

        static void ClassVariance(ArgsClassVariance cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".var.tsv");

            int count = 0;
            int group_count = 0;
            var variances = File.ReadLines(cmd.inTsv)
                //.AsParallel().AsOrdered()
                .Select(x => x.Split('\t'))
                .Select(cols => 
                {
                    Console.Write("Line processed: {0}\r", ++count);
                    return new { c = cols[cmd.label], f = ConvertFeature(cols[cmd.feature], true) };
                })
                .GroupBy(x => x.c)
                .Select(g =>
                {
                    int N = g.Count();
                    var mean = new float[g.AsEnumerable().First().f.Length];
                    Array.Clear(mean, 0, mean.Length);
                    foreach (var x in g.AsEnumerable())
                    {
                        for (int i = 0; i < mean.Length; i++)
                            mean[i] += x.f[i];
                    }
                    mean = mean.Select(x => x / N).ToArray();

                    var var = g.AsEnumerable()
                        .Select(x => (float)Math.Sqrt(Distance.L2Distance(mean, x.f)))
                        .Aggregate(0f, (sum, x) => sum + x)
                        / N;

                    Console.Write("Groups processed: {0}\r", ++group_count);
                    return Tuple.Create(g.Key, var, N);
                })
                .ToList();

            File.WriteAllLines(cmd.outTsv, variances.OrderBy(tp => tp.Item2)
                    .Select(tp => tp.Item1 + "\t" + tp.Item2 + "\t" + tp.Item3));
            Console.WriteLine("\nDone.");
        }

    }
}