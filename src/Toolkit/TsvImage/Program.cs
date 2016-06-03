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
using CEERecognition;

namespace TsvImage
{
    partial class Program
    {
        class ArgsRank
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for feature")]
            public int featureCol = 3;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for label string")]
            public int labelCol = 0;
            [Argument(ArgumentType.Required, HelpText = "Query label string")]
            public string query = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Positive prefix")]
            public string positivePrefix = null;
        }

        class ArgsEval
        {
            [Argument(ArgumentType.Required, HelpText = "Model TSV file")]
            public string modelTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Test TSV file")]
            public string testTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output score file")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for feature (default: 3)")]
            public int featureCol = 3;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for label string (default: 0)")]
            public int labelCol = 0;
        }

        class ArgsConfusion
        {
            [Argument(ArgumentType.Required, HelpText = "Input score file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output confusion matrix")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Confidence threshold")]
            public float conf = 0f;
        }

        static float[] ConvertFeature(string base64Str, bool normalize = true)
        {
            var raw = Convert.FromBase64String(base64Str);
            float[] feature = new float[raw.Count() / sizeof(float)];
            Buffer.BlockCopy(raw, 0, feature, 0, raw.Count());
            if (normalize)
                Distance.Normalize(ref feature);
            return feature;
        }

        static void Rank(ArgsRank cmd)
        {
            var data = File.ReadLines(cmd.inTsv)
                        .Select(line => line.Split('\t'))
                        .Select(_cols => new {cols = _cols, feature = ConvertFeature(_cols[cmd.featureCol])});

            var query = data.Where(d => d.cols[cmd.labelCol].StartsWith(cmd.query));
            if (query.Count() == 0)
            {
                Console.WriteLine("Query not found!");
                return;
            }
            else if (query.Count() > 1)
            {
                Console.WriteLine("More than one query found. Cannot process!");
                return;
            }
            
            var queryFeature = ConvertFeature(query.First().cols[cmd.featureCol]);
            var rankedData = data.Select(d => new { cols = d.cols, score = Distance.CosineSimilarity(queryFeature, d.feature) })
                .OrderByDescending(d => d.score);

            File.WriteAllLines(cmd.outTsv, rankedData
                    .Select(d => string.Format("{0}\t{1}", string.Join("\t", d.cols), d.score))
                );

            if (File.Exists(Path.ChangeExtension(cmd.inTsv, "def")))
            {
                var def = File.ReadLines(Path.ChangeExtension(cmd.inTsv, "def")).First() 
                            + "; score:" + query.First().cols.Length.ToString();
                File.WriteAllText(Path.ChangeExtension(cmd.outTsv, "def"), def);
            }
            if (File.Exists(Path.ChangeExtension(cmd.outTsv, "idx")))
                File.Delete(Path.ChangeExtension(cmd.outTsv, "idx"));

            Console.WriteLine("Ranking finished!");
        }

        static void Eval(ArgsEval cmd)
        {
            var model = File.ReadLines(cmd.modelTsv)
                        .Select(line => line.Split('\t'))
                        .Select(_cols => new { file = _cols[cmd.labelCol], name = _cols[cmd.labelCol].Split('\\')[0], feature = ConvertFeature(_cols[cmd.featureCol]) })
                        .ToArray();

            var test = File.ReadLines(cmd.testTsv)
                        .Select(line => line.Split('\t'))
                        .Select(_cols => new { file = _cols[cmd.labelCol], name = _cols[cmd.labelCol].Split('\\')[0], feature = ConvertFeature(_cols[cmd.featureCol]) })
                        .ToArray();

            int total = test.Count();
            Console.WriteLine("Total test faces: {0}", total);
            int count = 0;
            var result = test.AsParallel().Select( (q, i) => 
                {
                    var recogResult = model.Select(d => 
                                new { file = q.file,
                                      name = q.name,
                                      recog = d.name,
                                      score = Distance.DotProduct(q.feature, d.feature) 
                                    })
                       .OrderByDescending(d => d.score).First();
                    Interlocked.Increment(ref count);
                    Console.Write("Processed: {0}, {1:0.0}%\r", count, (float)count * 100 / total);
                    return recogResult;
                }).ToArray();
            Console.WriteLine();
            int errs = result.Where(q => q.name != q.recog).Count();
            Console.WriteLine("Error rate: {0}/{1}={2}%", errs, result.Count(), (float)errs / result.Count());
            File.WriteAllLines(cmd.outTsv, 
                result.Select(q => string.Format("{0}\t{1}\t{2}\t{3}", q.name, q.file, q.recog, q.score)));
        }

        static void Confusion(ArgsConfusion cmd)
        {
            if (cmd.outTsv == null)
            {
                cmd.outTsv = string.Format("{0}-Th{1}.txt", Path.GetFileNameWithoutExtension(cmd.inTsv), cmd.conf.ToString("0.00").Replace('.', '_'), Path.GetExtension(cmd.inTsv));
                Console.WriteLine("Output file: {0}", cmd.outTsv);
            }
            var scores = File.ReadLines(cmd.inTsv)
                    .Select(line => line.Split('\t'))
                    .Select(c => new { name = c[0], file = c[1], recog = c[2], score = Convert.ToSingle(c[3]) })
                    .ToArray();
            var nameDict = scores.Select(d => d.name).Distinct().OrderBy(d => d)
                            .Select((name, i) => new KeyValuePair<string, int>(name, i))
                            .ToDictionary(kv => kv.Key, kv => kv.Value);
            int N = nameDict.Count();
            var mat = new int[N, N + 1];
            for (int i = 0; i < N; i++)
                for (int j = 0; j < N + 1; j++)
                    mat[i, j] = 0;

            foreach (var s in scores)
            {
                int i = nameDict[s.name];
                int j = nameDict[s.recog];
                if (s.score < cmd.conf)
                    j = N;
                mat[i, j]++;
            }

            using (var sw = new StreamWriter(cmd.outTsv))
            {
                sw.WriteLine(N);
                sw.WriteLine(N + 1);
                for (int i = 0; i < N; i++)
                {
                    for (int j = 0; j < N + 1; j++)
                        sw.Write("{0}\t", mat[i, j]);
                    sw.WriteLine();
                }
            }

            int recogs = scores.Where(q => (q.score >= cmd.conf)).Count();
            int errs = scores.Where(q => (q.name != q.recog) && (q.score >= cmd.conf)).Count();
            Console.WriteLine("Detection rate: {0}/{1}={2:0.000}", recogs, scores.Count(), (float)recogs / scores.Count());
            Console.WriteLine("Error rate: {0}/{1}={2:0.0000}", errs, recogs, (float)errs / recogs);
        }

        static private void update_ngram(Dictionary<String, int> ngram, String str)
        {
            if (ngram.ContainsKey(str))
            {
                ngram[str] += 1;
            }
            else
            {
                ngram.Add(str, 1);
            }
        }

        static void Main(string[] args)
        {
            ParserX.AddTask<ArgsRank>(Rank, "Rank faces in TSV file");
            ParserX.AddTask<ArgsEval>(Eval, "Eval recognition using model and test TSV files");
            ParserX.AddTask<ArgsConfusion>(Confusion, "Generate confusion matrix");
            ParserX.AddTask<ArgsNGram>(NGram, "Generate ngram multi labels");
            ParserX.AddTask<ArgsNGram2Id>(NGram2Id, "Convert ngram string to ID");
            ParserX.AddTask<ArgsNGramClean>(NGramClean, "NGram table clean");
            ParserX.AddTask<ArgsNGramEval>(NGramEval, "NGram evaluation");
            ParserX.AddTask<ArgsVocab>(Vocab, "Vocab for concrete nouns");
            ParserX.AddTask<ArgsBrqWithVocab>(BrqWithVocab, "Select Brq with vocab phrases");
            ParserX.AddTask<ArgsGenomeCrop>(GenomeCrop, "Crop visual genome images");
            ParserX.AddTask<ArgsGenomeFilter>(GenomeFilter, "Filter visual genome regions");
            ParserX.AddTask<ArgsLfwEval>(LfwEval, "Eval LFW using face feature file and pair list file");
            ParserX.AddTask<ArgsCeleb>(Celeb, "Celebrity selection");
            ParserX.AddTask<ArgsCelebRemove>(CelebRemove, "Celebrity removal for ancient people");
            ParserX.AddTask<ArgsCeleb2Phase>(Celeb2Phase, "Divide celebrities into multiple phrases");
            ParserX.AddTask<ArgsClassVariance>(ClassVariance, "Calculate class variance (for data clean)");
            ParserX.AddTask<ArgsClassMeanVar>(ClassMeanVar, "Calculate class mean and variance");
            ParserX.AddTask<ArgsParseResult>(ParseResult, "Parse Caffe evaluation result for accuracy per class");
            ParserX.AddTask<ArgsViewCheck>(ViewCheck, "Data repo view check");
            ParserX.AddTask<ArgsView2Data>(View2Data, "Data repo view to data");
            ParserX.AddTask<ArgsWrongCeleb>(WrongCeleb, "Detect wrong celebs based on the prediction result");
            ParserX.AddTask<ArgsImageScale>(ImageScale, "Generate multiple images by down scaling");
            ParserX.AddTask<ArgsCheckCoverage>(CheckCoverage, "Parse Caffe evaluation result for false alarm or detection rate");
            if (ParserX.ParseArgumentsWithUsage(args))
            {
                Stopwatch timer = Stopwatch.StartNew();
                ParserX.RunTask();
                timer.Stop();
                Console.WriteLine("Time used: {0}", timer.Elapsed);
            }
        }

    }

}
