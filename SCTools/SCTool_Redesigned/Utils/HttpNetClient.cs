using System;
using System.Net;
using System.Net.Http;

namespace SCTool_Redesigned.Utils
{
    public static class HttpNetClient
    {
        private static readonly HttpClientHandler _clientHandler;
        public static HttpClient Client { get; }
        static HttpNetClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            _clientHandler = new HttpClientHandler();
            //_clientHandler.UseProxy = App.Settings.UseHttpProxy;
            Client = new HttpClient(_clientHandler);
            Client.DefaultRequestHeaders.UserAgent.ParseAdd($"{App.Name}/{App.Version.ToString(3)}");
            Client.Timeout = TimeSpan.FromMinutes(1);
        }
    }
}
