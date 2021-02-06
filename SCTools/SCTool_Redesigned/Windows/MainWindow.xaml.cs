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
using System.Windows.Shapes;

namespace SCTool_Redesigned.Windows
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Hide();
            Windows.PrefaceWindow preface = new PrefaceWindow();
            preface.Show();
            //preface.startUpdate();
            preface.startLang();
            //preface.Hide();
            this.Show();
            frame_right.Content = new Pages.mainNotes();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("HI!");
            //this.Close();
        }
    }
}
