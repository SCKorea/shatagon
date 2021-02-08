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
        private int _PhaseNumber;
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
            _PhaseNumber = 0;
            frame_right.Content = new Pages.mainNotes();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("HI!");
            //this.Close();
        }
        public int Phase
        {
            get { return _PhaseNumber; }
            set
            {
                switch (value)
                {
                    case 0: //select patch Language
                        InstallBtn.IsEnabled = false;
                        UninstallBtn.IsEnabled = false;
                        NextBtn.IsEnabled = false;
                        PrevBtn.IsEnabled = false;
                        break;
                    case 1: //main Install
                        
                    case 2: //select Dir
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectDir();
                        InstallBtn.IsEnabled = false;
                        UninstallBtn.IsEnabled = false;
                        NextBtn.IsEnabled = true;
                        PrevBtn.IsEnabled = true;
                        break;
                    case 3:
                    
                    default: throw new Exception(value.ToString()+" Phase is not exist");
                }
            }
        }
    }
}
