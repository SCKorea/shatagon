using System;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Update;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// updatePatcher.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class updatePatcher : Page
    {
        private static CustomApplicationUpdater _updater = new(GetUpdateRepository(), App.ExecutableDir, new CustomPackageVerifier());
        private static CancellationTokenSource _cancellationToken = new();  //TODO: Dispose, cancel when exit
        public updatePatcher()
        {
            InitializeComponent();

            TryUpdateAsync();
        }

        private async void TryUpdateAsync()
        {
            try
            {
#if (!DEBUG)
                if (UpdateProcessHelper.UpdateFailed)
                {
                    App.Logger.Error("Self-update failed. Continuing with the current version without retrying this startup.");
                    MessageBox.Show(Properties.Resources.MSG_Desc_ApplicationUpdateFailed, App.Name);
                    return;
                }

                App.Logger.Info("Check for program updates.");

                var availableUpdate = await _updater.CheckForUpdateVersionAsync(_cancellationToken.Token);

                ProgBar.Value = ProgBar.Minimum;

                if (availableUpdate != null)
                {
                    App.Logger.Info("Program is not the latest version.");
                    App.Logger.Info("New Version found: "+availableUpdate.GetVersion());
                    App.Logger.Info("Current Version: " + App.Version?.ToString(4));

                    //FIXME:
                    var downloadDialogAdapter = new DownloadProgressDialogAdapter(null, this);
                    var filePath = await _updater.DownloadVersionAsync(availableUpdate, _cancellationToken.Token, downloadDialogAdapter);

                    if (!_updater.ScheduleInstallUpdate(availableUpdate, filePath))
                        throw new InvalidOperationException($"Failed to schedule update {availableUpdate.GetVersion()}.");

                    if (await InstallScheduledUpdateAsync())
                    {
                        GoogleAnalytics.Hit(App.Settings.UUID, "/update", "Program Update");

                        Windows.MainWindow.UI.Quit();
                    }
                    return;
                }

                ProgBar.Value = ProgBar.Maximum;

                App.Logger.Info("Program is the latest version.");
#endif

            }
            catch (Exception exception) //TODO: write log and label text, but not on MessageBox
            {
                App.Logger.Error(exception.Message);

                if (exception is HttpRequestException)
                {
                    MessageBox.Show($"{Properties.Resources.Localization_Download_ErrorTitle}" + '\n' + exception.Message, $"{Properties.Resources.Localization_Update_ErrorTitle}");
                }

                MessageBox.Show($"{Properties.Resources.MSG_Title_GeneralError}:{exception.Message}", $"{Properties.Resources.Localization_Update_ErrorTitle}");
            }
            finally
            {
                NextPhase();
            }
        }

        private static async Task<bool> InstallScheduledUpdateAsync()
        {
            var result = await _updater.InstallScheduledUpdateAsync(_cancellationToken.Token);

            if (result != InstallUpdateStatus.Success)
            {
                App.Logger.Error($"Failed to prepare self-update: {result}");
                MessageBox.Show(Properties.Resources.MSG_Desc_ApplicationUpdateFailed, App.Name);
                return false;
            }
            return true;
        }

        private static IUpdateRepository GetUpdateRepository()
        {
            var repository = "SCKorea/Shatagon";
            var updateRepository = new ApplicationUpdateRepository(HttpNetClient.Client, App.Name, repository);

            updateRepository.AllowPreReleases = false;
            updateRepository.SetCurrentVersion(App.Version.ToString(4));

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
