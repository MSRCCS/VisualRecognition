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
using System.Drawing.Drawing2D;
using IniParser;
using CmdParser;
using CEERecognition;
using TsvTool.Utility;

namespace TsvImage
{
    partial class Program
    {
        // calculate backgroun color by checking the bounding region
        static Color CalcBoundingBandColor(Bitmap img)
        {
            const int band = 3;
            if (img.Width <= 2 * band || img.Height <= 2 * band)
                return img.GetPixel(0, 0);

            var centralRect = new Rectangle(0, 0, img.Width, img.Height);
            centralRect.Inflate(-2 * band, -2 * band);

            float r, g, b;
            r = g = b = 0;
            for (int x = 0; x < img.Width; x++)
            {
                for (int y = 0; y < img.Height; y++)
                {
                    if (centralRect.Contains(x, y))
                        continue;

                    var c = img.GetPixel(x, y);
                    r += c.R;
                    g += c.G;
                    b += c.B;
                }
            }
            int total = img.Width * img.Height - centralRect.Width * centralRect.Height;
            r /= total;
            g /= total;
            b /= total;
            return Color.FromArgb((int)r, (int)g, (int)b);
        }

        static Bitmap DownsizeImageToCenter(Bitmap img, float scale)
        {
            if (scale < 1.01 && scale > 0.99)
                return img;

            int w = (int)(scale * img.Width), h = (int)(scale * img.Height);

            var destRect = new Rectangle(0, 0, w, h);
            destRect.Offset((img.Width - w) / 2, (img.Height - h) / 2);
            
            var newImage = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
            newImage.SetResolution(img.HorizontalResolution, img.VerticalResolution);

            using (var g = Graphics.FromImage(newImage))
            {
                g.Clear(CalcBoundingBandColor(img));
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, destRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
            }

            return newImage;
        }

        class ArgsImageScale
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace InTsv .ext with .scale.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image")]
            public int image = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Down sampling scales (no larger than 1), e.g. 1,0.5,0.25 (default: 1,0.5,0.25")]
            public string scale = "1,0.5,0.25";
        }

        static void ImageScale(ArgsImageScale cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".scale.tsv");
            var scales = cmd.scale.Split(',').Select(x => Convert.ToSingle(x)).ToArray();

            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .AsParallel().AsOrdered()
                .Select(line => line.Split('\t').ToArray())
                .Select(cols => 
                {
                    var imageData = Convert.FromBase64String(cols[cmd.image]);
                    var ms = new MemoryStream(imageData);
                    Bitmap bmp = new Bitmap(ms);
                    return new { cols = cols, bmp = bmp };
                })
                .Where(x => x.bmp.Width > 5 && x.bmp.Height > 5)
                .SelectMany(x =>
                {
                    var multi_imgs = scales
                        .Select(s =>
                        {
                            var img = DownsizeImageToCenter(x.bmp, s);
                            var img_str = Convert.ToBase64String(ImageUtility.SaveImageToJpegInBuffer(img));
                            var new_cols = x.cols.ToList();
                            new_cols[cmd.image] = img_str;
                            return new_cols;
                        })
                        .ToList();
                    return multi_imgs;
                })
                .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");
        }

    }
}