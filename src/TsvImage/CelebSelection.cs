﻿using System;
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
using IniParser;
using CmdParser;
using CEERecognition;
using TsvTool.Utility;

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
            [Argument(ArgumentType.AtMostOnce, HelpText = "Outlier threshold (default: 0.9)")]
            public float thresh = 0.9f;
        }

        static float[] CalcMean<T>(IEnumerable<T> source, Func<T, float[]> func)
        {
            var mean = new float[func(source.First()).Length];
            Array.Clear(mean, 0, mean.Length);
            int total = source.Count();
            if (total > 0)
            {
                foreach (var x in source)
                {
                    for (int i = 0; i < mean.Length; i++)
                        mean[i] += func(x)[i];
                }
                mean = mean.Select(x => x / total).ToArray();
            }
            return mean;
        }

        static float CalcVariance<T>(IEnumerable<T> source, float[] mean, Func<T, float[]> func)
        {
            int total = source.Count();
            if (total == 0)
                return 0;
            var var = source.AsEnumerable()
                .Select(x => (float)Math.Sqrt(Distance.L2Distance(mean, func(x))))
                .Aggregate(0f, (sum, x) => sum + x)
                / total;
            return var;
        }

        static void ClassVariance(ArgsClassVariance cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".var.tsv");

            var variances = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                //.AsParallel().AsOrdered()
                .Select(x => x.Split('\t'))
                .Select(cols => new { c = cols[cmd.label], f = ConvertFeature(cols[cmd.feature], true) })
                .GroupBy(x => x.c)
                .ReportProgress("Groups processed")
                .Select(g =>
                {
                    // calc mean and var for top 20 images
                    var mean20 = CalcMean(g.AsEnumerable().Take(20), x => x.f);
                    var var20 = CalcVariance(g.AsEnumerable().Take(20), mean20, x => x.f);

                    // remove outliers in top 20 and calc cleaned mean20 and var20
                    var top20_cleaned = g.AsEnumerable().Take(20)
                        .Where(x => (float)Math.Sqrt(Distance.L2Distance(mean20, x.f)) < cmd.thresh);
                    bool top20_empty = top20_cleaned.Count() == 0;
                    var mean20_cleaned = top20_empty ? mean20 : CalcMean(top20_cleaned, x => x.f);
                    var var20_cleaned = top20_empty ? 999.0f : CalcVariance(top20_cleaned, mean20_cleaned, x => x.f);

                    var mean_all = CalcMean(g.AsEnumerable(), x => x.f);
                    var var_all = CalcVariance(g.AsEnumerable(), mean_all, x => x.f);

                    var dist_array = g.AsEnumerable()
                        .Select(x => 
                        {
                            var dis = (float)Math.Sqrt(Distance.L2Distance(mean20_cleaned, x.f));
                            return x.c + "\t" + var20_cleaned + "\t" + dis + "\t" + (dis < cmd.thresh);
                        })
                        .ToArray();

                    return new {key = g.Key, var20_cleaned = var20_cleaned, n_top20_cleaned = top20_cleaned.Count(), 
                                var_all = var_all, total = g.Count(), dis_array = dist_array};
                })
                .ToList();

            File.WriteAllLines(cmd.outTsv, variances.OrderBy(x => x.var20_cleaned)
                    .Select(x => x.key + "\t" + x.var20_cleaned + "\t" + x.n_top20_cleaned 
                        + "\t" + x.var_all + "\t" + x.total));

            var lines = variances.SelectMany(x => x.dis_array);
            File.WriteAllLines(Path.ChangeExtension(cmd.inTsv, ".score.tsv"), lines);

            Console.WriteLine("Done.");
        }

        class ArgsViewCheck
        {
            [Argument(ArgumentType.Required, HelpText = "Input INI file for the view of data repo")]
            public string ini = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Max positive data per class (default: 500)")]
            public int maxPos = 500;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Min positive data per class (default: 10)")]
            public int minPos = 10;
        }

        static Dictionary<string, int> SelectSid(ArgsViewCheck cmd)
        {
            var iniParser = new FileIniDataParser();
            var iniData = iniParser.ReadFile(cmd.ini);

            Dictionary<string, int> sid_selected = new Dictionary<string, int>();

            foreach (var sec in iniData.Sections)
            {
                Console.WriteLine("Section: {0}", sec.SectionName);
                string data_file = sec.Keys["data_file"];
                string stat_file = Path.ChangeExtension(data_file, "stat.tsv");
                var sid_count = File.ReadLines(stat_file)
                    .Select(line => line.Split('\t'))
                    .ToDictionary(cols => cols[0], cols => Convert.ToInt32(cols[1]));
                Console.WriteLine("# of entities in data: {0}", sid_count.Count());

                HashSet<string> black_list;
                string black_list_file = sec.Keys["black_list"];
                if (string.IsNullOrEmpty(black_list_file))
                    black_list = new HashSet<string>();
                else
                    black_list = new HashSet<string>(File.ReadLines(black_list_file)
                                        .Select(line => line.Split('\t')[0])
                                        .Distinct(),
                                        StringComparer.Ordinal);

                string white_list_file = sec.Keys["white_list"];
                if (string.IsNullOrEmpty(white_list_file))
                    white_list_file = stat_file;

                var sid_list = File.ReadLines(white_list_file)
                        .Select(line => line.Split('\t')[0])
                        .Where(sid => !black_list.Contains(sid))
                        .Where(sid => !sid_selected.ContainsKey(sid))
                        .Select(sid => Tuple.Create(sid, Math.Min(cmd.maxPos, sid_count[sid])))
                        .Where(tp => tp.Item2 >= cmd.minPos)
                        .Select(tp =>
                        {
                            sid_selected.Add(tp.Item1, tp.Item2);
                            return tp;
                        })
                        .ToList();

                Console.WriteLine("# of entities selected: {0}", sid_list.Count());
                Console.WriteLine("# of images selected: {0}", sid_list.Sum(tp => tp.Item2));
                Console.WriteLine();
            }

            Console.WriteLine("Total # of entities: {0}", sid_selected.Count());
            Console.WriteLine("Total # of images: {0}", sid_selected.Sum(kv => kv.Value));

            return sid_selected;
        }

        static void ViewCheck(ArgsViewCheck cmd)
        {
            SelectSid(cmd);
        }

        class ArgsView2Data: ArgsViewCheck
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace ini file .ext with .tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Max image size (default: 256)")]
            public int size = 256;
        }

        static void View2Data(ArgsView2Data cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.ini, ".tsv");
            string cmd_outTsvLabel = Path.ChangeExtension(cmd.outTsv, ".label.tsv");

            var sid_selected = SelectSid(cmd);
            var sid_count = sid_selected.ToDictionary(kv => kv.Key, kv => 0);

            var sid_in_previous_sections = new HashSet<string>();

            Console.WriteLine("\nStart generating data...");
            var iniParser = new FileIniDataParser();
            var iniData = iniParser.ReadFile(cmd.ini);

            int count = 0;
            int count_saved = 0;

            using (var sw_data = new StreamWriter(cmd.outTsv))
            using (var sw_label = new StreamWriter(cmd_outTsvLabel))
            foreach (var sec in iniData.Sections)
            {
                Console.WriteLine("Section: {0}", sec.SectionName);
                string data_file = sec.Keys["data_file"];
                int image_col = Convert.ToInt32(sec.Keys["image_col"]);
                int label_col = Convert.ToInt32(sec.Keys["label_col"]);

                var sid_in_this_section = new HashSet<string>();
                foreach (var line in File.ReadLines(data_file))
                {
                    count++;
                    var cols = line.Split('\t');
                    string sid = cols[label_col];
                    if (!sid_selected.ContainsKey(sid))
                        continue;
                    if (sid_count[sid] >= cmd.maxPos)
                        continue;

                    // if sid has been used in previous sections, skip it
                    if (sid_in_previous_sections.Contains(sid))
                        continue;
                    
                    sid_in_this_section.Add(sid);

                    sid_count[sid] = sid_count[sid] + 1;

                    try
                    {
                        string img_string;
                        if (cmd.size > 0)
                        {
                            using (var ms = new MemoryStream(Convert.FromBase64String(cols[image_col])))
                            using (var bmp = new Bitmap(ms))
                            {
                                Bitmap img = ImageUtility.DownsizeImage(bmp, cmd.size);
                                byte[] img_buf = ImageUtility.SaveImageToJpegInBuffer(img, 90L);
                                img_string = Convert.ToBase64String(img_buf);
                            }
                        }
                        else
                            img_string = cols[image_col];

                        sw_data.WriteLine("{0}\t{1}", sid, img_string);
                        sw_label.WriteLine(sid);
                        count_saved++;
                        Console.Write("Lines processed: {0}, saved: {1}\r", count, count_saved);
                    }
                    catch
                    {
                        Console.WriteLine("\nError in reading image stream (col:{0}): {1}", 
                            image_col, cols[image_col].Substring(0, Math.Min(50, cols[image_col].Length)));
                    }
                }

                foreach (var sid in sid_in_this_section)
                    sid_in_previous_sections.Add(sid);

                Console.WriteLine();
            }

        }

        class ArgsWrongCeleb
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace InTsv .ext with .wrong.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for label")]
            public int label = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for prediction in format tag1:0.98;tag2:0.01")]
            public int predict = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Confidence threshold for wrong entity (default: 0.8)")]
            public float conf = 0.8f;
        }

        static void WrongCeleb(ArgsWrongCeleb cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".wrong.tsv");

            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .Select(line => line.Split('\t'))
                .Select(cols => Tuple.Create(cols[cmd.label], cols[cmd.predict]))
                .GroupBy(tp => tp.Item1)
                .ReportProgress("Group processed")
                .Select(g =>
                {
                    string ground_true = g.Key;
                    var wrong = g.AsEnumerable()
                        .Select(tp => tp.Item2.Split(';'))
                        .Where(cols => cols.Length > 0)
                        .Select(cols => cols[0].Split(':'))
                        .Select(pred => new { cls = pred[0], conf = Convert.ToSingle(pred[1]) })
                        .Where(pred => pred.conf > cmd.conf && string.CompareOrdinal(pred.cls, ground_true) != 0)
                        .Select(pred => pred.cls + ":" + pred.conf);

                    return Tuple.Create(g.Key, g.Count(), wrong.Count(), string.Join(";", wrong));
                })
                .Where(tp => tp.Item3 > 0)
                .OrderByDescending(tp => tp.Item3)
                .Select(tp => tp.Item1 + "\t" + tp.Item2 + "\t" + tp.Item3 + "\t" + tp.Item4);

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("Done.");
        }
    }
}