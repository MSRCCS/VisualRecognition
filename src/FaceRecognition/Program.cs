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

namespace CEERecognition
{
    class Program
    {
        static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo returncodec = null;
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (var codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    returncodec = codec;
            }
            return returncodec;
        }

        // save image to jpg format
        static string SaveImageToString(Bitmap bmp)
        {
            using (var jpegImageStream = new MemoryStream())
            {
                var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                var myEncoder = System.Drawing.Imaging.Encoder.Quality;
                var myEncoderParas = new EncoderParameters(1);
                var myEncoderPara = new EncoderParameter(myEncoder, 90L);
                myEncoderParas.Param[0] = myEncoderPara;

                bmp.Save(jpegImageStream, jpgEncoder, myEncoderParas);
                return Convert.ToBase64String(jpegImageStream.ToArray());
            }
        }

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

        class ArgsExtractFolder : Args
        {
            [Argument(ArgumentType.Required, HelpText = "Input folder")]
            public string inFolder = null;
            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Feature blob name")]
            public string blob = "fc6";
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

        static void ExtractFace(ArgsExtract cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".face.tsv");

            CelebrityPredictor predictor = InitPredictor(cmd);

            Stopwatch timer = Stopwatch.StartNew();

            int count = 0;
            var lines = File.ReadLines(cmd.inTsv);
            using (StreamWriter sw = new StreamWriter(cmd.outTsv))
            {
                foreach (string line in lines)
                {
                    string[] cols = line.Split('\t');
                    string strImage = cols[cmd.imageCol];
                    using (var ms = new MemoryStream(Convert.FromBase64String(strImage)))
                    using (var bmp = new Bitmap(ms))
                    {
                        
                        FaceRectLandmarks[] faces;
                        List<Bitmap> croppedFaces;
                        predictor.DetectAndCropFaces(bmp, out faces, out croppedFaces);

                        if ((!cmd.skipMultiFace && faces.Length > 0) ||
                            (cmd.skipMultiFace && faces.Length == 1))
                        {
                            // select the largest face
                            var face = faces.Select((f, idx) => new KeyValuePair<int, FaceRectLandmarks>(idx, f))
                                    .OrderByDescending(kv => kv.Value.FaceRect.Area)
                                    .ToArray()[0];

                            Bitmap img = croppedFaces[face.Key];
                            float[] features = predictor.ExtractFeature(img, cmd.blob);
                            byte[] fea = new byte[features.Length * sizeof(float)];
                            Buffer.BlockCopy(features, 0, fea, 0, features.Length * sizeof(float));

                            var rc = face.Value.FaceRect;
                            sw.WriteLine("{0}\t{1},{2},{3},{4}\t{5}\t{6}", line,
                                    rc.Left, rc.Top, rc.Width, rc.Height,
                                    SaveImageToString(img),
                                    Convert.ToBase64String(fea));
                        }
                    }
                    Console.Write("Lines processed: {0}\r", ++count);
                }
            }

            timer.Stop();
            Console.WriteLine("Latency: {0} seconds per image", timer.Elapsed.TotalSeconds / count);

            Console.WriteLine("Column names: Name, MUrl, ImageStream, FaceRect, FaceImageStream, FaceFeature(4096D)");
        }

        static void Extract(ArgsExtract cmd)
        {
            var inData = File.ReadLines(cmd.inTsv)
                            .Select(line => line.Split('\t'));

            CelebrityPredictor predictor = InitPredictor(cmd);

            Stopwatch timer = Stopwatch.StartNew();
            int count = 0;
            var result = inData.Select(cols =>
                {
                    using (var ms = new MemoryStream(Convert.FromBase64String(cols[cmd.imageCol])))
                    using (var bmp = new Bitmap(ms))
                    {
                        float[] features = predictor.ExtractFeature(bmp, cmd.blob);
                        byte[] fea = new byte[features.Length * sizeof(float)];
                        Buffer.BlockCopy(features, 0, fea, 0, features.Length * sizeof(float));
                        cols[cmd.imageCol] = Convert.ToBase64String(fea);
                        Console.Write("{0}\r", ++count);
                        return cols;
                    }
                })
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, result);

            timer.Stop();
            Console.WriteLine("Latency: {0} seconds per image", timer.Elapsed.TotalSeconds / count);

            Console.WriteLine("Columns are kept the same, with just image stream replaced by feature");
        }
        
        static void ExtractFaceFolder(ArgsExtractFolder cmd)
        {
            CelebrityPredictor predictor = InitPredictor(cmd);

            var allFiles = Directory.GetFiles(cmd.inFolder, "*.*", SearchOption.AllDirectories)
                            .Where(file => file.ToLower().EndsWith("jpg") || file.ToLower().EndsWith("bmp"))
                            .ToArray();

            Stopwatch timer = Stopwatch.StartNew();
            int count = 0;
            using (StreamWriter sw = new StreamWriter(cmd.outTsv))
            {
                foreach (string file in allFiles)
                { 
                    byte[] rawImage = File.ReadAllBytes(file);
                    using (var ms = new MemoryStream(rawImage))
                    using (var bmp = new Bitmap(ms))
                    {
                        FaceRectLandmarks[] faces;
                        List<Bitmap> croppedFaces;
                        predictor.DetectAndCropFaces(bmp, out faces, out croppedFaces);

                        for (int i = 0; i < faces.Length; i++)
                        {
                            float[] features = predictor.ExtractFeature(croppedFaces[i], cmd.blob);
                            byte[] fea = new byte[features.Length * sizeof(float)];
                            Buffer.BlockCopy(features, 0, fea, 0, features.Length * sizeof(float));

                            var rc = faces[i].FaceRect;
                            sw.WriteLine("{0}\t{1},{2},{3},{4}\t{5}\t{6}", file.Substring(cmd.inFolder.Length + 1),
                                    rc.Left, rc.Top, rc.Width, rc.Height,
                                    SaveImageToString(croppedFaces[i]),
                                    Convert.ToBase64String(fea));
                        }
                    }

                    count++;
                    Console.WriteLine("Processed {0} out of {1} images", count, allFiles.Length);
                }
            }

            timer.Stop();
            Console.WriteLine("Latency: {0} seconds per image", timer.Elapsed.TotalSeconds / count);

            Console.WriteLine("Column names: FileName, FaceRect, FaceImageStream, FaceFeature(4096D)");
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

        static void Main(string[] args)
        {
            ParserX.AddTask<ArgsTest>(Test, "Test celebrity recognition from image folder or image files");
            ParserX.AddTask<ArgsTestTsv>(TestTsv, "Test celebrity recognition from TSV file");
            ParserX.AddTask<ArgsExtract>(ExtractFace, "Extract face feature from TSV file");
            ParserX.AddTask<ArgsExtract>(Extract, "Extract caffe feature from TSV file");
            ParserX.AddTask<ArgsExtractFolder>(ExtractFaceFolder, "Extract face feature from folder");
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
