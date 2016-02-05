using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Web;
using System.Diagnostics;
using CmdParser;

namespace IRC.DataProcess
{
    class Program
    {
        class Arguments
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;

            [Argument(ArgumentType.Required, HelpText = "Output TSV file")]
            public string outTsv = null;

            [Argument(ArgumentType.Required, HelpText = "Column index for image url for downloading")]
            public int murlCol = -1;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Resize image after downloading (default: 0, no resize)")]
            public int resize = 0;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Max number of downloads (default: -1 for all)")]
            public int maxDownloads = Int32.MaxValue;

            [Argument(ArgumentType.AtMostOnce, HelpText = "Discard broken images? (default: true)")]
            public bool discardBroken = true;
        }

        private static async Task<byte[]> DownloadWebImageToByteArray(string uri)
        {
            byte[] data = null;
            try
            {
                using(var httpClient = new HttpClient())
                {
                    httpClient.Timeout = new TimeSpan(0, 0, 15);
                    byte[] buffer = await httpClient.GetByteArrayAsync(uri);

                    // check if it is a valid image file
                    using (Stream ms = new MemoryStream(buffer))
                    using (Bitmap img = (Bitmap)Bitmap.FromStream(ms))
                    { }
                    data = buffer;
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(ex.ToString());
            }

            return data;
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

        static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var g = Graphics.FromImage(destImage))
            {
                //g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                //g.SmoothingMode = SmoothingMode.HighQuality;
                //g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                //using (var wrapMode = new ImageAttributes())
                //{
                //    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                //    g.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                //}
                g.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
            }

            return destImage;
        }

        static byte[] CovertToJpegFormat(byte[] imgData, int maxImageSize, Int64 quality = 90L)
        {
            using (Stream mr = new MemoryStream(imgData))
            using (Bitmap streamImg = (Bitmap)Bitmap.FromStream(mr))
            {
                Bitmap img = streamImg;
                if (maxImageSize > 0)
                {
                    int w = img.Width, h = img.Height;
                    if (w > maxImageSize || h > maxImageSize)
                    {
                        if (w > h)
                        {
                            w = maxImageSize;
                            h = img.Height * maxImageSize / img.Width;
                        }
                        else
                        {
                            w = img.Width * maxImageSize / img.Height;
                            h = maxImageSize;
                        }
                        img = ResizeImage(streamImg, w, h);
                    }
                }

                if (img.PixelFormat != PixelFormat.Format24bppRgb)
                    img = img.Clone(new Rectangle(0, 0, img.Width, img.Height), PixelFormat.Format24bppRgb);

                // if img not changed (neither resized nor cloned), just return the original imgData;
                if (img == streamImg)
                    return imgData;

                // save image to jpg format
                var jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                var myEncoder = System.Drawing.Imaging.Encoder.Quality;
                var myEncoderParas = new EncoderParameters(1);
                var myEncoderPara = new EncoderParameter(myEncoder, quality);
                myEncoderParas.Param[0] = myEncoderPara;

                using (var mw = new MemoryStream())
                {
                    img.Save(mw, jpgEncoder, myEncoderParas);
                    return mw.ToArray();
                }
            }
        }

        static async Task<string> DownloadOneImage(string murl, int maxImageSize)
        {
            string strImageStream = string.Empty;

            try
            {
                if (murl.StartsWith("http"))
                {
                    byte[] data = await DownloadWebImageToByteArray(murl);
                    if (data != null)
                    {
                        data = CovertToJpegFormat(data, maxImageSize, 90L);
                        strImageStream = Convert.ToBase64String(data);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nException for: {0}", murl);
                Console.WriteLine("Exception message: {0}", ex.Message);
            }
            return strImageStream;
        }

        // download images, and append one column for base64 encoded image stream
        static void DownloadImages(Arguments cmd)
        {
            int count = 0;
            int count_started = 0;
            int count_downloaded = 0;

            Stopwatch timer = Stopwatch.StartNew();

            var lines = File.ReadLines(cmd.inTsv)
                    .AsParallel().AsOrdered()
                    .Select(line => line.Split('\t').ToList())
                    .Select(cols => 
                    {
                        count_started++;
                        var imageStream = DownloadOneImage(cols[cmd.murlCol], cmd.resize).Result;
                        cols.Add(imageStream);
                        if (!string.IsNullOrEmpty(imageStream))
                            count_downloaded++;
                        Console.Write("Lines started: {0}, processed: {1}, downloaded: {2}\r", count_started, ++count, count_downloaded);
                        return cols;
                    })
                    .Where(cols => !(cmd.discardBroken && string.IsNullOrEmpty(cols.Last())))
                    //.Take(cmd.maxDownloads)
                    .Select(cols => string.Join("\t", cols));

            File.WriteAllLines(cmd.outTsv, lines);

            timer.Stop();
            Console.WriteLine("\nDownloaded {0} images from {1} lines in {2:0.0} seconds", count_downloaded, count, timer.Elapsed.TotalSeconds);

        }

        static void Main(string[] args)
        {
            var cmd = new Arguments();
            if (!Parser.ParseArgumentsWithUsage(args, cmd))
                return;

            DownloadImages(cmd);
        }
    }
}
