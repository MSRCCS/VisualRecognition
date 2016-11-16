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
        class ArgsCheckCoverage
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV data file, which contains the test recognition prob data")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "label map TSV file, which contains the Label-->ClassID map, a sample is covered if this map contains predicted label ")]
            public string inTsvLabel = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file for recognition summary, default: inputTSV.result.tsv")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for prob data in data file, start from 0, default=8")]
            public int dataCol = 8;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Confidence threshold, default=0")]
            public float thresh = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "The recognition prob data is Base64 encoded (floats X (# of classes)) or not ( Label1:conf1;Label2:conf2;...)")]
            public bool encodedFeature = false;

        }

        static void CheckCoverage(ArgsCheckCoverage cmd)
        {

            int count = 0;
            int count_total = 0;
            int count_total_recognized = 0;
            int count_total_correct = 0;

            var dict = File.ReadLines(cmd.inTsvLabel)
                    .Select(line => line.Split('\t'))
                    .ToDictionary(cols => cols[0], cols => Convert.ToInt32(cols[1]));


            var lines = File.ReadLines(cmd.inTsv)
                .AsParallel().AsOrdered()
                .Select(line => line.Split('\t'))
                .Select(cols => {
                    Console.Write("lines processed: {0}\r", ++count);
                    bool recognized = false;
                    bool correct = false;
                    if (cmd.encodedFeature)
                    {
                        //for base64encoded features, each column is a confidence, corresponding to classID 0,1,..., Class#-1
                        var f = ConvertFeature(cols[cmd.dataCol], false);
                        float maxV = f.Max();
                        Int32 predicated_id = Array.FindIndex(f, x => x == maxV);

                        recognized = (maxV >= cmd.thresh);
                        correct = dict.ContainsValue(predicated_id);

                    }
                    else
                    {
                        //for text features, the result format is ClassLabel:confidence
                        float maxV = float.MinValue;
                        string predicted_label = "UnknownClassLabel";
                        var results = cols[cmd.dataCol].Split(';')
                                        .Select(pair => pair.Split(':'))
                                        .ToDictionary(tuple => tuple[0].Trim(), tuple => Convert.ToDouble(tuple[1]));
                        foreach (var rlt in results)
                        {
                            if (rlt.Value > maxV)
                            {
                                maxV = (float)rlt.Value;
                                predicted_label = rlt.Key;
                            }
                        }
                        recognized = (maxV >= cmd.thresh);
                        correct = dict.ContainsKey(predicted_label);
                    }
                    Interlocked.Increment(ref count_total);
                    if (recognized)
                        Interlocked.Increment(ref count_total_recognized);
                    if (recognized && correct)
                        Interlocked.Increment(ref count_total_correct);

                    var result = Tuple.Create(recognized, correct);
                    return result;
                })
                .Select(x => string.Format("{0}\t{1}\t{2}", x.Item1, x.Item2, (x.Item1&&(!x.Item2))));
                //Item1: recognized, i.e. wether the confidence >= threshold
                //Item2: correct predict, i.e. the predicted label is covered ( in the provided label map )
                //Item3: wrong predict, i.e. the predicted label is not in the provided lable map, and the confidence >= threshold
            
            string outTsv = string.IsNullOrEmpty(cmd.outTsv)?Path.ChangeExtension(cmd.inTsv, "result.tsv"):cmd.outTsv; 
            File.WriteAllLines(outTsv, lines);
            File.AppendAllText(outTsv, string.Format("IsRecognized(conf>thresh)\tIsObject(found in labelmap)\tRecognizedAsNonObject\r\n"));
            File.AppendAllText(outTsv,
                string.Format("Total sample number: {0}\r\nrecognized (i.e. Confidence>Threshold {5}): {1}\r\nCorrect(i.e. predicted classID in label map {6}): {2}\r\nCoverage rate: {3}\r\nPrecision rate: {4}\r\nConfidence Threshold: {5}\r\n",
                count_total, count_total_recognized, count_total_correct, 
                (float)count_total_recognized / count_total, (float)count_total_correct / count_total_recognized, 
                cmd.thresh, cmd.inTsvLabel));
        }

    }
}