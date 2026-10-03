using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Localization;
using SCTool_Redesigned.Update;
using SCTool_Redesigned.Windows;
using static SCTool_Redesigned.Windows.MainWindow;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// installProgress.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class installProgress : Page
    {
        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private bool _installStarted;
        private bool _navigationCanceled;

        public bool CancelDownload()
        {
            if (_installStarted) return false;
            _navigationCanceled = true;
            _cancellationToken.Cancel();
            return true;
        }
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

            App.Logger.Info($"Game Mode: {gameInfo.Mode}");
          

            switch (mode)
            {
                case MainWindow.InstallerMode.install:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationInstall;
                    InstallVersionAsync(gameInfo);
                    break;

                case MainWindow.InstallerMode.uninstall:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationUninstall;
                    Uninstall(gameInfo, new GameSettings(gameInfo));
                    break;

                case MainWindow.InstallerMode.disable:
                    Phasetext.Content = Properties.Resources.UI_Desc_LocailzationPH;
                    Disable();
                    break;
            }


        }

        private async void InstallVersionAsync(GameInfo gameInfo)
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

                ReturnToSelection();

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

                ReturnToSelection();
                return;
            }

            bool status = false;
            string? patchZipFile = null;
            var actualVariant = "legacy";
            IReadOnlyList<string>? actualFeatures = null;

            try
            {
                var tempPath = Path.GetTempPath();
                if (RepositoryManager.GetLocalizationSource().HasVariant)
                {
                    var release = RepositoryManager.TargetRelease;
                    if (release == null || release.Info.Tag != targetUpdateInfo.TagName || !RepositoryManager.FeatureSelectionReady)
                        throw new InvalidOperationException("The selected release's feature catalog is not ready");
                    var prepared = await targetRepository.PrepareFeaturesAsync(release,
                        RepositoryManager.TargetFeatures, tempPath, _cancellationToken.Token, downloadDialogAdapter);
                    patchZipFile = prepared.ZipPath;
                    actualVariant = prepared.LegacyVariant;
                    actualFeatures = prepared.Features;
                    App.Logger.Info($"Features: requested={string.Join(",", RepositoryManager.TargetFeatures)}, actual={string.Join(",", actualFeatures ?? [])}, tag={release.Info.Tag}");
                    if (prepared.Warnings.Count > 0)
                    {
                        var warning = string.Join("\n", prepared.Warnings);
                        App.Logger.Warn(warning);
                        MessageBox.Show(warning, Properties.Resources.MSG_Title_GeneralWarning,
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    patchZipFile = await targetRepository.DownloadAsync(targetUpdateInfo, tempPath, _cancellationToken.Token, downloadDialogAdapter);
                }
                _cancellationToken.Token.ThrowIfCancellationRequested();
                _installStarted = true;
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
            catch (OperationCanceledException)
            {
                App.Logger.Info("Localization download canceled");
            }
            catch (HttpRequestException e)
            {
                App.Logger.Error(e, "Error during install localization");
                if (!_cancellationToken.IsCancellationRequested) MessageBox.Show(
                    Properties.Resources.Localization_Download_ErrorText + '\n' + e.Message,
                    Properties.Resources.Localization_Download_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            catch (Exception e)
            {
                App.Logger.Error(e, "Error during install localization");
                if (!_cancellationToken.IsCancellationRequested) MessageBox.Show(
                    Properties.Resources.Localization_Download_ErrorText + '\n' + e.Message,
                    Properties.Resources.Localization_Download_ErrorTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                Cursor = null;  //Cursor to default
                if (patchZipFile != null && File.Exists(patchZipFile))
                {
                    try { File.Delete(patchZipFile); }
                    catch (IOException e) { App.Logger.Warn(e, "Unable to delete temporary ZIP"); }
                }
            }

            if (status == false)
            {
                App.Logger.Info("Fail localization installation");
                if (!_navigationCanceled && MainWindow.UI.Phase == 8)
                    ReturnToSelection();

                return;
            }

            ProgBar.Value = ProgBar.Maximum;

            // The installer writes the custom language to user.cfg. GameSettings.Load() validates
            // it against system.cfg's sys_languages list and can remove it on some game installs.
            // A successful install should leave the patch enabled, including when repairing an
            // installation previously recorded as disabled.
            targetInstallation.IsEnabled = true;

            targetInstallation.InstalledVariant = actualVariant;
            targetInstallation.InstalledFeatures = actualFeatures?.ToList();
            RepositoryManager.SetInstallationRepository(targetInstallation);

            App.Logger.Info("Finish localization installation");
            MainWindow.UI.Phase++;
        }

        private static void ReturnToSelection()
        {
            MainWindow.UI.Phase = RepositoryManager.GetLocalizationSource().HasVariant ? 7 : 6;
        }

        private void Uninstall(GameInfo gameInfo, GameSettings gameSettings)
        {
            App.Logger.Info("Start localization uninstallation");

            var gameMode = App.SelectedGameMode;
            var targetInstallation = RepositoryManager.GetInstallationRepository(gameMode);
            var targetRepository = RepositoryManager.TargetRepository;

            if (targetInstallation == null || targetRepository == null)
            {
                App.Logger.Info("TargetInstallation or TargetRepository is not registered");

                return;
            }

            var status = false;

            try
            {
                var uninstallStatus = targetRepository.Installer.Uninstall(gameInfo.RootFolderPath);

                switch (uninstallStatus)
                {
                    case UninstallStatus.Success:

                        status = true;

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

            if (status == false)
            {
                MainWindow.UI.Phase = 5;
                return;
            }

            gameSettings.Load();

            ProgBar.Value = ProgBar.Minimum;

            RepositoryManager.RemoveInstallationRepository(targetInstallation);
            App.Logger.Info("Finish localization uninstallation");

            //MessageBox.Show(Properties.Resources.MSG_Desc_Uninstall);    //왜인진 몰라도 이거 빼면 frame_all content가 안 비워짐....

            MainWindow.UI.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                MainWindow.UI.Phase = 9;
            }));
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
            _downloadedSize = 0;
            UpdateDialogTaskInfo();
        }

        public void ReportDownloadedSize(long value)
        {
            _downloadedSize = value;
            UpdateDialogTaskInfo();
        }

        private void UpdateDialogTaskInfo()
        {
            long downloaded = _downloadedSize;
            long total = _totalContentSize;
            void Update()
            {
                float downloadSizeMBytes = (float)downloaded / (1024 * 1024);
                _dialog.ProgBar.IsIndeterminate = total <= 0;
                if (total > 0)
                {
                    _dialog.ProgBar.Value = downloaded * _dialog.ProgBar.Maximum / total;
                    float contentSizeMBytes = (float)total / (1024 * 1024);
                    _dialog.DescText.Content = $"{downloadSizeMBytes:0.00} MB/{contentSizeMBytes:0.00} MB";
                }
                else _dialog.DescText.Content = $"{downloadSizeMBytes:0.00} MB";
            }
            if (_dialog.Dispatcher.CheckAccess()) Update();
            else if (!_dialog.Dispatcher.HasShutdownStarted) _dialog.Dispatcher.BeginInvoke(new Action(Update));
        }
    }
}
