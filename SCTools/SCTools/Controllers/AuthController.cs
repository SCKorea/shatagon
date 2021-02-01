using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;

using System.Windows.Forms;

namespace NSW.StarCitizen.Tools.Controllers
{
    public class AuthController
    {
        private string authtoken;
        private bool authed;
        private static readonly HttpClient client = new HttpClient();
        public AuthController()
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            authtoken = "empty";
            authed = false;
        }
        public async Task<bool> try_auth(string passwd)
        {
            var payload = new StringContent("{\'passworsd\':" + passwd + "}", Encoding.UTF8, "application/json");
            //var payload = new StringContent("{\'password\':" + passwd + "}", Encoding.UTF8);
            MessageBox.Show("{\'password\':\'" + passwd + "\'}");
            var response = await client.PostAsync("https://sc.galaxyhub.kr/api/v1/password/check", payload);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var resultstr = await response.Content.ReadAsStringAsync();
                var jresult = JsonConvert.DeserializeObject(resultstr);
                MessageBox.Show(jresult.ToString());

                //TODO: 코드 판정
                return authed = true;
            }
            else
            {
                MessageBox.Show("인증서버 통신 오류");
                return false;
            }
        }
        public string get_authtoken()
        {
            return "asdf";
        }
    }
}
