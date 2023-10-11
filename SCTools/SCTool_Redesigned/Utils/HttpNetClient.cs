using System;
using System.Net;
using System.Net.Http;

namespace SCTool_Redesigned.Utils
{
    public static class HttpNetClient
    {
        private static HttpClient _client;

        public static HttpClient Client
        {
            get
            {
                if (_client == null)
                {
                    _client = CreateHttpClient();
                }

                return _client;
            }

        }

        static HttpNetClient()
        {

        }

        public static void Dispose()
        {
            _client.Dispose();
            _client = null;
        }

        private static HttpClient CreateHttpClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            HttpClientHandler handler = new HttpClientHandler();
            //handler.UseProxy = App.Settings.UseHttpProxy;

            HttpClient client = new HttpClient(handler);

            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{App.Name}/{App.Version.ToString(3)}");
            client.Timeout = TimeSpan.FromMinutes(1);

            return client;
        }
    }
}
