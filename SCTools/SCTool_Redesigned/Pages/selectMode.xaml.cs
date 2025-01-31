using System;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectDir.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectMode : Page
    {
        private bool _isPatchInstalled = true;

        public selectMode(MainWindow.InstallerMode mode)
        {
            App.SelectedGameMode = "";

            InitializeComponent();

            switch (mode)
            {
                case MainWindow.InstallerMode.install:
                    Phasetext.Content = "패치를 설치할 스타 시티즌 버전을 선택하세요.";
                    PhaseDesc.Content = "주의: 버전에 따라 한국어 패치가 호환되지 않을 수 있습니다.";
                    PhaseDesc.Visibility = Visibility.Visible;
                    PhasePath.Content = "한국어 패치를 적용할 버전을 선택 후 다음 버튼을 누르세요.";
                    _isPatchInstalled = false;

                    break;

                case MainWindow.InstallerMode.uninstall:
                    Phasetext.Content = "패치를 제거할 스타 시티즌 버전을 선택하세요.";
                    PhaseDesc.Visibility = Visibility.Hidden;
                    PhasePath.Content = "한국어 패치를 삭제할 버전을 선택 후 다음 버튼을 누르세요.";
                    break;

                case MainWindow.InstallerMode.disable:
                    Phasetext.Content = "패치를 비활성화활 스타 시티즌 버전을 선택하세요.";
                    PhaseDesc.Visibility = Visibility.Hidden;
                    PhasePath.Content = "한국어 패치를 비활성화할 버전을 선택 후 다음 버튼을 누르세요.";
                    break;
            }

            UpdateInstalledGameMode(_isPatchInstalled);
        }


        private void ChannelSelectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelSelectListBox.SelectedItem == null)
            {
                return;
            }

            var selected = ChannelSelectListBox.SelectedItem.ToString() ?? "";

            App.SelectedGameMode = ChannelSelectListBox.SelectedItem.ToString() ?? "";
            App.Logger.Info($"Selected {selected} folder");
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => UpdateInstalledGameMode(_isPatchInstalled);

        private void UpdateInstalledGameMode(bool onlyPatchInstalled)
        {
            var currentSelection = ChannelSelectListBox.SelectedItem;

            var modes = GetInstalledGameMode(onlyPatchInstalled);
            ChannelSelectListBox.ItemsSource = modes;


            if (currentSelection != null && modes.Contains(currentSelection.ToString() ?? ""))
            {
                ChannelSelectListBox.SelectedItem = currentSelection;

                return;
            }

            if (modes.Count > 0)
            {
                ChannelSelectListBox.SelectedIndex = 0;
            }
        }


        private static List<string> GetInstalledGameMode(bool onlyPatchInstalled)
        {
            App.Logger.Info("Find installed Game Folders");

            var path = App.Settings.GameFolder;
            var installedGameFolders = GameFolderManager.GetInstalledFolder(path);

            if (onlyPatchInstalled)
            {
                installedGameFolders = installedGameFolders.FindAll(folder =>
                {
                    return RepositoryManager.GetInstallationRepository(folder) != null;
                });
            }

            App.Logger.Info($"Found {installedGameFolders.Count} installed game folders");

            return installedGameFolders;
        }
    }
}
