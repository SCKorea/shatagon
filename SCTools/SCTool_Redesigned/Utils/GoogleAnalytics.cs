using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using System.Xml.Serialization;

namespace SCTool_Redesigned.Utils
{
    class GoogleAnalytics
    {
        private static readonly string UA = $"SCTools Redesigned ({Environment.OSVersion.VersionString})";
        private static readonly string UL = CultureInfo.CurrentCulture.Name;

        public static void Sesstion(string uuid, string command) => Track(uuid, "event", new Dictionary<string, string> {
            { "sc", command }
        });

        public static void Hit(string uuid, string page, string title) => Track(uuid, "pageview", new Dictionary<string, string> {
            { "dh", "https://app.sc.galaxyhub.kr" },
            { "dp", page },
            { "dt", title },
        });

        public static void Event(string uuid, string category, string action) => Track(uuid, "event", new Dictionary<string, string> {
            { "ec", category },
            { "ea", action },
        });

        public static void Track(string uuid, string type, Dictionary<string, string> data)
        {
            Task.Run(() => {
                var request = (HttpWebRequest) WebRequest.Create("https://www.google-analytics.com/collect");
                request.Method = "POST";

                // the request body we want to send
                var postData = new Dictionary<string, string>
                {
                    { "v", "1" },
                    { "tid", "UA-190778304-1" },
                    { "cid",  uuid },
                    { "t", type },
                    { "ds", "web" },
                    { "ua", UA },
                    { "ul", UL }
                };

                data.ToList().ForEach(x => postData.Add(x.Key, x.Value));

                var postDataString = postData
                    .Aggregate("", (data, next) => string.Format("{0}&{1}={2}", data, next.Key, HttpUtility.UrlEncode(next.Value)))
                    .TrimEnd('&');

                // set the Content-Length header to the correct value
                request.ContentLength = Encoding.UTF8.GetByteCount(postDataString);

                // write the request body to the request
                using (var writer = new StreamWriter(request.GetRequestStream()))
                {
                    writer.Write(postDataString);
                }

                try
                {
                    var webResponse = (HttpWebResponse) request.GetResponse();
                    var statusCode = webResponse.StatusCode;

                    webResponse.Close();

                    if (statusCode != HttpStatusCode.OK)
                    {
                        throw new HttpException((int) statusCode, "Google Analytics tracking did not return OK 200");
                    }
                }
                catch (Exception ex)
                {
                }
            });
        }

        public enum HitType
        {
            @event,
            @pageview,
        }
    }
}
