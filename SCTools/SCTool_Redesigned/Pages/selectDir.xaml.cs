using System;
using System.Collections.Generic;
using System.IO;
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
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectDir.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectDir : Page
    {
        public selectDir()
        {
            InitializeComponent();

            if (App.Settings.GameFolder != null)
            {
                PhasePath.Content = App.Settings.GameFolder;
            }
        }

        private void dialogBtn_Click(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                CommonOpenFileDialog dialog = OpenDialog();

                var result = dialog.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    string directoryPath = dialog.FileName;
                    DirectoryInfo directoryInfo = new DirectoryInfo(directoryPath);

                    if (directoryInfo.Name.Equals("StarCitizen"))
                    {
                        if (directoryInfo.GetDirectories().Any(dir => dir.Name.Equals("LIVE") || dir.Name.Equals("PTU")))
                        {
                            App.Settings.GameFolder = directoryPath;
                            App.SaveAppSettings();

                            PhasePath.Content = directoryPath;

                            MainWindow.UI.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
                            {
                                MainWindow.UI.NextBtn.Visibility = Visibility.Visible;
                            }));

                            break;
                        }
                    }

                    MessageBox.Show(Properties.Resources.MSG_Decs_NotGameFolder, Properties.Resources.MSG_Title_NotGameFolder);
                }
                else
                {
                    break;
                }
            }
        }

        private CommonOpenFileDialog OpenDialog()
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = Properties.Resources.MSG_Title_SelectStarCitizenDirectory,
                InitialDirectory = @"C:\Program Files\Roberts Space Industries"
            };

            return dialog;
        }
    }
}
