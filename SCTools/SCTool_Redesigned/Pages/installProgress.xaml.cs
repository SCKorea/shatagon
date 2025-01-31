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
        //private GameSettings _gameSettings;

        public installProgress(MainWindow.InstallerMode mode)
        {
            InitializeComponent();

            var gameMode = App.SelectedGameMode;

            if (gameMode == "")
            {
                App.Logger.Info("Not selected game mode.");

                return;
            }

            App.Logger.Info($"Game Folder Name: {gameMode}");

            GameInfo? gameInfo = null;

            foreach (var info in GameFolders.GetGameModes(App.Settings.GameFolder))
            {
                App.Logger.Info(info.Mode);

                if (info.Mode == gameMode)
                {
                    gameInfo = info;
                    break;
                }
            }

            if (gameInfo == null)
            {
                App.Logger.Info($"Not found matched mode, check game folder or starcitizen.exe");

                return;
            }

            var gameSetting = new GameSettings(gameInfo);

            App.Logger.Info($"Game Mode: {gameInfo.Mode}");
          

            switch (mode)
            {
                case MainWindow.InstallerMode.install:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationInstall;
                    InstallVersionAsync(gameInfo, gameSetting);
                    break;

                case MainWindow.InstallerMode.uninstall:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationUninstall;
                    Uninstall(gameInfo, gameSetting);
                    break;

                case MainWindow.InstallerMode.disable:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationPH;
                    Disable();
                    break;
            }


            App.SelectedGameMode = "";
        }

        private async void InstallVersionAsync(GameInfo gameInfo, GameSettings gameSettings)
        {
            App.Logger.Info("Start localization installation");

            Cursor = Cursors.Wait;
            var targetInstallation = RepositoryManager.TargetInstallation;

            if (targetInstallation == null)
            {
                App.Logger.Error("Not found TargetInstallation");

                MessageBox.Show(
                    Properties.Resources.Localization_Install_ErrorText,
                    Properties.Resources.Localization_Install_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error
                );

                MainWindow.UI.Phase--;

                return;
            }

            var downloadDialogAdapter = new InstallDownloadProgressDialogAdapter(targetInstallation.InstalledVersion, this);
            var targetRepository = RepositoryManager.TargetRepository;
            var targetUpdateInfo = RepositoryManager.TargetInfo;

            if (targetRepository == null || targetUpdateInfo == null)
            {
                App.Logger.Error("Not found Patch Repository or Repository info");

                MessageBox.Show(
                    Properties.Resources.Localization_Install_ErrorText,
                    Properties.Resources.Localization_Install_ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error
                );

                return;
            }

            bool status = false;

            try
            {
                var tempPath = Path.GetTempPath();
                var patchZipFile = await targetRepository.DownloadAsync(targetUpdateInfo, tempPath, _cancellationToken.Token, downloadDialogAdapter);
                var result = targetRepository.Installer.Install(patchZipFile, gameInfo.RootFolderPath);

                App.Logger.Info($"install path: {gameInfo.RootFolderPath}");
                App.Logger.Info($"install result: {result}");

                switch (result)
                {
                    case InstallStatus.Success:
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
            catch (HttpRequestException e)
            {
                App.Logger.Error(e, "Error during install localization");
                MessageBox.Show(
                    Properties.Resources.Localization_Download_ErrorText + '\n' + e.Message,
                    Properties.Resources.Localization_Download_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (Exception e)
            {
                App.Logger.Error(e, "Error during install localization");
                MessageBox.Show(
                    Properties.Resources.Localization_Download_ErrorText,
                    Properties.Resources.Localization_Download_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                Cursor = null;  //Cursor to default
            }

            if (status == false)
            {
                App.Logger.Info("Fail localization installation");
                MainWindow.UI.Phase--;

                return;
            }

            ProgBar.Value = ProgBar.Maximum;
            gameSettings.Load();
 
            if (targetInstallation.IsEnabled == false)
            {
                var destinationPath = Path.Combine(App.Settings.GameFolder, targetInstallation.Mode);
                var installationType = targetRepository.Installer.RevertLocalization(destinationPath);

                if (installationType == LocalizationInstallationType.Disabled)
                {
                    targetInstallation.IsEnabled = false;
                }

                if (installationType == LocalizationInstallationType.Enabled)
                {
                    targetInstallation.IsEnabled = true;
                }

                if (installationType == LocalizationInstallationType.None)
                {
                    RepositoryManager.RemoveInstallationRepository(targetInstallation);
                }
            }

            RepositoryManager.SetInstallationRepository(targetInstallation);

            App.Logger.Info("Finish localization installation");
            MainWindow.UI.Phase++;
        }

        private void Uninstall(GameInfo gameInfo, GameSettings gameSettings)
        {
            App.Logger.Info("Start localization uninstallation");

            var targetInstallation = RepositoryManager.TargetInstallation;
            var targetRepository = RepositoryManager.TargetRepository;

            if (targetInstallation == null || targetRepository == null)
            {
                App.Logger.Info("TargetInstallation or TargetRepository is not registered");

                return;
            }

            try
            {
                var uninstallStatus = targetRepository.Installer.Uninstall(gameInfo.RootFolderPath);

                switch (uninstallStatus)
                {
                    case UninstallStatus.Success:
                        gameSettings.RemoveCurrentLanguage();
                        gameSettings.Load();

                        ProgBar.Value = ProgBar.Minimum;

                        RepositoryManager.RemoveInstallationRepository(targetInstallation);
                        App.Logger.Info("Finish localization uninstallation");

                        break;

                    case UninstallStatus.Partial:
                        gameSettings.RemoveCurrentLanguage();
                        gameSettings.Load();

                        ProgBar.Value = ProgBar.Maximum;

                        RepositoryManager.RemoveInstallationRepository(targetInstallation);

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

        private void Disable()
        {

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
