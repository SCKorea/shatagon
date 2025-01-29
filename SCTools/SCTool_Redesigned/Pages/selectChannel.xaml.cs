using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using NSW.StarCitizen.Tools.Lib.Global;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectDir.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectChannel : Page
    {
        private const string Defaultdir = @"C:\Program Files\Roberts Space Industries\StarCitizen";
        public selectChannel()
        {
            App.Settings.SelectedGameVersion = "";

            InitializeComponent();
            UpdateInstalledChannel();
        }


        private void ChannelSelectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChannelSelectListBox.SelectedItem == null)
            {
                return;
            }

            var selected = ChannelSelectListBox.SelectedItem.ToString() ?? "";

            App.Settings.SelectedGameVersion = ChannelSelectListBox.SelectedItem.ToString() ?? "";
            App.Logger.Info($"Selected {selected} folder");
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => UpdateInstalledChannel();

        private void UpdateInstalledChannel()
        {
            var currentSelection = ChannelSelectListBox.SelectedItem;

            // 버전 목록 가져오기
            var chennels = GetInstalledGameChannel();
            ChannelSelectListBox.ItemsSource = chennels;

            // 이전 선택 항목 복원
            if (currentSelection != null && chennels.Contains(currentSelection.ToString() ?? ""))
            {
                ChannelSelectListBox.SelectedItem = currentSelection;
            }
            else if (chennels.Count > 0)
            {
                ChannelSelectListBox.SelectedIndex = 0;
            }
        }


        private List<string> GetInstalledGameChannel()
        {
            App.Logger.Info("Find installed Game Folders");

            string path = App.Settings.GameFolder;

            DirectoryInfo parentFolder = new DirectoryInfo(path);
            DirectoryInfo[] gamefolders = parentFolder.GetDirectories();

            List<string> installedGameFolders = [];

            foreach (var innerFolder in gamefolders)
            {
                if(innerFolder.GetFiles("Data.p4k").FirstOrDefault() == null)
                {
                    continue;
                }

                installedGameFolders.Add(innerFolder.Name);
            }

            App.Logger.Info($"Found {gamefolders.Length} folders");

            return installedGameFolders;
        }
    }
}
