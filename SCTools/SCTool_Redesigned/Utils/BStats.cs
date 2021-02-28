using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SCTool_Redesigned.Utils
{
    public static class BStats
    {
        // this is not intended for this purpose!
        // ref: https://github.com/Bastian/bStats-Metrics/blob/single-file/bukkit/Metrics.java

        public static void Send()
        {

            HttpClient client = HttpNetClient.Client;

            
        }        
        

    }

    public class MetricsBase
    {
        private const string REPORT_URL = "https://bStats.org/api/v2/data/%s";


        public MetricsBase()
        {

        }

    }
}
