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
using System.Net.Http;

namespace SCTool_Redesigned.Windows
{
    /// <summary>
    /// AuthWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AuthWindow : Window
    {
        private string _authtoken;
        private DispatcherTimer _labellifespantimer, _labelblinker;
        private int _status;
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
            _status = -1;
            CheckTGS();
        }
        private async void CheckTGS()
        {
            var json = new JObject();

            var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
            var response = await HttpNetClient.Client.PostAsync("https://sc.galaxyhub.kr/api/v4/patcher/mode", content);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var resultstr = await response.Content.ReadAsStringAsync();
                JObject jresult = JObject.Parse(resultstr);
                _status = int.Parse(jresult["data"].ToString());
                if (_status == 0)
                {
                    await TryAuthAsync("");//no need to enter code.
                }
            }
            else
            {
                //server is dead
                _status = -1;
            }
        }
        private void labellifespan(object sender, EventArgs e)
        {
            _labellifespantimer.Stop();
            _labelblinker.Stop();
            ErrorLabel.Visibility = Visibility.Hidden;
        }
        private void labelblink(object sender, EventArgs e)
        {
            if (ErrorLabel.Visibility == Visibility.Visible)
                ErrorLabel.Visibility = Visibility.Hidden;
            else
                ErrorLabel.Visibility = Visibility.Visible;
        }

        public async Task<int> TryAuthAsync(string passwd)
        {
            var json = new JObject();

            json.Add("password", passwd);

            var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
            var response = await HttpNetClient.Client.PostAsync("https://sc.galaxyhub.kr/api/v4/patcher/token", content);

            switch ((int)response.StatusCode)
            {
                case 200:
                    break;

                case 403:
                    return 403;

                case 422:
                    return 403;

                case 423:
                    return 423;

                default:
                    MessageBox.Show("인증서버 통신 오류");
                    return -1;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var resultstr = await response.Content.ReadAsStringAsync();
                JObject jresult = JObject.Parse(resultstr);
                var statcode = int.Parse(jresult["status"].ToString());
                if (statcode == 200)
                {
                    _authtoken = jresult["data"].ToString();
                }
                return statcode;
            }
            else
            {
                MessageBox.Show("인증서버 통신 오류");
                return -1;
            }
        }

        private void Applybtn_Click(object sender, RoutedEventArgs e)
        {
            SubmitPasswordAsync();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        private void CodeInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SubmitPasswordAsync();
            }
        }

        public string GetAuthToken()
        {
            return _authtoken;
        }

        private async void SubmitPasswordAsync()
        {

            var result = await TryAuthAsync(CodeInputBox.Text);
            if (result == 200)
            {
                _labelblinker.Stop();
                _labellifespantimer.Stop();
                this.Hide();
            }
            else
            {
                Dispatcher.Invoke(DispatcherPriority.Background, new Action(() =>
                {
                    ErrorLabel.Visibility = Visibility.Visible;
                }));

                switch (result) //TODO: localization
                {
                    case 403:
                        ErrorLabel.Content = Properties.Resources.UI_Desc_AuthError;
                        _labelblinker.Start();
                        _labellifespantimer.Start();
                        break;
                    case 423:
                        ErrorLabel.Content = "패치 배포가 일시 중단되었습니다.";
                        break;
                    case -1:
                        ErrorLabel.Content = "인증서버 통신 오류";
                        break;
                    default:
                        ErrorLabel.Content = $"인증서버 응답오류 : {result}";
                        break;
                }
            }

        }
    }
}
