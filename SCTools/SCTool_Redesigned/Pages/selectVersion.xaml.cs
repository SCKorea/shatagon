using System.Windows;
using System.Windows.Controls;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectVersion.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectVersion : Page
    {
        public selectVersion()
        {
            App.Logger.Info("Opens the game localization version selection window.");

            InitializeComponent();
            //VersionSelectListBox.ItemsSource = RepositoryManager.GetReleaseVersions();
            VersionSelectListBox.ItemsSource = RepositoryManager.GetInfos(false);
            VersionSelectListBox.SelectedIndex = 0;

            GoogleAnalytics.Hit(App.Settings.UUID, "/localization/install/version", "Select Localization Version");
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            VersionSelectListBox.ItemsSource = RepositoryManager.GetInfos(false);
            VersionSelectListBox.SelectedIndex = 0;
        }


        private void VersionSelectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var version = VersionSelectListBox.SelectedValue;

            if (version == null)
            {
                return;
            }

            var versionName = VersionSelectListBox.SelectedValue.ToString();
            var gameMode = App.SelectedGameMode;

            if (string.IsNullOrEmpty(versionName) || string.IsNullOrEmpty(gameMode))
            {
                App.Logger.Info("Not found version or game mode");
                return;
            }

            RepositoryManager.SetTargetInstallation(gameMode, versionName, (UpdateInfo)VersionSelectListBox.SelectedItem);
            App.Logger.Info("Game localization version selected.");
        }
    }
}
