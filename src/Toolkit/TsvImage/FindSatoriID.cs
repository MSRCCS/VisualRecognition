using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CmdParser;
using EntityTools;

namespace TsvImage
{
    partial class Program
    {
        class ArgsFindSatoriID
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file, which contains the entity name string")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file with satori IDs, and some detail info if asked")]
            public string outTsv = "";
            [Argument(ArgumentType.Required, HelpText = "Column index for entity name string, start from 0")]
            public int dataCol = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "output extra detail info about the entity, if any")]
            public bool extraInfo = false;
        }
        static void FindSatoriID(ArgsFindSatoriID cmd)
        {
            /*
            var dict = File.ReadLines(cmd.inTsv)
                    .Select(line => line.Split('\t'))
                    .GroupBy(cols => cols[cmd.dataCol])
                    .ToDictionary( entityName => entityName, x => Guid.Empty);            
                    */

            var dict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
           
            int count = 0;
            int count_nonEmptyResult = 0;
            int count_foundID = 0;

            var lines = File.ReadLines(cmd.inTsv)
                //.AsParallel().AsOrdered()
                .Select(line => line.Split('\t'))
                .Select(cols =>
                {
                    Console.Write("lines processed: {0}\r", ++count);
                    var oriLine = string.Join("\t", cols);
                    string entityName = cols[cmd.dataCol].Trim().ToLower();
                    string[] satoriInfo;
                    if (!dict.ContainsKey(entityName))
                    {
                        satoriInfo = EntityTools.Entitytools.getSatoriInfo(entityName).Split('\t');
                        dict.Add(entityName, satoriInfo);
                    }
                    else
                        satoriInfo = dict[entityName];

                    if (entityName.Length > 0)
                    {
                        count_nonEmptyResult++;
                        if (satoriInfo[0] != Guid.Empty.ToString())
                            count_foundID++;
                    }
                    string satoriInfoStr = (cmd.extraInfo) ? string.Join("\t", satoriInfo) : satoriInfo[0];
                    //Console.Write("EntityName={0}\tSatoriInfo={1}",entityName,satoriInfoStr);
                    return oriLine + "\t" + satoriInfoStr;
                });

            string outTsv = (cmd.outTsv == "") ? Path.ChangeExtension(cmd.inTsv, "satori.tsv") : cmd.outTsv;
            File.WriteAllLines(outTsv, lines);
            Console.WriteLine("\n# of Lines:\t{0}\n# of non-empty entity name:\t{1}\n# of satoriID found:\t{2}\n# of unique entities:\t{3}", count, count_nonEmptyResult, count_foundID, dict.Count());
        }
    }
}
