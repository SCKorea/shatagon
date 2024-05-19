using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SCTool_Redesigned.Utils
{
    class GoogleAnalytics
    {
        private static bool Enable = false;

        private static readonly string UA = $"SCTools Redesigned ({Environment.OSVersion.VersionString})";
        private static readonly string UL = CultureInfo.CurrentCulture.Name;

        public static void Session(string uuid, string command, bool wait) => Track(uuid, "event", new Dictionary<string, string> {
            { "sc", command }
        }, wait);

        public static void Hit(string uuid, string page, string title) => Track(uuid, "pageview", new Dictionary<string, string> {
            { "dh", Properties.Resources.Google_Analytics_Site },
            { "dp", page },
            { "dt", title },
        });

        public static void Event(string uuid, string category, string action) => Track(uuid, "event", new Dictionary<string, string> {
            { "ec", category },
            { "ea", action },
        });

        public static void Track(string uuid, string type, Dictionary<string, string> data, bool wait = false)
        {
            //TODO
        }

        public enum HitType
        {
            @event,
            @pageview,
        }
    }
}
