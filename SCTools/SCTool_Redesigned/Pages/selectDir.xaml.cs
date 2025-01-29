using System;
using System.Dynamic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using NSW.StarCitizen.Tools.Lib.Global;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectDir.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectDir : Page
    {
        private const string Defaultdir = @"C:\Program Files\Roberts Space Industries\StarCitizen";
        public selectDir()
        {
            InitializeComponent();

            if (!IsCorrectGameFolder(App.Settings.GameFolder ?? ""))
            {
                App.Logger.Warn("StarCitizen p4k File does not exist. Maybe not installed?");

                App.Settings.GameFolder = "";
                App.SaveAppSettings();
            }
        }

        private static bool IsInstalledGame(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return false;
                }

                DirectoryInfo parentFolder = new DirectoryInfo(path);

                if (!parentFolder.Exists)
                {
                    return false;
                }

                DirectoryInfo[] gameFolders = parentFolder.GetDirectories();

                foreach (var folder in gameFolders)
                {
                    if (folder.GetFiles("Data.p4k").FirstOrDefault() != null)
                    {
                        return true;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                App.Logger.Info("Folder Access Denied");

                return false;
            }
            
            return false;
        }

        private bool IsCorrectGameFolder(string path)
        {
            App.Logger.Info("Check game folder path");

            if (IsInstalledGame(path) == false)
            {
                return false;
            }

            var verifyedPath = GameFolders.SearchGameFolder(path);

            if (verifyedPath == null)
            {
                App.Logger.Info("Invalid game folder path");

                return false;
            }

            App.Settings.GameFolder = verifyedPath;
            App.SaveAppSettings();

            PhasePath.Content = verifyedPath;

            MainWindow.UI.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                MainWindow.UI.NextBtn.Visibility = Visibility.Visible;
            }));

            App.Logger.Info("Valid game folder path");

            return true;
        }

        private void dialogBtn_Click(object sender, RoutedEventArgs e)
        {
            var flag = true;

            while (flag)
            {
                var dialog = OpenDialog();
                App.Logger.Info("Open game folder selection dialog");

                var result = dialog.ShowDialog();

                if (result != true)
                {
                    App.Logger.Info("Close game folder selection dialog");
                    break;
                }

                App.Logger.Info("Close game folder selection dialog");

                var directoryPath = dialog.FolderName;

                if (flag = !IsCorrectGameFolder(directoryPath))
                {
                    MessageBox.Show(Properties.Resources.MSG_Decs_NotGameFolder, Properties.Resources.MSG_Title_NotGameFolder);
                    App.Logger.Warn("Not a game folder.");
                }
            }
        }

        private static OpenFolderDialog OpenDialog() => new()
        {
            Title = Properties.Resources.MSG_Title_SelectStarCitizenDirectory,
            InitialDirectory = @"C:\Program Files\Roberts Space Industries"
        };
    }
}
