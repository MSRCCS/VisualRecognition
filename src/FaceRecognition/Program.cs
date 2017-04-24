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

        class ArgsLFWCrop
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Column index for image stream")]
            public int imageCol = 2;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace file .ext with .face.tsv")]
            public string outTsv = null;
        }

        static void LFWCrop(ArgsLFWCrop cmd)
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
                        int cx = bmp.Width / 2;
                        int cy = bmp.Height / 2;

                        FaceRectLandmarks[] faces;
                        List<Bitmap> croppedFaces;
                        faceDetector.DetectAndCropFaces(bmp, out faces, out croppedFaces);
                        return new { cols, faces, croppedFaces, cx, cy };
                    }
                })
                .Where(x => x.faces.Length > 0)
                .Select(x =>
                {
                    var centerFace = x.faces
                        .Select((face, idx) => new {face, idx})
                        .OrderBy(f =>
                        {
                            float centerX = (float)(f.face.FaceRect.Left) + f.face.FaceRect.Width / 2.0f;
                            float centerY = (float)(f.face.FaceRect.Top) + f.face.FaceRect.Height / 2.0f;
                            return Math.Sqrt((centerX - x.cx) * (centerX - x.cx) 
                                    + (centerY - x.cy) * (centerY - x.cy));
                        })
                        .First();

                    if (centerFace.idx > 0)
                        Console.WriteLine("\n{0} face detected: {1}", x.faces.Count(), x.cols[0]);
                    var image_buffer = TsvTool.Utility.ImageUtility.SaveImageToJpegInBuffer(x.croppedFaces[centerFace.idx]);
                    x.cols[cmd.imageCol] = Convert.ToBase64String(image_buffer);
                    return x.cols;
                })
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");
        }

        static void Main(string[] args)
        {
            ParserX.AddTask<ArgsCropFace>(CropFace, "Detect and crop face from TSV file");
            ParserX.AddTask<ArgsLFWCrop>(LFWCrop, "Crop faces in LFW");
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
