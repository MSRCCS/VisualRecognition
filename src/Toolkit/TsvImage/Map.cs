using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using CmdParser;

namespace TsvImage
{
    partial class Program
    {
        class ArgsMap
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file, with optional col index (default 0) prefixed with '?'. E.g. abc.tsv?1")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "dictionary file, each line is a Key Value pair, delimited by TAB")]
            public string dictTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file after mapping")]
            public string outTsv = "";
        }
        static void Map(ArgsMap cmd)
        {
                
            
            //load the dictionary file: Key \t Value
            Console.WriteLine("Loading dictionary file {0}", cmd.dictTsv);
            var dict = File.ReadLines(cmd.dictTsv)
                    .Select( (line,x ) =>
                    {
                        Console.Write("processed lines: {0}\r", x);
                        return line.Split('\t');
                    })
                    .GroupBy(cols => cols[0], StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First()[1], StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("Loaded {0} entries from dictionary file {1}", dict.Count(), cmd.dictTsv);

            var args_inTsv = cmd.inTsv.Split('?');
            cmd.inTsv = args_inTsv[0];
            int col_inTsv = args_inTsv.Length > 1 ? Convert.ToInt32(args_inTsv[1]) : 0;
            

            int count = 0;      //# of lines in the input file
            int count_mapped = 0;  //# of lines which has been mapped

            //load the dictionary file: Key \t Value
            Console.WriteLine("Parsing input file {0}", cmd.inTsv);
            var lines = File.ReadLines(cmd.inTsv)
                .AsParallel().AsOrdered()
                .Select(line => line.Split('\t'))
                .Select(cols =>
                {
                    Interlocked.Increment(ref count);
                    Console.Write("lines processed: {0}\r", count);
                    string oriStr = cols[col_inTsv].Trim().ToLower();
                    if (dict.ContainsKey(oriStr))
                    {
                        cols[col_inTsv] = dict[oriStr];
                        Interlocked.Increment(ref count_mapped);
                    }
                    return string.Join("\t", cols);
                });

            string outTsv = (cmd.outTsv == "") ? Path.ChangeExtension(cmd.inTsv, "mapped.tsv") : cmd.outTsv;
            File.WriteAllLines(outTsv, lines);
            Console.WriteLine("\n# of Lines:\t{0}", count);
            Console.WriteLine("# of lines mapped:\t{0}", count_mapped);
        }
    }
}
