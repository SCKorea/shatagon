using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Windows;

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
                if (gameInfo.Mode == GameMode.LIVE)
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

        public async void InstallVersionAsync()
        {
            App.Logger.Info("Start localization installation");

            bool status = false;

            try
            {
                Cursor = Cursors.Wait;

                var downloadDialogAdapter = new InstallDownloadProgressDialogAdapter(RepositoryManager.TargetInstallation.InstalledVersion, this);
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
                        App.Logger.Error("Failed install localization due to package error");

                        MessageBox.Show(Properties.Resources.Localization_Package_ErrorText,
                            Properties.Resources.Localization_Package_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case InstallStatus.VerifyError:
                        App.Logger.Error("Failed install localization due to core verify error");

                        MessageBox.Show(Properties.Resources.Localization_Verify_ErrorText,
                            Properties.Resources.Localization_Verify_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case InstallStatus.FileError:
                        App.Logger.Error("Failed install localization due to file error");

                        MessageBox.Show(Properties.Resources.Localization_File_ErrorText,
                            Properties.Resources.Localization_File_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    default:
                        App.Logger.Error("Failed install localization");

                        MessageBox.Show(Properties.Resources.Localization_Install_ErrorText,
                            Properties.Resources.Localization_Install_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception e)
            {

                App.Logger.Error(e, "Error during install localization");

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

                if (!RepositoryManager.TargetInstallation.IsEnabled)
                {
                    RepositoryManager.ToggleLocalization(); //to ensure enabled
                }

                App.Logger.Info("Finish localization installation");
                MainWindow.UI.Phase++;
            }
            else
            {
                App.Logger.Info("Fail localization installation");
                MainWindow.UI.Phase--;
            }
        }

        public void Uninstall()
        {
            App.Logger.Info("Start localization uninstallation");

            if (RepositoryManager.TargetInstallation.InstalledVersion != null)
            {
                if (!App.CurrentGame.IsAvailable())
                {
                    App.Logger.Error("Uninstall localization mode path unavailable");
                    MessageBox.Show(Properties.Resources.Localization_Uninstall_ErrorText,
                        Properties.Resources.Localization_Uninstall_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    switch (RepositoryManager.TargetRepository.Installer.Uninstall(App.CurrentGame.RootFolderPath))
                    {
                        case UninstallStatus.Success:
                            GameSettings.RemoveCurrentLanguage();
                            GameSettings.Load();
                            ProgBar.Value = ProgBar.Maximum;
                            RepositoryManager.RemoveInstalledRepository();

                            App.Logger.Info("Finish localization uninstallation");
                            break;

                        case UninstallStatus.Partial:
                            GameSettings.RemoveCurrentLanguage();
                            GameSettings.Load();
                            ProgBar.Value = ProgBar.Maximum;
                            RepositoryManager.RemoveInstalledRepository();

                            App.Logger.Warn("Localization uninstalled partially");

                            MessageBox.Show(Properties.Resources.Localization_Uninstall_WarningText,
                                    Properties.Resources.Localization_Uninstall_WarningTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                            break;

                        default:
                            App.Logger.Error("Failed uninstall localization");

                            MessageBox.Show(Properties.Resources.Localization_Uninstall_ErrorText,
                                Properties.Resources.Localization_Uninstall_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                    }
                }
                catch (Exception e)
                {
                    App.Logger.Error(e, "Error during uninstall localization");

                    MessageBox.Show(Properties.Resources.Localization_Uninstall_ErrorText + "\n" + e.Message,
                        Properties.Resources.Localization_Uninstall_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void DisableInstallation()
        {
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
