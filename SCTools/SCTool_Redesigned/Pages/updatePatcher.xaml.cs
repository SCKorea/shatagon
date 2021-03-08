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
using SCTool_Redesigned.Update;
using System.IO;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// updatePatcher.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class updatePatcher : Page
    {
        private static ApplicationUpdater _updater = new ApplicationUpdater(GetUpdateRepository(), App.ExecutableDir, Properties.Resources.UpdateScript, new CustomPackageVerifier());
        private static CancellationTokenSource _cancellationToken = new CancellationTokenSource();  //TODO: Dispose, cancel when exit
        public updatePatcher()
        {
            InitializeComponent();

            if (!CheckUpdated())
            {
                TryUpdateAsync();
            }
            else
            {
                CleanUpdate();
            }
            
        }

        private bool CheckUpdated()
        {
            var batchFile = System.IO.Path.Combine(App.ExecutableDir, "update.bat");

            return File.Exists(batchFile);
        }

        private void CleanUpdate()
        {
            _updater.RemoveUpdateScript();

            NextPhase();

        }

        private async void TryUpdateAsync()
        {
            try
            {
#if (!DEBUG)
                var availableUpdate = await _updater.CheckForUpdateVersionAsync(_cancellationToken.Token);

                ProgBar.Value = ProgBar.Minimum;

                if (availableUpdate != null)
                {
                    //MessageBox.Show("업데이트 있음", "업데이트 확인");
                    //FIXME:
                    var downloadDialogAdapter = new DownloadProgressDialogAdapter(null, this);
                    var filePath = await _updater.DownloadVersionAsync(availableUpdate, _cancellationToken.Token, downloadDialogAdapter);

                    _updater.ScheduleInstallUpdate(availableUpdate, filePath);

                    if (InstallScheduledUpdate())
                    {
                        GoogleAnalytics.Hit(App.Settings.UUID, "/update", "Program Update");

                        Windows.MainWindow.UI.Quit();

                        return;
                    }
                }

                // MessageBox.Show("업데이트 없음", "업데이트 확인");
                ProgBar.Value = ProgBar.Maximum;
#endif
                
            }
            catch (Exception exception) //TODO: write log and label text, but not on MessageBox
            {
                if (exception is HttpRequestException)
                    MessageBox.Show($"{Properties.Resources.Localization_Download_ErrorTitle}" + '\n' + exception.Message, $"{Properties.Resources.Localization_Update_ErrorTitle}");
                else
                    MessageBox.Show($"{Properties.Resources.Localization_Download_ErrorTitle}", $"{Properties.Resources.Localization_Update_ErrorTitle}");
                MessageBox.Show($"{Properties.Resources.MSG_Title_GeneralError}:{exception.Message}", $"{Properties.Resources.Localization_Update_ErrorTitle}");
            }
            finally
            {
                NextPhase();
            }
        }

        private static bool InstallScheduledUpdate()
        {
            var result = _updater.InstallScheduledUpdate();

            if (result != InstallUpdateStatus.Success)
            {
                //_logger.Error($"Failed launch install update: {result}");
                MessageBox.Show($"{Properties.Resources.Localization_Update_ErrorTitle}" + @" - " + result.ToString("d"), App.Name); //FOR DEBUG
                return false;
            }
            return true;
        }

        private static IUpdateRepository GetUpdateRepository()
        {
            var repository = "SCKorea/Shatagon";
            var updateInfoFactory = GitHubUpdateInfo.Factory.NewWithVersionByTagName();
            var updateRepository = new GitHubUpdateRepository(HttpNetClient.Client, GitHubDownloadType.Assets, updateInfoFactory, App.Name, repository);

            updateRepository.AllowPreReleases = App.Settings.Nightly;
            updateRepository.SetCurrentVersion(App.Version.ToString(3));

            return updateRepository;
        }

        private static void NextPhase()
        {
            Windows.MainWindow.UI.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                Windows.MainWindow.UI.Phase++;
            }));
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
