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
using SCTool_Redesigned.Settings;
using SCTool_Redesigned.Windows;
using SCTool_Redesigned.groceries;
namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectPatchLang.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectPatchLang : Page
    {

        public selectPatchLang()
        {
            InitializeComponent();
            LocalizationListBox.ItemsSource = RepositoryManager.GetLocalizationList();
        }

        private void applyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.Settings.GameLanguage == null)
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_SelectLocalization, Properties.Resources.MSG_Title_SelectLocalization);
                return;
            }

            MainWindow.UI.Phase++;
        }
        

        private void LocalizationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LocalizationListBox.SelectedValue is string language)
            {
                App.Settings.GameLanguage = language;
                App.SaveAppSettings();
            }
        }
    }
}
