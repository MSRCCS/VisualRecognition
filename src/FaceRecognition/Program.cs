using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using CmdParser;
using CEERecognition;
using FaceSdk;
using TsvTool.Utility;

namespace CEERecognition
{
    class Program
    {
        // args for predictor initialization
        class Args
        {
            // To simplify the cmdline, caffe model is described in a model.cfg file in the same model folder.
            // Its format is as follows:
            //      proto: protofile.prototxt
            //      model: modelfile.caffemodel
            //      mean: imagemean.binaryproto
            //      labelmap: train.labelmap
            //      entityinfo: EntityInfo.tsv
            [Argument(ArgumentType.Required, HelpText = "Model folder that contains model.cfg file")]
            public string modelDir = ".";
            [Argument(ArgumentType.AtMostOnce, HelpText = "Gpu Id (default: 0, -1 for cpu)")]
            public int gpu = 0;
        }

        class ArgsTest: Args
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "Top k result")]
            public int topk = 5;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Recognition confidence threshold")]
            public float conf = 0.9f;
            [Argument(ArgumentType.Required, HelpText = "Images list file")]
            public string imgs = null;
        }

        class ArgsTestTsv : Args
        {
            [Argument(ArgumentType.AtMostOnce, HelpText = "Top k result")]
            public int topk = 5;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Recognition confidence threshold")]
            public float conf = 0.9f;
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image stream")]
            public int imageCol = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for image key")]
            public int keyCol = -1;
        }
        
        class ArgsExtract : Args
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image stream")]
            public int imageCol = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Feature blob name")]
            public string blob = "fc6";
            [Argument(ArgumentType.AtMostOnce, HelpText = "Skip multiple face image (default: yes)")]
            public bool skipMultiFace = true;
        }

        class ArgsLDA
        {
            [Argument(ArgumentType.Required, HelpText = "LDA model file")]
            public string ldaModel = null;
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image stream")]
            public int featureCol = -1;
        }

        static CelebrityPredictor InitPredictor(Args cmd)
        {
            string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string faceModelFile = Path.Combine(exeDir, "ProductCascadeJDA27ptsWithLbf.mdl");

            string modelCfg = Path.Combine(cmd.modelDir, "model.cfg");
            if (!File.Exists(modelCfg))
            {
                throw new FileNotFoundException("Cannot find model.cfg in folder " + cmd.modelDir);
            }
            var modelDict = File.ReadLines(modelCfg)
                .Where(line => line.Trim().StartsWith("#") == false)
                .Select(line => line.Split(':'))
                .ToDictionary(cols => cols[0].Trim(), cols => cols[1].Trim(), StringComparer.OrdinalIgnoreCase);

            string protoFile = Path.Combine(cmd.modelDir, modelDict["proto"]);
            string modelFile = Path.Combine(cmd.modelDir, modelDict["model"]);
            string labelmapFile = Path.Combine(cmd.modelDir, modelDict["labelmap"]);
            string meanFile = Path.Combine(cmd.modelDir, modelDict["mean"]);
            string sidMapping = null;
            if (modelDict.ContainsKey("entityinfo") && !string.IsNullOrEmpty(modelDict["entityinfo"]))
                sidMapping = Path.Combine(cmd.modelDir, modelDict["entityinfo"]);

            System.Diagnostics.Debug.Assert(File.Exists(faceModelFile));
            System.Diagnostics.Debug.Assert(File.Exists(protoFile));

            CelebrityPredictor predictor = new CelebrityPredictor();
            predictor.Init(faceModelFile, protoFile, modelFile, meanFile, labelmapFile, sidMapping, cmd.gpu);

            return predictor;
        }

        static void Test(ArgsTest cmd)
        {
            var imgs = Directory.Exists(cmd.imgs)
                ? Directory.GetFiles(cmd.imgs, "*.jpg") // read images from directory
                : cmd.imgs.EndsWith(".jpg")
                    ? new[] { cmd.imgs } // single image
                    : File.ReadAllLines(cmd.imgs); // read images from list file
            
            CelebrityPredictor predictor = InitPredictor(cmd);

            Stopwatch timer = Stopwatch.StartNew();

            foreach (var img in imgs)
            {
                byte[] imageStream = File.ReadAllBytes(img);
                using (var ms = new MemoryStream(imageStream))
                using (var bmp = new Bitmap(ms))
                {
                    var recogResult = predictor.Predict(bmp, 20, cmd.conf);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("==== Image: {0} ====", img);
                    Console.ResetColor();
                    // print out result
                    foreach (var face in recogResult)
                    {
                        Console.WriteLine("------ Face: {0}, {1}, {2}, {3} ------", face.Rect.X, face.Rect.Y, face.Rect.Width, face.Rect.Height);
                        var resString = string.Join("\n",
                             face.RecogizedAs.Select(r => r.EntityName + '\t' + r.Confidence + '\t' + r.EntityId + '\t' + r.EntityMUrl)
                                         .ToArray());
                        Console.WriteLine(resString);
                    }
                }
            }

            timer.Stop();
            Console.WriteLine("Latency: {0} seconds per image", timer.Elapsed.TotalSeconds / imgs.Length);            
        }

        static void TestTsv(ArgsTestTsv cmd)
        {
            var in_data = File.ReadLines(cmd.inTsv)
                    .Select(line => line.Split('\t'));

            CelebrityPredictor predictor = InitPredictor(cmd);

            Stopwatch timer = Stopwatch.StartNew();

            int line_count = 0;
            int face_count = 0;
            using (var sw = new StreamWriter(cmd.outTsv))
            foreach (var cols in in_data)
            {
                byte[] imageStream = Convert.FromBase64String(cols[cmd.imageCol]);
                using (var ms = new MemoryStream(imageStream))
                using (var bmp = new Bitmap(ms))
                {
                    var recogResult = predictor.Predict(bmp, cmd.topk, cmd.conf);

                    var faces = recogResult.Select((face, i) =>
                    {
                        // ImageKey, FaceId, FaceRect, RecogResult
                        return string.Format("{0}\tFaceId_{1}\t{2}\t{3}",
                            cols[cmd.keyCol],
                            i,
                            string.Format("{0}, {1}, {2}, {3}", face.Rect.X, face.Rect.Y, face.Rect.Width, face.Rect.Height),
                            string.Join(";", face.RecogizedAs.Select(r => r.EntityId + ":" + r.Confidence.ToString()))
                            );
                    });

                    foreach (var f in faces)
                        sw.WriteLine(f);

                    line_count++;
                    face_count += faces.Count();
                }
                Console.Write("Images processed: {0}, faces detected: {1}\r", line_count, face_count);
            }

            timer.Stop();
            Console.WriteLine("\nLatency: {0} seconds per image", timer.Elapsed.TotalSeconds / line_count);
        }

        static void Extract(ArgsExtract cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".face." + cmd.blob + ".tsv");

            CelebrityPredictor predictor = InitPredictor(cmd);

            Stopwatch timer = Stopwatch.StartNew();

            int count = 0;
            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .Select(line => line.Split('\t').ToList())
                .Select(cols => 
                {
                    count++;
                    using (var ms = new MemoryStream(Convert.FromBase64String(cols[cmd.imageCol])))
                    using (var bmp = new Bitmap(ms))
                    {
                        FaceRectLandmarks[] faces;
                        List<Bitmap> croppedFaces;
                        predictor.DetectAndCropFaces(bmp, out faces, out croppedFaces);
                        return new {cols = cols, faces = faces, croppedFaces = croppedFaces};
                    }
                })
                .Where(x => x.faces.Length > 0) // no face
                .Where(x => !(cmd.skipMultiFace && x.faces.Length > 1)) // skip multiple face if needed
                .Select(x => 
                {
                    // select the largest face
                    var face = x.faces.Select(f => f.FaceRect)
                        .Select((f, idx) => Tuple.Create(f, idx))
                            .OrderByDescending(tp => tp.Item1.Area)
                            .First();

                    Bitmap img = x.croppedFaces[face.Item2];
                    float[] features = predictor.ExtractFeature(img, cmd.blob);

                    byte[] fea = new byte[features.Length * sizeof(float)];
                    Buffer.BlockCopy(features, 0, fea, 0, features.Length * sizeof(float));

                    x.cols.RemoveAt(cmd.imageCol);
                    var rc = face.Item1;
                    x.cols.Add(string.Format("{0},{1},{2},{3}", rc.Left, rc.Top, rc.Width, rc.Height));
                    var img_buf = TsvTool.Utility.ImageUtility.SaveImageToJpegInBuffer(img);
                    x.cols.Add(Convert.ToBase64String(img_buf));
                    x.cols.Add(Convert.ToBase64String(fea));

                    img.Dispose();

                    return x.cols;
                })
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");

            timer.Stop();
            Console.WriteLine("Latency: {0} seconds per image", timer.Elapsed.TotalSeconds / count);

            Console.WriteLine("Column names: Name, MUrl, ImageStream, FaceRect, FaceImageStream, FaceFeature(4096D)");
        }

        static void Lda(ArgsLDA cmd)
        {
            var inData = File.ReadLines(cmd.inTsv)
                        .Select(line => line.Split('\t'));

            linearProjection lp = new linearProjection();
            lp.readRightMatrixBinaryRowNum(cmd.ldaModel, 512);
            Console.WriteLine("Lda model loaded");

            Console.WriteLine("Applying LDA transformation...");
            var ldaResult = inData.AsParallel().AsOrdered()
                    .Select((cols, idx) =>
                    {
                        var feature = lp.rightMatrixProduct(Convert.FromBase64String(cols[cmd.featureCol]), 512);
                        cols[cmd.featureCol] = Convert.ToBase64String(feature);
                        Console.Write("{0}\r", idx + 1);
                        return cols;
                    })
                    .Select(cols => string.Join("\t", cols));

            Console.WriteLine("Saving data...");
            File.WriteAllLines(cmd.outTsv, ldaResult);
            Console.WriteLine();
            Console.WriteLine("Done!");
        }

        class ArgsCropFace
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image stream")]
            public int imageCol = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace file .ext with .face.tsv")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Max image size for cropped face (default: 0 for no resize)")]
            public int maxSize = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Skip multiple face image (default: yes)")]
            public bool skipMultiFace = true;
        }

        static void CropFace(ArgsCropFace cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".face.tsv");

            string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string faceModelFile = Path.Combine(exeDir, "ProductCascadeJDA27ptsWithLbf.mdl");
            FaceDetector faceDetector = new FaceDetector(faceModelFile);

            Stopwatch timer = Stopwatch.StartNew();

            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .AsParallel().AsOrdered()
                .Select(line => line.Split('\t').ToList())
                .Select(cols =>
                {
                    // detect face
                    using (var ms = new MemoryStream(Convert.FromBase64String(cols[cmd.imageCol])))
                    using (var bmp = new Bitmap(ms))
                    {
                        FaceRectLandmarks[] faces;
                        List<Bitmap> croppedFaces;
                        faceDetector.DetectAndCropFaces(bmp, out faces, out croppedFaces);
                        croppedFaces = croppedFaces.Select(img =>
                            {
                                // resize if needed
                                if (cmd.maxSize > 0)
                                {
                                    var resized_img = TsvTool.Utility.ImageUtility.DownsizeImage(img, cmd.maxSize);
                                    if (!Object.ReferenceEquals(resized_img, img))
                                        img.Dispose();
                                    img = resized_img;
                                }
                                return img;
                            })
                            .ToList();

                        cols.RemoveAt(cmd.imageCol);
                        return new { cols = cols, faces = faces, croppedFaces = croppedFaces };
                    }
                })
                .Where(x => x.faces.Length > 0)
                .Where(x => !(cmd.skipMultiFace && x.faces.Length > 1))
                .SelectMany(x =>
                {
                    var face_lines = x.faces.Select(f => f.FaceRect)
                        .Select((f, idx) =>
                        {
                            var image_buffer = TsvTool.Utility.ImageUtility.SaveImageToJpegInBuffer(x.croppedFaces[idx]);
                            x.croppedFaces[idx].Dispose();

                            // generate result
                            var append = new string[] 
                            {
                                "Face_Id" + idx,
                                string.Format("{0},{1},{2},{3}", f.Left, f.Top, f.Width, f.Height),
                                Convert.ToBase64String(image_buffer)
                            };

                            return string.Join("\t", x.cols) + "\t" + string.Join("\t", append);
                        });
                    return face_lines;
                });

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");

            Console.WriteLine("OutTsv format: OriginalLine with ImageStream Removed, FaceId, FaceRect, FaceImageStream");
        }

        static void Main(string[] args)
        {
            ParserX.AddTask<ArgsTest>(Test, "Test celebrity recognition from image folder or image files");
            ParserX.AddTask<ArgsTestTsv>(TestTsv, "Test celebrity recognition from TSV file");
            ParserX.AddTask<ArgsExtract>(Extract, "Extract face feature from TSV file");
            ParserX.AddTask<ArgsCropFace>(CropFace, "Detect and crop face from TSV file");
            ParserX.AddTask<ArgsLDA>(Lda, "Apply LDA transformation");
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
