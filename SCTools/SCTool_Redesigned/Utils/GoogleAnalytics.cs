using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SCTool_Redesigned.Utils
{
    class GoogleAnalytics
    {
        public static void Track(string uuid, HitType type, Dictionary<string, string> data)
        {
            var request = (HttpWebRequest) WebRequest.Create("https://www.google-analytics.com/collect");
            request.Method = "POST";

            // the request body we want to send
            var postData = new Dictionary<string, string>
                           {
                               { "v", "1" },
                               { "tid", "UA-190778304-1" },
                               { "cid",  uuid },
                               { "t", type.ToString() }
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
                if (webResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpException((int) webResponse.StatusCode,"Google Analytics tracking did not return OK 200");
                }
            }
            catch (Exception ex)
            {
            }
        }

        public enum HitType
        {
            @event,
            @pageview,
        }
    }
}
