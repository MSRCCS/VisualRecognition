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
            [Argument(ArgumentType.Required, HelpText = "Input TSV data file, which contains the recognition prob data")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "label map TSV file, which contains the Label-->ClassID map (if labels are not in the input data file)")]
            public string inTsvLabel = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file for recognition summary")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for classID in data file (or in label map file if provided), start from 0")]
            public int labelCol = 1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for prob data in data file, start from 0")]
            public int dataCol = 8;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Confidence threshold")]
            public float thresh = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "The recognition prob data is Base64 encoded (floats X (# of classes)) or not ( Label1:conf1;Label2:conf2;...)")]
            public bool encodedFeature = false;

        }

        static void ParseResult(ArgsParseResult cmd)
        {
            
            int count = 0;
            int group = 0;

            int count_total = 0;
            int count_total_recognized = 0;
            int count_total_correct = 0; 

            var dict = File.ReadLines(cmd.inTsvLabel)
                    .Select(line => line.Split('\t'))
                    .ToDictionary(cols => cols[0], cols => Convert.ToInt32(cols[1]));
                   

            var lines = File.ReadLines(cmd.inTsv)
                .AsParallel().AsOrdered()
                .Select(line => line.Split('\t'))
                //.Where (cols => cols.Length > Math.Max(cmd.dataCol, cmd.labelCol))
                //col0:Satori ID, col1:classLabel, ... , col8:probData, ...
                .Select(cols => {
                    double maxV = -1.0;
                    Int32 id = -1;

                    if (cmd.encodedFeature)
                    {
                        var f = ConvertFeature(cols[cmd.dataCol], false);
                        maxV = f.Max();
                        id = Array.FindIndex(f, x => x == maxV);
                    }
                    else
                    {
                        var results = cols[cmd.dataCol].Split(';')
                                        .Select(pair => pair.Split(':'))
                                        .ToDictionary(tuple => tuple[0].Trim(), tuple => Convert.ToDouble(tuple[1]));
                        foreach (var rlt in results)
                        {
                            if (rlt.Value > maxV) 
                            {
                                maxV = rlt.Value;
                                id = dict[rlt.Key];
                            }
                        }
                        
                    }
                    //Int32 clsIDLabel = (cmd.labelCol != -1) ? dict[cols[cmd.labelCol]] : -1;
                    Int32 clsIDLabel = dict[cols[cmd.labelCol]];
                    Console.Write("lines processed: {0}\r", ++count);
                    return new
                    {
                        clsID = clsIDLabel,
                        //sID = (cmd.labelCol != -1) ? cols[cmd.labelCol]:"TargetClass",
                        sID = cols[cmd.labelCol],
                        argmax = Tuple.Create(maxV, id),
                        recognized = (maxV >= cmd.thresh),
                        correct = (clsIDLabel == id)                        
                    };
                });

            var groups = lines.GroupBy(x => x.clsID)
                .Select(g =>
                {
                    Console.Write("groups processed: {0}\r", ++group);
                    
                    var gEnum = g.AsEnumerable();
                    int total = gEnum.Count();
                    int recognized = gEnum.Count(x => x.recognized);
                    int correct = gEnum.Count(x => x.recognized && x.correct);
                    
                    Interlocked.Add(ref count_total, total);
                    Interlocked.Add(ref count_total_recognized, recognized);
                    Interlocked.Add(ref count_total_correct, correct);
                    
                    float accuracy = (float)correct / total;
                    float precision = recognized!=0?((float)correct / recognized):(1.0f);
                    float recall = (float)recognized / total;

                    var result = Tuple.Create(g.Key, g.AsEnumerable().First().sID, accuracy, precision, recall, correct, recognized, total);
                    return result;
                })
                .OrderByDescending(x => x.Item3).ThenByDescending(x => x.Rest.Item1)
                .Select(x => string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}", x.Item1, x.Item2, x.Item3, x.Item4, x.Item5, x.Item6, x.Item7, x.Rest.Item1));

            Trace.Assert(count_total == count);
            File.WriteAllText(cmd.outTsv, string.Format("clsID\tclsLabel\tcorrect/total\tcorrect/recognized\trecognized/total\tcorrect\trecognized\ttotal\r\n"));
            File.AppendAllLines(cmd.outTsv, groups);
            File.AppendAllText(cmd.outTsv, 
                string.Format("Total sample number: {0}\r\nrecognized: {1}\r\nCorrect: {2}\r\nRecall rate: {3}\r\nPrecision rate: {4}\r\nConfidence Threshold: {5}\r\n", 
                count_total, count_total_recognized, count_total_correct, (float)count_total_recognized / count_total, (float)count_total_correct / count_total_recognized, cmd.thresh));
            ////append a column to image data file
            //var imgLines = File.ReadLines(cmd.inTsvLabel)
            //    .Zip(lines, (first, second) => first + "\t" + second.correct);
            //File.WriteAllLines(cmd.inTsv, imgLines);
            
        }

    }
}