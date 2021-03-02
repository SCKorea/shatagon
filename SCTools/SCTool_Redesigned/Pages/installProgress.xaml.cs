using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using SCTool_Redesigned.Windows;
using SCTool_Redesigned.Utils;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Update;
using NSW.StarCitizen.Tools.Lib.Localization;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// installProgress.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class installProgress : Page
    {
        private static CancellationTokenSource _cancellationToken = new CancellationTokenSource();  //TODO: Dispose, cancel when exit
        private GameSettings GameSettings;
        public installProgress(MainWindow.InstallerMode mode)
        {
            InitializeComponent();

            //TODO: CHOOSE PTU LIVE 
            foreach (GameInfo gameInfo in GameFolders.GetGameModes(App.Settings.GameFolder))
            {
                if( gameInfo.Mode == GameMode.LIVE)
                {
                    App.CurrentGame = gameInfo;
                    break;
                }
            }
            GameSettings = new GameSettings(App.CurrentGame);
            switch (mode)
            {
                case MainWindow.InstallerMode.install:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationInstall;
                    InstallVersionAsync();
                    break;
                case MainWindow.InstallerMode.uninstall:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationUninstall;
                    Uninstall();
                    break;
                case MainWindow.InstallerMode.disable:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationPH;
                    RepositoryManager.ToggleLocalization();
                    break;
            }

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

        public async void InstallVersionAsync()
        {
            //_logger.Info($"Install localization: {CurrentGame.Mode}, {selectedUpdateInfo.Dump()}");
            bool status = false;
            try
            {
                Cursor = Cursors.Wait;
                var downloadDialogAdapter = new InstallDownloadProgressDialogAdapter(RepositoryManager.TargetInstallation.InstalledVersion,this);
                var filePath = await RepositoryManager.TargetRepository.DownloadAsync(RepositoryManager.TargetInfo, Path.GetTempPath(),
                    _cancellationToken.Token, downloadDialogAdapter);
                var result = RepositoryManager.TargetRepository.Installer.Install(filePath, App.CurrentGame.RootFolderPath);
                switch (result)
                {
                    case InstallStatus.Success:
                        GameSettings.Load();
                        ProgBar.Value = ProgBar.Maximum;
                        RepositoryManager.SetInstalledRepository();
                        status = true;
                        break;
                    case InstallStatus.PackageError:
                        //_logger.Error($"Failed install localization due to package error: {CurrentGame.Mode}, {selectedUpdateInfo.Dump()}");
                        MessageBox.Show(Properties.Resources.Localization_Package_ErrorText,
                            Properties.Resources.Localization_Package_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case InstallStatus.VerifyError:
                        //_logger.Error($"Failed install localization due to core verify error: {CurrentGame.Mode}, {selectedUpdateInfo.Dump()}");
                        MessageBox.Show(Properties.Resources.Localization_Verify_ErrorText,
                            Properties.Resources.Localization_Verify_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    case InstallStatus.FileError:
                        //_logger.Error($"Failed install localization due to file error: {CurrentGame.Mode}, {selectedUpdateInfo.Dump()}");
                        MessageBox.Show(Properties.Resources.Localization_File_ErrorText,
                            Properties.Resources.Localization_File_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    default:
                        //_logger.Error($"Failed install localization: {CurrentGame.Mode}, {selectedUpdateInfo.Dump()}");
                        MessageBox.Show(Properties.Resources.Localization_Install_ErrorText,
                            Properties.Resources.Localization_Install_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception e)
            {

                //_logger.Error(e, $"Error during install localization: {CurrentGame.Mode}, {selectedUpdateInfo.Dump()}");
                if (e is HttpRequestException)
                {
                    MessageBox.Show(Properties.Resources.Localization_Download_ErrorText + '\n' + e.Message,
                        Properties.Resources.Localization_Download_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(Properties.Resources.Localization_Download_ErrorText,
                        Properties.Resources.Localization_Download_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                Cursor = null;  //Cursor to default
            }
            if (status)
            {
                RepositoryManager.ToggleLocalization();
                MainWindow.UI.Phase++;
            }
            else
                MainWindow.UI.Phase--;
        }

        public void Uninstall()
        {
            if (RepositoryManager.TargetInstallation.InstalledVersion != null)
            {
                if (!App.CurrentGame.IsAvailable())
                {
                    //_logger.Error($"Uninstall localization mode path unavailable: {CurrentGame.RootFolderPath}");
                    MessageBox.Show(Properties.Resources.Localization_Uninstall_ErrorText,
                        Properties.Resources.Localization_Uninstall_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                //_logger.Info($"Uninstall localization: {CurrentGame.Mode}, {CurrentInstallation.Repository} {CurrentInstallation.InstalledVersion}");
                try
                {
                    switch (RepositoryManager.TargetRepository.Installer.Uninstall(App.CurrentGame.RootFolderPath))
                    {
                        case UninstallStatus.Success:
                            GameSettings.RemoveCurrentLanguage();
                            GameSettings.Load();
                            ProgBar.Value = ProgBar.Maximum;
                            RepositoryManager.RemoveInstalledRepository();
                            break;
                        case UninstallStatus.Partial:
                            GameSettings.RemoveCurrentLanguage();
                            GameSettings.Load();
                            ProgBar.Value = ProgBar.Maximum;
                            RepositoryManager.RemoveInstalledRepository();
                            //_logger.Warn($"Localization uninstalled partially: {CurrentGame.Mode}");
                            MessageBox.Show(Properties.Resources.Localization_Uninstall_WarningText,
                                    Properties.Resources.Localization_Uninstall_WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                            break;
                        default:
                            //_logger.Error($"Failed uninstall localization: {CurrentGame.Mode}");
                            MessageBox.Show(Properties.Resources.Localization_Uninstall_ErrorText,
                                Properties.Resources.Localization_Uninstall_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                    }
                }
                catch (Exception e)
                {
                    //_logger.Error(e, $"Error during uninstall localization: {CurrentGame.Mode}");
                    MessageBox.Show(Properties.Resources.Localization_Uninstall_ErrorText+"\n"+e.Message,
                        Properties.Resources.Localization_Uninstall_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void DisableInstallation()
        {
            //MessageBox.Show("대충 비활성화하는 동작");
            ProgBar.Value = ProgBar.Maximum;
        }
    }
    public class InstallDownloadProgressDialogAdapter : IDownloadProgress
    {
        private readonly string _localizationVersion;
        private long _totalContentSize;
        private long _downloadedSize;
        private installProgress _dialog;

        public InstallDownloadProgressDialogAdapter(string localizationVersion, installProgress dialog)
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
                _dialog.ProgBar.IsIndeterminate = true;
                _dialog.DescText.Content = $"{downloadSizeMBytes:0.00} MB";
            }
        }
    }
}
