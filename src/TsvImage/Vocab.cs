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
        class ArgsVocab
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
        }

        static void Vocab(ArgsVocab cmd)
        {
            var vocab_lines = File.ReadLines(cmd.inTsv)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrEmpty(line) && !line.EndsWith(":"))
                .ToList();
            var vocab = vocab_lines
                .SelectMany(line => line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries), 
                            (line, word) => word.Trim())
                .Where(word => word.Length > 1)
                .Distinct()
                .ToList();
            File.WriteAllLines(cmd.outTsv, vocab);
        }

        class ArgsBrqWithVocab
        {
            [Argument(ArgumentType.Required, HelpText = "Input BRQ TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Vocab file")]
            public string vocab = null;
        }

        static void BrqWithVocab(ArgsBrqWithVocab cmd)
        {
            var vocab = File.ReadLines(cmd.vocab)
                .Select((line, i) => Tuple.Create(line, i))
                .SelectMany(tp => tp.Item1.Split(';'), 
                            (tp, phrase) => Tuple.Create(phrase.Trim().Split(new char[]{' ', '-'})
                                                               .Select(word => word.Trim().ToLower()).ToArray(), 
                                                         tp.Item2))
                .ToArray();

            var vocab_by_id = vocab
                .GroupBy(tp => tp.Item2)
                .OrderBy(g => g.Key)
                .Select(g => g.AsEnumerable().Select(tp => tp.Item1).ToArray())
                .ToArray();

            // dict to map phrase word to phrase id
            var dict = vocab
                .SelectMany(tp => tp.Item1, (tp, word) => Tuple.Create(word.ToLower(), tp.Item2))
                .GroupBy(tp => tp.Item1)
                .Select(g => Tuple.Create(g.Key, g.AsEnumerable().Select(tp => tp.Item2).ToArray()))
                .ToDictionary(tp => tp.Item1, tp => tp.Item2, StringComparer.OrdinalIgnoreCase);

            Console.WriteLine("Start to select queries...");

            int count = 0;
            var brq = File.ReadLines(cmd.inTsv)
                .Select(line => line.Replace("&quot;", "").Replace("&amp;", "").Trim())
                .Take(1000 * 100 * 1)
                .Select(line => line.Split('\t'))
                .Select(cols =>
                {
                    var query = cols[0].Split(' ').Select(word => word.ToLower()).ToArray();

                    // find vocab phrases that overlap with this query, and return their ids
                    var id_list = new List<int>();
                    foreach (var w in query)
                    {
                        if (dict.ContainsKey(w))
                            id_list.AddRange(dict[w]);
                    }

                    id_list = id_list
                        .Distinct()
                        .Select(id =>
                        {
                            var vocab_phrases = vocab_by_id[id];
                            if (vocab_phrases.Count() == 1 && vocab_phrases[0].Count() == 1)
                                return id;
                            foreach (var vacab_phrase in vocab_phrases)
                            {
                                int match_count = 0;
                                foreach (var word in vacab_phrase)
                                    if (Array.IndexOf(query, word.ToLower()) >= 0)
                                        match_count++;
                                if (match_count == vacab_phrase.Count())
                                    return id;
                            }

                            return -1;
                        })
                        .Where(id => id >= 0)
                        .ToList();

                    Console.Write("Lines processed: {0}\r", ++count);
                    return Tuple.Create(cols, id_list);
                })
                .Where(tp => tp.Item2.Count() > 0)
                .Select(tp => Tuple.Create(tp.Item1, string.Join(";", tp.Item2.Select(id => id.ToString())),
                                           string.Join(";", tp.Item2.Select(id => string.Join(" ", vocab_by_id[id][0])))))
                .Select(tp => string.Join("\t", tp.Item1) + "\t" + tp.Item2 + "\t" + tp.Item3);

            File.WriteAllLines(Path.GetFileNameWithoutExtension(cmd.inTsv) + ".withvoab.tsv", brq);
            Console.WriteLine("\nDone.");
        }
    }
}