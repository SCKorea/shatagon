using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System.Windows.Forms;

namespace NSW.StarCitizen.Tools.Controllers
{
    public class AuthController
    {
        private string _authtoken=null;
        private bool _authed=false;
        static AuthController()
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }
        public async Task<bool> try_auth(string passwd)
        {
            var values = new Dictionary<string, string>
            {
                { "password", passwd }
            };
            var payload = new System.Net.Http.FormUrlEncodedContent(values);

            var response = await NSW.StarCitizen.Tools.Update.HttpNetClient.Client.PostAsync("https://sc.galaxyhub.kr/api/v1/password/check", payload);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var resultstr = await response.Content.ReadAsStringAsync();
                JObject jresult = JObject.Parse(resultstr);
                //MessageBox.Show(jresult.ToString());
                if (jresult["status"].ToString() == "200")
                {
                    _authtoken=jresult["value"].ToString();
                    return _authed = true;
                }
                else
                    return false;
               
            }
            else
            {
                MessageBox.Show("인증서버 통신 오류");
                return false;
            }
        }
        public string get_authtoken()
        {
            if (_authed)
                return _authtoken;
            else
                return null;
        }
    }
}
