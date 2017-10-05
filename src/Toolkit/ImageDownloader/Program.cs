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
using TsvTool.Utility;

namespace ImageAcquisition
{
    partial class Program
    {
        class ArgsCrawl
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace inTsv .ext with .image.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for image url for downloading")]
            public int murlCol = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Resize image after downloading (default: 0, no resize)")]
            public int resize = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Max number of downloads (default: 0 for all)")]
            public int maxDownloads = 0;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Discard broken images? (default: true)")]
            public bool discardBroken = true;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Keep PNG format untouched (default: false)")]
            public bool png = false;
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

        static async Task<string> DownloadOneImage(string murl, int maxImageSize, bool keepPng)
        {
            string strImageStream = string.Empty;

            try
            {
                if (murl.StartsWith("http"))
                {
                    byte[] data = await DownloadWebImageToByteArray(murl);
                    if (data != null)
                    {
                        using (MemoryStream ms = new MemoryStream(data))
                        using (Bitmap bmp = new Bitmap(ms))
                        {
                            if (!keepPng || bmp.PixelFormat != PixelFormat.Format32bppArgb)
                            {
                                Bitmap resizedBmp = ImageUtility.DownsizeImage(bmp, maxImageSize > 0 ? maxImageSize : Int32.MaxValue, true);
                                data = ImageUtility.SaveImageToJpegInBuffer(resizedBmp);
                                if (!Object.ReferenceEquals(bmp, resizedBmp))
                                    resizedBmp.Dispose();
                            }
                        }
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
        static void Crawl(ArgsCrawl cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".image.tsv");

            int count = 0;
            int count_started = 0;
            int count_downloaded = 0;

            using (StreamWriter swFailed = new StreamWriter(Path.ChangeExtension(cmd.inTsv, ".failed.tsv")))
            {
                Stopwatch timer = Stopwatch.StartNew();

                var lines = File.ReadLines(cmd.inTsv)
                    .AsParallel().AsOrdered()
                    .Select(line => line.Split('\t').ToList())
                    .Select(async cols =>
                    {
                        count_started++;
                        var imageStream = await DownloadOneImage(cols[cmd.murlCol], cmd.resize, cmd.png);

                        if (string.IsNullOrEmpty(imageStream))
                            swFailed.WriteLine(string.Join("\t", cols));

                        cols.Add(imageStream);
                        if (!string.IsNullOrEmpty(imageStream))
                            count_downloaded++;
                        Console.Write("Lines started: {0}, processed: {1}, downloaded: {2}\r", count_started, ++count, count_downloaded);
                        return cols;
                    })
                    .Where(cols => !(cmd.discardBroken && string.IsNullOrEmpty(cols.Result.Last())))
                    .Select(cols => string.Join("\t", cols.Result));

                File.WriteAllLines(cmd.outTsv, 
                        cmd.maxDownloads > 0 ? lines.Take(cmd.maxDownloads) : lines);
                timer.Stop();
                Console.WriteLine("\nDownloaded {0} images from {1} lines in {2:0.0} seconds", count_downloaded, count, timer.Elapsed.TotalSeconds);
            }
        }

        static void Main(string[] args)
        {
            ParserX.AddTask<ArgsCrawl>(Crawl, "Crawl web images");
            ParserX.AddTask<ArgsBing>(Bing, "Bing scrapping");
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
