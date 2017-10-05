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
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using CmdParser;
using TsvTool.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImageAcquisition
{
    partial class Program
    {
        class ArgsBing
        {
            [Argument(ArgumentType.Required, HelpText = "Input TSV file")]
            public string inTsv = null;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Output TSV file (default: replace inTsv .ext with .scrapping.tsv)")]
            public string outTsv = null;
            [Argument(ArgumentType.Required, HelpText = "Column index for query")]
            public int query = -1;
            [Argument(ArgumentType.AtMostOnce, HelpText = "Scrapping depth (default: 100)")]
            public int depth = 100;
        }

        static string queryUrlTemplate = @"http://www.bing.com/images/search?q={0}&form=MONITR&qs=n&format=pbxml&first=0&count={1}&fdpriority=premium&mkt=en-us"; //default
        // http://www.bing.com/images/search?q=giuseppe+zanotti+&qft=+filterui:face-portrait&FORM=R5IR33
        static async Task<string> DownloadBingResultPage(string query, int depth)
        {
            string result = string.Empty;
            var query_fields = query.Split(new string[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
            query_fields[0] = System.Net.WebUtility.UrlEncode(query_fields[0]);
            string queryUrl = String.Format(queryUrlTemplate, string.Join("&", query_fields), depth);
            using (WebClient wc = new WebClient())
            {
                bool retry = true;
                int retry_count = 0;
                while (retry && retry_count < 10)
                {
                    try
                    {
                        //wc.Proxy = new WebProxy("itgproxy.redmond.corp.microsoft.com");
                        result = await wc.DownloadStringTaskAsync(queryUrl);
                        retry = false;
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine("Download Page Error : {0}\n{1}", queryUrl, e.Message);
                        retry = true;
                        retry_count++;
                    }
                    if (retry)
                        await Task.Delay(1000);
                }
            }
            return result;
        }

        static IEnumerable<string[]> ParseResultXml(string xmlData)
        {
            var dom = new XmlDocument();
            dom.LoadXml(xmlData);
            var json = dom.GetElementsByTagName("k_AnswerDataKifResponse").Item(0).InnerText;
            int len = json.Length;
            JObject jo = JObject.Parse(json);
            JToken jt = jo.Property("results").Value;
            foreach (var x in jt.Children())
            {
                JObject jo2 = JObject.Parse(x.ToString());

                JToken value_murl, value_purl, value_title;
                string murl , purl, title;
                murl = purl = title = "";
                bool success_murl = jo2.TryGetValue("mediaURL", StringComparison.OrdinalIgnoreCase, out value_murl);
                bool success_purl = jo2.TryGetValue("URL", StringComparison.OrdinalIgnoreCase, out value_purl);
                bool success_title = jo2.TryGetValue("title", StringComparison.OrdinalIgnoreCase, out value_title);
                if (success_murl)
                    murl = value_murl.ToString();
                else
                    continue;
                if (success_purl)
                    purl = value_purl.ToString();
                if (success_title)
                    title = value_title.ToString();
                yield return new string[] { murl, purl, title };
            }
        }

        // Bing scrapping
        static void Bing(ArgsBing cmd)
        {
            if (cmd.outTsv == null)
                cmd.outTsv = Path.ChangeExtension(cmd.inTsv, ".scrapping.tsv");

            var lines = File.ReadLines(cmd.inTsv)
                .ReportProgress("Lines processed")
                .Select(line => line.Split('\t'))
                .Select(async cols =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    var xmlData = await DownloadBingResultPage(cols[cmd.query], cmd.depth);
                    var msec = (int)sw.ElapsedMilliseconds;
                    await Task.Delay(Math.Max(0, 333 - msec));
                    return new { cols = cols, xmlData = xmlData };
                })
                .SelectMany(xx =>
                {
                    var x = xx.Result;
                    return ParseResultXml(x.xmlData)
                        .Select((img, rank) => new {cols = x.cols, rank = rank, img = img});
                })
                .Select(x => string.Join("\t", x.cols) + "\t" + x.rank + "\t" + 
                        string.Join("\t", x.img));

            File.WriteAllLines(cmd.outTsv, lines);
            Console.WriteLine("\nDone.");
        }
    }
}
