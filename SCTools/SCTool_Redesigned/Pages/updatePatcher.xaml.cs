using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Forms;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// updatePatcher.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class updatePatcher : Page
    {
        private static ApplicationUpdater _updater = new ApplicationUpdater(GetUpdateRepository(), App.ExecutableDir, Properties.Resources.UpdateScript);
        private static CancellationTokenSource _cancellationToken = new CancellationTokenSource();  //TODO: Dispose, cancel when exit
        public updatePatcher()
        {
            InitializeComponent();
            //Progressbar_demo();
            TryUpdate();
        }
        private DispatcherTimer timer1;
        private void Progressbar_demo()
        {
            ProgBar.Value = 0;
            timer1 = new DispatcherTimer();
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = TimeSpan.FromMilliseconds(30);
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ProgBar.Value += 5;
            if (ProgBar.Value == ProgBar.Maximum)
            {
                timer1.Stop();
                ((Windows.MainWindow)Application.Current.MainWindow).Phase++;
            }
        }

        private async void TryUpdate()
        {
            //var scheduledUpdateInfo = _updater.GetScheduledUpdateInfo();
            //if (scheduledUpdateInfo != null)
            //{
            //    if (_updater.IsAlreadyInstalledVersion(scheduledUpdateInfo))
            //    {
            //        _updater.CancelScheduleInstallUpdate();
            //        return false;
            //    }
            //    return InstallScheduledUpdate();
            //}
            //return false;
            try
            {
                var availableUpdate = await _updater.CheckForUpdateVersionAsync(_cancellationToken.Token);
                ProgBar.Value = ProgBar.Minimum;
                if (availableUpdate == null)
                {
                    //MessageBox.Show("업데이트 없음", "업데이트 확인");
                    ProgBar.Value = ProgBar.Maximum;
                    return;
                }
                //MessageBox.Show("업데이트 있음", "업데이트 확인");
                //FIXME:
                var downloadDialogAdapter = new DownloadProgressDialogAdapter(null, this);
                var filePath = await _updater.DownloadVersionAsync(availableUpdate, _cancellationToken.Token, downloadDialogAdapter);
                _updater.ScheduleInstallUpdate(availableUpdate, filePath);
                //if (InstallScheduledUpdate())
                //{
                //    ((Windows.MainWindow)Application.Current.MainWindow).Quit();
                //}

                GoogleAnalytics.Event(App.Settings.UUID, "Program", "Update");
            }
            catch (Exception exception) //TODO: write log and label text, but not on MessageBox
            {
                if (exception is HttpRequestException)
                    MessageBox.Show("다운로드 에러:" + '\n' + exception.Message, "업데이트 에러");
                else
                    MessageBox.Show("다운로드 에러:", "업데이트 에러");
                MessageBox.Show("어쨌든 에러:", "업데이트 에러");
            }
            finally
            {
                ((Windows.MainWindow)Application.Current.MainWindow).Phase++;
            }
        }

        private static bool InstallScheduledUpdate()
        {
            var result = _updater.InstallScheduledUpdate();
            if (result != InstallUpdateStatus.Success)
            {
                //_logger.Error($"Failed launch install update: {result}");
                MessageBox.Show("업데이트 실패" + @" - " + result.ToString("d"), App.Name); //FOR DEBUG
                return false;
            }
            return true;
        }

        private static IUpdateRepository GetUpdateRepository()
        {
            var updateInfoFactory = GitHubUpdateInfo.Factory.NewWithVersionByTagName();
            var updateRepository = new GitHubUpdateRepository(HttpNetClient.Client,
                GitHubDownloadType.Assets, updateInfoFactory, App.Name, "SCKorea/Shatagon");
            updateRepository.SetCurrentVersion(App.Version.ToString(3));
            return updateRepository;
        }
    }

    public class DownloadProgressDialogAdapter : IDownloadProgress
    {
        private readonly string _localizationVersion;
        private long _totalContentSize;
        private long _downloadedSize;
        private updatePatcher _dialog;

        public DownloadProgressDialogAdapter(string localizationVersion, updatePatcher dialog)
        {
            _localizationVersion = localizationVersion;
            _dialog = dialog;
        }

        public void ReportContentSize(long value)
        {
            _totalContentSize = value;
            UpdateDialogTaskInfo();
        }

        public void ReportDownloadedSize(long value)
        {
            _downloadedSize = value;
            UpdateDialogTaskInfo();
        }

        private void UpdateDialogTaskInfo()
        {

            float downloadSizeMBytes = (float)_downloadedSize / (1024 * 1024);
            if (_totalContentSize > 0)
            {
                _dialog.ProgBar.Value = _downloadedSize * _dialog.ProgBar.Maximum / _totalContentSize;
                float contentSizeMBytes = (float)_totalContentSize / (1024 * 1024);
                _dialog.DescText.Content = $"{downloadSizeMBytes:0.00} MB/{contentSizeMBytes:0.00} MB";
            }
            else
            {
                _dialog.DescText.Content = $"{downloadSizeMBytes:0.00} MB";
            }
        }
    }
}
