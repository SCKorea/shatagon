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
using SCTool_Redesigned.Windows;

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
        }

        private void applyBtn_Click(object sender, RoutedEventArgs e)
        {
            ((MainWindow)Application.Current.MainWindow).Phase++;
        }
    }
}