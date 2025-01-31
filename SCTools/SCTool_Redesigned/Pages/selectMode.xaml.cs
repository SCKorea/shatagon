using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using NSW.StarCitizen.Tools.Lib.Global;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectDir.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectMode : Page
    {
        private const string Defaultdir = @"C:\Program Files\Roberts Space Industries\StarCitizen";
        public selectMode()
        {
            App.SelectedGameMode = "";

            InitializeComponent();
            UpdateInstalledGameMode();
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

        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => UpdateInstalledGameMode();

        private void UpdateInstalledGameMode()
        {
            var currentSelection = ChannelSelectListBox.SelectedItem;

            var modes = GetInstalledGameMode();
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


        private List<string> GetInstalledGameMode()
        {
            App.Logger.Info("Find installed Game Folders");

            var path = App.Settings.GameFolder;
            var installedGameFolders = GameFolderManager.GetInstalledFolder(path);

            App.Logger.Info($"Found {installedGameFolders.Count} installed game folders");

            return installedGameFolders;
        }
    }
}
