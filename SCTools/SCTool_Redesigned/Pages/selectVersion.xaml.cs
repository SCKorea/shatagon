using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SCTool_Redesigned.Utils;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectVersion.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectVersion : Page
    {
        public selectVersion()
        {
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

            if (version != null)
            {
                var verString = VersionSelectListBox.SelectedValue.ToString();
                RepositoryManager.SetInstallationTarget(verString, verString, (UpdateInfo)VersionSelectListBox.SelectedItem);
            }
        }
    }
}
