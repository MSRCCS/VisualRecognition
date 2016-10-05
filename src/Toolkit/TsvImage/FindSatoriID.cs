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
            [Argument(ArgumentType.AtMostOnce, HelpText = "output detail info about the entity, if any, default=false")]
            public bool extraInfo = false;
            [Argument(ArgumentType.AtMostOnce, HelpText = "concatenate the output, if the entry name matches multiple entities, default=false")]
            public bool returnMultiple = false;
            [Argument(ArgumentType.AtMostOnce, HelpText = "delimiter used to seperate multiple output strings, if returnMultipe==true, default is blank space")]
            public string outDelimiter = " ";


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
           
            int count = 0;      //# of lines in the input file
            int count_nonEmptyResult = 0;   //# of non-empty entity name strings which can be used to get satoriIDs
            int count_foundID = 0;  //# of entities which really found at least one satoriID
            int count_total_IDs = 0;  //# of total satoriIDs found ( note: 1. some entity cannot find satoriID 2. some entity may correspond to >1 satoriIDs 3. we didn't do dedup, although the same satoriID can be returned for different entity name;)
            int count_notFoundID = 0;

            var lines = File.ReadLines(cmd.inTsv, Encoding.UTF8)
                //.AsParallel().AsOrdered()
                .Select(line => line.Split('\t'))
                .Select(cols =>
                {
                Console.Write("lines processed: {0}\r", ++count);
                var oriLine = string.Join("\t", cols);
                string entityName = cols[cmd.dataCol].Trim().ToLower().Replace("\"", "");
                string[] satoriInfo = { };
                if (!dict.ContainsKey(entityName))
                {
                        //normalize the entityName string to ascii
                        byte[] tempBytes;
                        tempBytes = System.Text.Encoding.GetEncoding("ISO-8859-8").GetBytes(entityName);
                        string entityNameNormalized = System.Text.Encoding.UTF8.GetString(tempBytes);
                        //Console.WriteLine("{0}\t{1}", entityName, entityNameNormalized);

                        var satoriInfoAll = EntityTools.Entitytools.getSatoriInfo(entityNameNormalized)
                                            .Select(ei => ei.Split('\t'))
                                            //.Where(fields => fields[4] == "Attactions" || fields[4] == "Place")
                                            .ToArray();

                        if (!cmd.returnMultiple)
                        {
                            //only output the first matched entity
                            satoriInfo = satoriInfoAll[0];
                            //satoriInfo = EntityTools.Entitytools.getSatoriInfo(entityName)[0].Split('\t');
                            count_total_IDs++;
                        }
                        else
                        {
                            satoriInfo = satoriInfoAll.Aggregate((working, next) =>
                            {
                                var aggregated = working;
                                for (var i = 0; i < next.Length; i++)
                                    aggregated[i] = working[i] + cmd.outDelimiter + next[i];
                                return aggregated;
                            });
                            count_total_IDs += satoriInfoAll.Count();
                        }

                        if (satoriInfo[0].Length == 0)
                            count_notFoundID++;

                        Console.WriteLine("{0}\t{1}\t{2}", satoriInfo[0], satoriInfo[4], satoriInfo[1]);

                        dict.Add(entityName, satoriInfo);
                    }
                    else
                        satoriInfo = dict[entityName];

                    if (entityName.Length > 0)
                    {
                        count_nonEmptyResult++;
                        if (satoriInfo.Length > 0 && satoriInfo[0] != Guid.Empty.ToString())
                            count_foundID++;
                        
                    }
                    string satoriInfoStr = (cmd.extraInfo) ? string.Join("\t", satoriInfo) : satoriInfo[0];
                    return oriLine + "\t" + satoriInfoStr;
                });

            string outTsv = (cmd.outTsv == "") ? Path.ChangeExtension(cmd.inTsv, "satori.tsv") : cmd.outTsv;
            File.WriteAllLines(outTsv, lines);
            Console.WriteLine("\n# of Lines:\t{0}\n# of non-empty entity names:\t{1}\n# of non-empty entity names found satoriIDs:\t{2}", count, count_nonEmptyResult, count_foundID);
            Console.WriteLine("\n# of unique entities:\t{0}\n# of unique satoriIDs found\t{1}", dict.Count(), count_total_IDs);
            Console.WriteLine("\n# of unique enities not found any satoriID\t{0}", count_notFoundID);


        }
    }
}
