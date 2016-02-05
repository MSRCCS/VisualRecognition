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
        class ArgsNGram
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for label string")]
            public int labelCol = -1;
        }

        class ArgsNGram2Id
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Ngram table (col0:ngram, col1:count)")]
            public string ngram = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for label string")]
            public int labelCol = -1;
        }

        class ArgsNGramClean
        {
            [Argument(ArgumentType.Required, HelpText = "BRQ Query set file")]
            public string brq = null;
            [Argument(ArgumentType.Required, HelpText = "Ngram table (col0:ngram, col1:count)")]
            public string ngram = null;
        }

        static IEnumerable<string> NGramSet(string label)
        {
            char[] sep = new char[] { ' ' };
            var words = label.Split(sep);
            for (int i = 0; i < words.Length; i++)
                yield return words[i];

            for (int i = 0; i < words.Length - 1; i++)
                yield return words[i] + ' ' + words[i + 1];

            for (int i = 0; i < words.Length - 2; i++)
                yield return words[i] + ' ' + words[i + 1] + ' ' + words[i + 2];

            for (int i = 0; i < words.Length - 3; i++)
                yield return words[i] + ' ' + words[i + 1] + ' ' + words[i + 2] + ' ' + words[i + 3];
        }

        static void NGram(ArgsNGram cmd)
        {
            var ngram = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var labels = File.ReadLines(cmd.inTsv)
                    .Select(line => line.Split('\t'))
                    .Select(cols => cols[cmd.labelCol].Replace("&quot;", "").Replace("&amp;", "").Trim());
            char[] sep = new char[] { ' ' };
            int count = 0;
            foreach (var label in labels)
            {
                foreach (var ng in NGramSet(label))
                    update_ngram(ngram, ng);

                count++;
                Console.Write("Processed {0} lines\r", count);
            }
            Console.WriteLine();

            var ordered_ngram = ngram.OrderByDescending(kv => kv.Value)
                                .Where(kv => kv.Value > 50)
                                .Select(kv => string.Format("{0}\t{1}", kv.Key, kv.Value));
            File.WriteAllLines(cmd.outTsv, ordered_ngram);
            Console.WriteLine("Result saved to {0}", cmd.outTsv);
        }

        static void NGram2Id(ArgsNGram2Id cmd)
        {
            var ngram = File.ReadLines(cmd.ngram)
                .Select(line => line.Split('\t')[0])
                //.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((line, idx) => Tuple.Create(line, idx))
                .SelectMany(tp => {
                                      Console.WriteLine(tp.Item1);
                                      return tp.Item1.Split(';');
                                  },
                            (tp, phrase) => Tuple.Create(phrase.Trim(), tp.Item2))
                .ToDictionary(tp => tp.Item1, tp => tp.Item2, StringComparer.OrdinalIgnoreCase);

            Console.WriteLine("Dictionary size: {0}", ngram.Values.Max() + 1);

            int count = 0;
            int max_ngram_count = 0;
            var lines = File.ReadLines(cmd.inTsv).AsParallel().AsOrdered()
                .Select(line => line.Split('\t')[cmd.labelCol].Replace("&quot;", "").Replace("&amp;", "").Trim())
                .Select(label =>
                {
                    var id_set = NGramSet(label)
                        .Where(ng => ngram.ContainsKey(ng))
                        .Select(ng => ngram[ng].ToString())
                        .ToList();
                    id_set.Add("-1");   // add a dummy id to avoid exception in TsvDataLayer
                    max_ngram_count = Math.Max(max_ngram_count, id_set.Count());
                    string id_str = string.Join(";", id_set);
                    count++;
                    Console.Write("Lines processed: {0}\r", count);
                    return id_str;
                });

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine();
            Console.WriteLine("Result saved to {0}", cmd.outTsv);
            Console.WriteLine("Max ngram count: {0}", max_ngram_count);
        }

        static bool ContainWords(string str, string sub_str)
        {
            int index = str.IndexOf(sub_str);
            if (index < 0)
                return false;

            // ensure the match boundary at word level
            int pos_before = index - 1;
            int pos_after = index + sub_str.Length;
            if (pos_before >= 0 && str[pos_before] != ' ')
                return false;
            if (pos_after < str.Length && str[pos_after] != ' ')
                return false;

            return true;
        }

        static void NGramClean(ArgsNGramClean cmd)
        {
            Console.WriteLine("Load ngram table...");
            var ngram = File.ReadLines(cmd.ngram).AsParallel()
                .Select(line => line.Split('\t')[0].Trim())
                .Where(str => !string.IsNullOrEmpty(str))
                .Where(str => str.Any(char.IsLetter))
                .OrderBy(str => str)
                .ToArray();
            Console.WriteLine("ngram count: {0}", ngram.Count());
            string withletter_file = Path.GetFileNameWithoutExtension(cmd.ngram) + ".withletter.tsv";
            File.WriteAllLines(withletter_file, ngram);
            Console.WriteLine("Ngram with letters saved to {0}", withletter_file);

            Console.WriteLine("Start to find sub string pairs...");
            int count = 0;
            var pairs = ngram.AsParallel()
                           .SelectMany(x => ngram, (x, y) => Tuple.Create(x, y))
                           .Where(t =>
                           {
                               // the first ngram must be shorter than the second
                               if (t.Item1.Length >= t.Item2.Length)
                                   return false;
                               if (!ContainWords(t.Item2, t.Item1))
                                   return false;
                               Console.Write("Pairs found: {0}\r", ++count);
                               return true;
                           })
                           .OrderBy(tuple => tuple.Item1)
                           .ToArray();
            Console.WriteLine("\nPair count: {0}", pairs.Count());

            Console.WriteLine("Load brq queries ...");
            var brq = File.ReadLines(cmd.brq).AsParallel().AsOrdered()
                .Select(line => line.Replace("&quot;", "").Replace("&amp;", "").Trim())
                .Take(1024 * 1024 * 10)
                .ToArray();
            Console.WriteLine("Brq query count: {0}", brq.Count());

            Console.WriteLine("Check if A < B and BRQ(A) ~ BRQ(B)...");
            var groups = pairs.GroupBy(t => t.Item1).ToArray();
            Console.WriteLine("Total groups: {0}", groups.Count());
            count = 0;
            var remove_list = groups.AsParallel().AsOrdered()
                .Where(g =>
                {
                    Interlocked.Increment(ref count);
                    Console.Write("Processing {0}\r", count);

                    var brq_with_key = brq.AsParallel().Where(q => ContainWords(q, g.Key)).ToArray();
                    if (brq_with_key.Count() < 10)
                    {
                        Console.WriteLine("Remove {0}", g.Key);
                        return true;
                    }
                    var max_overlap = g.AsEnumerable()
                        .Select(tuple => Tuple.Create(tuple.Item2, brq_with_key.Where(q => ContainWords(q, tuple.Item2)).Count()))
                        .OrderByDescending(tuple => tuple.Item2)
                        .First();
                    float ratio = (float)max_overlap.Item2 / brq_with_key.Count();
                    if (ratio > 0.8)
                    {
                        Console.WriteLine("Remove {0}, {1}, {2}, {3}", g.Key, max_overlap.Item1, brq_with_key.Count(), max_overlap.Item2);
                        return true;
                    }
                    return false;
                })
                .Select(g => g.Key)
                .OrderBy(str => str)
                .ToArray();
            Console.WriteLine("\nRemoved count: {0}", remove_list.Count());

            var keep_list = ngram.Except(remove_list).OrderBy(str => str);
            string removed_file = Path.GetFileNameWithoutExtension(cmd.ngram) + ".removed.tsv";
            string keep_file = Path.GetFileNameWithoutExtension(cmd.ngram) + ".keep.tsv";
            File.WriteAllLines(removed_file, remove_list);
            File.WriteAllLines(keep_file, keep_list);
            Console.WriteLine("Removed list saved to {0}", removed_file);
            Console.WriteLine("Kept list saved to {0}", keep_file);
        }

        class ArgsNGramEval
        {
            [Argument(ArgumentType.Required, HelpText = "Ngram table (col0:ngram, col1:count)")]
            public string ngram = null;
            [Argument(ArgumentType.Required, HelpText = "Input prediction TSV file")]
            public string inTsvPredict = null;
            [Argument(ArgumentType.Required, HelpText = "Input label TSV file")]
            public string inTsvLabel = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for prediction scores")]
            public int predCol = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for label")]
            public int labelCol = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Threshold for precision")]
            public float conf = 0.1f;
        }

        static int[] UnrollLabel(string label, int dim)
        {
            int[] unrolled_label = new int[dim];
            Array.Clear(unrolled_label, 0, unrolled_label.Count());
            var compact_label = label.Split(';').Select(l => Convert.ToInt32(l));
            foreach (var l in compact_label)
            {
                if (l >= 0)
                    unrolled_label[l] = 1;
            }
            return unrolled_label;
        }

        public static IEnumerable<float> CumulativeAccuracy(IEnumerable<int> sequence)
        {
            float sum = 0;
            int count = 0;
            foreach (var item in sequence)
            {
                count++;
                sum += item;
                yield return sum / count;
            }
        }

        //ngrameval -ngram Model.brq1m\brq1m.ngram.dict.tsv  -intsvpredict brq1m.jpg.test.1000.prob.tsv -intsvlabel brq1m.jpg.test.label.tsv -predcol 4 -outtsv brq1m.ngram.dict.eval.tsv
        static void NGramEval(ArgsNGramEval cmd)
        {
            Console.WriteLine("Loading ngram table...");
            var ngram = File.ReadLines(cmd.ngram)
                .Select(line => line.Split('\t')[0])
                .ToArray();

            int count = 0;
            var result = File.ReadLines(cmd.inTsvPredict)//.AsParallel().AsOrdered()
                .Select(line => line.Split('\t'))
                .Select(cols =>
                {
                    var pred = ConvertFeature(cols[cmd.predCol], false);
                    var compact = pred//.Where((s, i) => i % 2 == 1)
                        .Select((s, i) => Tuple.Create(s, i))
                        .Where(tp => tp.Item1 > 0.4)
                        .OrderByDescending(tp => tp.Item1)
                        .Select(tp => string.Format("{0}:{1:0.00}", ngram[tp.Item2], tp.Item1))
                        .ToList();
                    cols[cmd.predCol] = string.Join(";", compact);
                    Console.Write("Lines processed: {0}\r", ++count);
                    return cols;
                })
                .Where(cols => !string.IsNullOrEmpty(cols[cmd.predCol]))
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, result);
            Console.WriteLine();
        }

        static void NGramEvalBak(ArgsNGramEval cmd)
        {
            Console.WriteLine("Loading ngram table...");
            var ngram = File.ReadLines(cmd.ngram)
                .Select(line => line.Split('\t')[0])
                .ToArray();

            Console.WriteLine("Loading prediction scores...");
            var predicts = File.ReadLines(cmd.inTsvPredict).AsParallel().AsOrdered()
                //.Take(10000)
                .Select(line => line.Split('\t')[cmd.predCol])
                .Select(str => ConvertFeature(str, false))
                .ToList();
            int dim = predicts.First().Count() / 2;

            Console.WriteLine("Loading ground truth labels...");
            var labels = File.ReadLines(cmd.inTsvLabel)
                //.Take(10000)
                .Select(line => line.Split('\t')[cmd.labelCol])
                .Select(str => UnrollLabel(str, dim))
                .ToArray();

            int total_count = labels.Count();

            int count = 0;
            var result = ngram.AsParallel().AsOrdered()
                .Select((ng, i) =>
                {
                    var pred = predicts.Select(p => p[i * 2]);
                    var label = labels.Select(l => l[i]);
                    int gt_count = label.Where(l => l > 0).Count();
                    if (gt_count < 10)
                        return Tuple.Create(ng, gt_count, -1.0f, -1.0f);
                    var zipped = pred.Zip(label, (p, l) => Tuple.Create(p, l))
                            .OrderByDescending(tp => tp.Item1)
                            .ToArray();

                    var acc = CumulativeAccuracy(zipped.Select(tp => tp.Item2)).ToArray();
                    int n = acc.Count() - 1;
                    for (; n >= 0; n--)
                    {
                        if (acc[n] > cmd.conf)
                            break;
                    }
                    int correct_count = zipped.Take(n).Where(tp => tp.Item2 > 0).Count();
                    float precision = (float)correct_count / n;
                    float recall = (float)correct_count / gt_count;
                    
                    Console.Write("Ngram analzed: {0}\r", ++count);

                    return Tuple.Create(ng, gt_count, precision, recall);
                })
                .OrderByDescending(tp => tp.Item3)
                .Select(tp => string.Format("{0}\t{1}\t{2}\t{3}", tp.Item1, tp.Item2, tp.Item3, tp.Item4));

            Console.WriteLine("Saving result...");
            File.WriteAllLines(cmd.outTsv, result);
            Console.WriteLine("\nDone");

        }
    }
}