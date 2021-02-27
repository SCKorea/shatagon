using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SCTool_Redesigned.Settings;
using SCTool_Redesigned.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SCTool_Redesigned.Windows
{
    /// <summary>
    /// AuthWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AuthWindow : Window
    {
        private string _authtoken;
        private DispatcherTimer _labellifespantimer, _labelblinker;
        public AuthWindow()
        {
            InitializeComponent();
            _authtoken = null;
            _labellifespantimer = new DispatcherTimer();
            _labellifespantimer.Tick += new EventHandler(labellifespan);
            _labellifespantimer.Interval = TimeSpan.FromSeconds(3);
            _labelblinker = new DispatcherTimer();
            _labelblinker.Tick += new EventHandler(labelblink);
            _labelblinker.Interval = TimeSpan.FromMilliseconds(500);
        }

        private void labellifespan(object sender, EventArgs e)
        {
            _labellifespantimer.Stop();
            _labelblinker.Stop();
            ErrorLabel.Visibility = Visibility.Hidden;
        }
        private void labelblink(object sender, EventArgs e)
        {
            if(ErrorLabel.Visibility == Visibility.Visible)
                ErrorLabel.Visibility = Visibility.Hidden;
            else
                ErrorLabel.Visibility = Visibility.Visible;
        }

        public async Task<bool> TryAuth(string passwd)
        {
            var values = new Dictionary<string, string>
            {
                { "password", passwd }
            };
            var payload = new System.Net.Http.FormUrlEncodedContent(values);

            var response = await HttpNetClient.Client.PostAsync("https://sc.galaxyhub.kr/api/v2/password/check", payload);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var resultstr = await response.Content.ReadAsStringAsync();
                JObject jresult = JObject.Parse(resultstr);
                if (jresult["status"].ToString() == "200")
                {
                    _authtoken = jresult["value"].ToString();
                    return true;
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

        private async void Applybtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await TryAuth(CodeInputBox.Password);
            if (result)
            {
                _labelblinker.Stop();
                _labellifespantimer.Stop();
                this.Hide();
            }
            else
            {
                ErrorLabel.Visibility = Visibility.Visible;
                _labelblinker.Start();
                _labellifespantimer.Start();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public string GetAuthToken()
        {
            return _authtoken;
        }
    }
}
