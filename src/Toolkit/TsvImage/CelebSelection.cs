using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
            [Argument(ArgumentType.AtMostOnce, HelpText = "Outlier threshold (default: 0.75)")]
            public float thresh = 0.75f;
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

                    var total_selected = 0;
                    var dist_array = g.AsEnumerable()
                        .Select(x =>
                        {
                            var dis = (float)Math.Sqrt(Distance.L2Distance(mean20_cleaned, x.f));
                            var select = dis < cmd.thresh;
                            if (select) total_selected++;
                            return var20_cleaned + "\t" + dis + "\t" + select;
                        })
                        .ToArray();

                    return new
                    {
                        key = g.Key,
                        var20_cleaned = var20_cleaned,
                        n_top20_cleaned = top20_cleaned.Count(),
                        var_all = var_all,
                        total = g.Count(),
                        selected = total_selected,
                        dist_array = dist_array
                    };
                })
                .ToList();

            File.WriteAllLines(cmd.outTsv, variances.OrderBy(x => x.var20_cleaned)
                    .Select(x => x.key + "\t" + x.var20_cleaned + "\t" + x.n_top20_cleaned
                        + "\t" + x.var_all + "\t" + x.total + "\t" + x.selected));

            var lines = variances.SelectMany(x => x.dist_array);
            File.WriteAllLines(Path.ChangeExtension(cmd.inTsv, ".score.tsv"), lines);

            Console.WriteLine("\nDone.");
        }

        class ArgsClassMeanVar
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace InTsv .ext with .mean.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for feature")]
            public int feature = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for label")]
            public int label = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Feature normalization (default: false, no normalization)")]
            public bool norm = false;
        }

        static void ClassMeanVar(ArgsClassMeanVar cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".mean.tsv");

            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .Select(x => x.Split('\t'))
                .Select(cols => new { c = cols[cmd.label], f = ConvertFeature(cols[cmd.feature], cmd.norm) })
                .GroupBy(x => x.c)
                .ReportProgress("Groups processed")
                .Select(g =>
                {
                    // calc mean and var for top 20 images
                    var mean = CalcMean(g.AsEnumerable(), x => x.f);
                    var var = CalcVariance(g.AsEnumerable(), mean, x => x.f);

                    byte[] buff = new byte[mean.Length * sizeof(float)];
                    Buffer.BlockCopy(mean, 0, buff, 0, buff.Length);
                    var mean_b64 = Convert.ToBase64String(buff);

                    return new
                    {
                        key = g.Key,
                        count = g.Count(),
                        var = var.ToString(),
                        mean = mean_b64
                    };
                })
                .Select(x => x.key + "\t" + x.count + "\t" + x.var + "\t" + x.mean);

            File.WriteAllLines(cmd.outTsv, lines);

            Console.WriteLine("\nDone.");
        }

        class ArgsViewCheck
        {
            [Argument(ArgumentType.Required, HelpText = "Input INI file for the view of data repo")]
            public string ini = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Max data per class (default: no limit)")]
            public int max = int.MaxValue;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Min data per class (default: 0)")]
            public int min = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "If the same class label appears in different views, only use the data in first view, or merge them (default=fase)")]
            public bool merge = false;

        }

        static ConcurrentDictionary<string, int> SelectSid(ArgsViewCheck cmd)
        {
            var iniParser = new FileIniDataParser();
            var iniData = iniParser.ReadFile(cmd.ini);

            ConcurrentDictionary<string, int> sid_selected = new ConcurrentDictionary<string, int>();

            HashSet<string> global_black_list;
            string global_black_list_file;
            if (!iniData.TryGetKey("black_list", out global_black_list_file))
                global_black_list = new HashSet<string>();
            else
                global_black_list = new HashSet<string>(File.ReadLines(global_black_list_file)
                                        .Select(line => line.Split('\t')[0])
                                        .Distinct(),
                                        StringComparer.Ordinal);
            
            foreach (var sec in iniData.Sections)
            {
                Console.WriteLine("Section: {0}", sec.SectionName);
                string data_file = sec.Keys["data_file"];
                string stat_file = Path.ChangeExtension(data_file, "stat.tsv");
                if (!File.Exists(stat_file))
                {
                    int label_col = Convert.ToInt32(sec.Keys["label_col"]);
                    Console.WriteLine("Cannot find {0} file, use 'Tsv.exe CountLabel -inTsv {1} -label {2}' to create it.", stat_file, data_file, label_col);
                }
                var sid_count = File.ReadLines(stat_file)
                    .Select(line => line.Split('\t'))
                    .ToDictionary(cols => cols[0], cols => Convert.ToInt32(cols[1]));
                Console.WriteLine("# of entities in data : {0}", sid_count.Count());

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

                //if cmd.max is not specified (i.e. no limit), use max_per_class in .ini file
                //cmd.max has higher priority, if it is specified in command line param
                int max_per_class = cmd.max;  
                string max_per_class_str = sec.Keys["max_per_class"];
                if (!string.IsNullOrWhiteSpace(max_per_class_str))
                     max_per_class = Convert.ToInt32(max_per_class_str);
                if (max_per_class < cmd.min)
                    Console.WriteLine("warning! max_per_class ({0}) < cmd.min ({1}), which means no data will be selected from this section.", max_per_class, cmd.min);
                
                var sid_list = File.ReadLines(white_list_file)
                        .Select(line => line.Split('\t')[0])
                        .Where(sid => !global_black_list.Contains(sid))
                        .Where(sid => !black_list.Contains(sid))
                        .Where(sid => cmd.merge || !sid_selected.ContainsKey(sid))
                        .Select(sid => Tuple.Create(sid, Math.Min( Math.Min(cmd.max - (sid_selected.ContainsKey(sid) ? sid_selected[sid] : 0), sid_count[sid]), max_per_class)))
                        .Where(tp => tp.Item2 >= cmd.min)
                        .Select(tp =>
                        {
                            sid_selected.AddOrUpdate(tp.Item1, tp.Item2, (existingID, existingCount) => existingCount + tp.Item2);
                            return tp;
                        })
                        .ToList();

                Console.WriteLine("# of entities selected: {0}", sid_list.Count());
                Console.WriteLine("# of images selected  : {0}", sid_list.Sum(tp => tp.Item2));
                Console.WriteLine();
            }

            Console.WriteLine("Total # of entities: {0}", sid_selected.Count());
            Console.WriteLine("Total # of images  : {0}", sid_selected.Sum(kv => kv.Value));

            return sid_selected;
        }

        static void ViewCheck(ArgsViewCheck cmd)
        {
            SelectSid(cmd);
        }

        class ArgsView2Data : ArgsViewCheck
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace ini file .ext with .tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Max image size (default: 0, no resize)")]
            public int size = 0;
        }

        static void View2Data(ArgsView2Data cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.ini, ".tsv");
            string cmd_outTsvLabel = Path.ChangeExtension(cmd.outTsv, ".label.tsv");

            var sid_selected = SelectSid(cmd);
            var sid_count = sid_selected.ToDictionary(kv => kv.Key, kv => 0);
            long total_sample_num = sid_selected.Sum(kv => kv.Value);

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

                    HashSet<string> black_list;
                    string black_list_file = sec.Keys["black_list"];
                    if (string.IsNullOrEmpty(black_list_file))
                        black_list = new HashSet<string>();
                    else
                        black_list = new HashSet<string>(File.ReadLines(black_list_file)
                                            .Select(line => line.Split('\t')[0])
                                            .Distinct(),
                                            StringComparer.Ordinal);
                    bool has_blacklist = black_list.Count() > 0;

                    HashSet<string> white_list;
                    string white_list_file = sec.Keys["white_list"];
                    if (string.IsNullOrEmpty(white_list_file))
                        white_list = new HashSet<string>();
                    else
                        white_list = new HashSet<string>(File.ReadLines(white_list_file)
                                            .Select(line => line.Split('\t')[0])
                                            .Distinct(),
                                            StringComparer.Ordinal);
                    bool has_whitelist = white_list.Count() > 0;

                    int max_per_class = cmd.max;
                    string max_per_class_str = sec.Keys["max_per_class"];
                    if (!string.IsNullOrWhiteSpace(max_per_class_str))
                        max_per_class = Convert.ToInt32(max_per_class_str);

                    ConcurrentDictionary<string, int> sid_in_this_section = new ConcurrentDictionary<string, int>();
                    foreach (var line in File.ReadLines(data_file))
                    {
                        count++;
                        var cols = line.Split('\t');
                        string sid = cols[label_col];
                        if (!sid_selected.ContainsKey(sid))
                            continue;
                        if (has_whitelist && !white_list.Contains(sid))
                            continue;
                        if (has_blacklist && black_list.Contains(sid))
                            continue;
                        if (sid_selected[sid] <= 0 || (sid_in_this_section.ContainsKey(sid) && sid_in_this_section[sid] == max_per_class))
                            continue;
                        
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
                            sid_selected[sid]--;
                            sid_in_this_section.AddOrUpdate(sid, 1, (existingID, existingCount) => existingCount + 1);
                            Console.Write("Lines processed: {0}, saved: {1}({2:P})\r", count, count_saved, (float)count_saved/total_sample_num);
                        }
                        catch
                        {
                            Console.WriteLine("\nError in reading image stream (col:{0}): {1}",
                                image_col, cols[image_col].Substring(0, Math.Min(50, cols[image_col].Length)));
                        }
                    }

                    Console.WriteLine();
                }
            Trace.Assert(sid_selected.Sum(tp => tp.Value) == 0);
            Trace.Assert(count_saved == total_sample_num);                
        }

        class ArgsWrongCeleb
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace InTsv .ext with .wrong.stat.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for label")]
            public int label = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for prediction in format tag1:0.98;tag2:0.01")]
            public int predict = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Confidence threshold for wrong entity (default: 0.8)")]
            public float conf = 0.8f;
            [Argument(ArgumentType.AtMostOnce, HelpText = "white list file for label, the samples with class labels in column#0 will be treated as correctly classified")]
            public string whiteListTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "black list file for prediction, the samples with prediction in column#0 will be treated as correctly classified")]
            public string blackListTsv = null;


        }

        static void WrongCeleb(ArgsWrongCeleb cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".wrong.stat.tsv");

            HashSet<string> setLabelWhiteList;
            if (string.IsNullOrEmpty(cmd.whiteListTsv))
                setLabelWhiteList = new HashSet<string>();
            else
            {
                setLabelWhiteList = new HashSet<string>(File.ReadLines(cmd.whiteListTsv)
                                    .Select(line => line.Split('\t')[0].Trim().ToLower())
                                    .Distinct(),
                                    StringComparer.OrdinalIgnoreCase);
                Console.WriteLine("Loaded {0} entries from white list file for labels {1}", setLabelWhiteList.Count(), cmd.whiteListTsv);
            }

            HashSet<string> setPredictionBlackList;
            if (string.IsNullOrEmpty(cmd.blackListTsv))
                setPredictionBlackList = new HashSet<string>();
            else
            {
                setPredictionBlackList = new HashSet<string>(File.ReadLines(cmd.blackListTsv)
                                    .Select(line => line.Split('\t')[0].Trim().ToLower())
                                    .Distinct(),
                                    StringComparer.OrdinalIgnoreCase);
                Console.WriteLine("Loaded {0} entries from black list file for predictions {1}", setPredictionBlackList.Count(), cmd.blackListTsv);
            }
            
            var predictions = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .Select(line => line.Split('\t'))
                .Select(cols => Tuple.Create(cols[cmd.label], cols[cmd.predict]))
                .ToArray();

            var wrongs = predictions
                .Select(tp =>
                {
                    string ground_true = tp.Item1;
                    string wrong = "Unknown";
                    if (setLabelWhiteList.Contains(ground_true, StringComparer.OrdinalIgnoreCase))
                        return wrong;
                    var predicts = tp.Item2.Split(';');
                    if(!string.IsNullOrEmpty(tp.Item2) && predicts.Length > 0)
                    {
                        var pred = predicts.First().Split(':');
                        if (setPredictionBlackList.Contains(pred[0], StringComparer.OrdinalIgnoreCase))
                            return wrong;
                        float conf = Convert.ToSingle(pred[1]);
                        if (conf > cmd.conf && string.CompareOrdinal(pred[0], ground_true) != 0)
                            wrong = predicts.First();
                    }
                    return wrong;
                });

            File.WriteAllLines(Path.ChangeExtension(cmd.inTsv, ".wrong.tsv"), wrongs);
            Console.WriteLine("\nDetailed result saved.");

            var lines = predictions
                .GroupBy(tp => tp.Item1)
                .ReportProgress("Group processed")
                .Select(g =>
                {
                    string ground_true = g.Key;
                    var wrong = g.AsEnumerable()
                        .Where(tp => !string.IsNullOrEmpty(tp.Item2))
                        .Select(tp => tp.Item2.Split(';'))
                        .Where(cols => cols.Length > 0)
                        .Select(cols => cols[0].Split(':'))
                        .Select(pred => new { cls = pred[0], conf = Convert.ToSingle(pred[1]) })
                        .Where(pred => !setPredictionBlackList.Contains(pred.cls, StringComparer.OrdinalIgnoreCase))
                        .Where(pred => pred.conf > cmd.conf && string.CompareOrdinal(pred.cls, ground_true) != 0)
                        .GroupBy(pred => pred.cls)
                        .Select(g_pred => new { cls = g_pred.Key, num = g_pred.Count() })
                        .OrderByDescending(x => x.num)
                        .ToArray();
                    int num_wrong = wrong.Sum(x => x.num);
                    float wrong_ratio = (float)num_wrong / g.Count();
                    if (setLabelWhiteList.Contains(ground_true, StringComparer.OrdinalIgnoreCase))
                        wrong_ratio = 0.0f;
                    return Tuple.Create(g.Key, wrong_ratio , g.Count(), num_wrong, string.Join(";", wrong.Select(x => x.cls + ":" + x.num)));
                })
                .Where(tp => tp.Item4 > 0)
                .OrderByDescending(tp => tp.Item2)
                .ThenByDescending(tp => tp.Item3)
                .Select(tp => tp.Item1 + "\t" + tp.Item2 + "\t" + tp.Item3 + "\t" + tp.Item4 + "\t" + tp.Item5);

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("Done.");
        }

        class ArgsCeleb2Phase
        {
            [Argument(ArgumentType.Required, HelpText = "Input entity type file")]
            public string entityType = null;
            [Argument(ArgumentType.Required, HelpText = "Input type phase file")]
            public string typePhase = null;
        }

        static void Celeb2Phase(ArgsCeleb2Phase cmd)
        {
            var typeDict = File.ReadLines(cmd.typePhase)
                .Select(line => line.Split('\t'))
                .Where(cols => string.CompareOrdinal(cols[2], "n.a") != 0)
                .ToDictionary(cols => cols[0], cols => Convert.ToInt32(cols[2]));

            var entity_phase = File.ReadLines(cmd.entityType)
                .ReportProgress("Lines read")
                .Select(line => line.Split('\t'))
                .Where(cols => typeDict.ContainsKey(cols[1]))
                .GroupBy(cols => cols[0])
                .Select(g =>
                {
                    var max_phase = g.AsEnumerable()
                        .Select(cols => new { sid = cols[0], phase = typeDict[cols[1]] })
                        .OrderByDescending(x => x.phase)
                        .First();
                    return max_phase;
                })
                .OrderBy(x => x.phase)
                .ThenBy(x => x.sid)
                .Select(x => x.sid + "\t" + x.phase);

            File.WriteAllLines(Path.ChangeExtension(cmd.entityType, ".phase.tsv"), entity_phase);
        }
    }
}