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
using System.Drawing;
using System.Drawing.Imaging;
using CmdParser;

namespace TsvImage
{
    partial class Program
    {
        class ArgsGenomeCrop
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image stream")]
            public int imageCol = -1;
            [Argument(ArgumentType.Required, HelpText = "Column index for region json string")]
            public int regionCol = -1;
        }

        public class Region
        {
            public int image { get; set; }
            public int height { get; set; }
            public int width { get; set; }
            public int x { get; set; }
            public int y { get; set; }
            public string phrase { get; set; }
            public int id { get; set; }
        }

        public class Regions
        {
            public List<Region> regions { get; set; }
        }

        static Bitmap cropImage(Bitmap bmp, Rectangle cropArea)
        {
            return bmp.Clone(cropArea, bmp.PixelFormat);
        }

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

        static string cropImage(string image_stream, Region region)
        {
            using (var ms = new MemoryStream(Convert.FromBase64String(image_stream)))
            using (var img = new Bitmap(ms))
            {
                Rectangle rect = new Rectangle(Math.Max(0, region.x), Math.Max(0, region.y), 
                                Math.Min(img.Width, region.x + region.width) - region.x, 
                                Math.Min(img.Height, region.y + region.height) - region.y);
                using (var cropped_img = cropImage(img, rect))
                {
                    return SaveImageToString(cropped_img);
                }
            }
        }

        static void GenomeCrop(ArgsGenomeCrop cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".region.tsv");

            int count = 0, region_count = 0;
            var lines = File.ReadLines(cmd.inTsv)
                .AsParallel().AsOrdered()
                .Select(line => line.Split('\t').ToList())
                .SelectMany(cols =>
                {
                    count++;
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<Regions>(cols[cmd.regionCol]).regions;
                }, 
                (cols, region) =>
                {
                    try
                    {
                        var new_cols = new List<string>();
                        new_cols.Add(string.Format(@"{0}\{1}", region.image, region.id));
                        new_cols.Add(region.image.ToString());
                        new_cols.Add(region.id.ToString());
                        new_cols.Add(region.width.ToString());
                        new_cols.Add(region.height.ToString());
                        new_cols.Add(region.phrase.Replace('\n', ';'));
                        new_cols.Add(Newtonsoft.Json.JsonConvert.SerializeObject(region));
                        new_cols.Add(cropImage(cols[cmd.imageCol], region));
                        Console.Write("Image & Region processed: {0}, {1}\r", count, ++region_count);
                        return new_cols;
                    }
                    catch
                    {
                        Console.WriteLine("Invalid region: {0}", Newtonsoft.Json.JsonConvert.SerializeObject(region));
                        return null;
                    }
                })
                .Where(cols => cols != null)
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");
        }

        class ArgsGenomeFilter
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file")]
            public string outTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Min size of region")]
            public int minSize = 100;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Min words of phrase")]
            public int minWords = 5;
        }

        static void GenomeFilter(ArgsGenomeFilter cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".selected.tsv");

            var lines = File.ReadLines(cmd.inTsv)
                .Where((x, i) =>
                {
                    if ((i + 1) % 1000 == 0)
                        Console.Write("Lines processed: {0}\r", i + 1);
                    const int col_width = 3;
                    const int col_height = 4;
                    const int col_phrase = 5;
                    var cols = x.Split('\t');
                    if (Convert.ToInt32(cols[col_width]) >= cmd.minSize &&
                        Convert.ToInt32(cols[col_height]) >= cmd.minSize &&
                        cols[col_phrase].Split(' ').Count() >= cmd.minWords)
                        return true;
                    return false;
                });

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");
        }
    }
}