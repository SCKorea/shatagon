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
using System.Windows.Threading;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// installProgress.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class installProgress : Page
    {
        public installProgress()
        {
            InitializeComponent();
            Progressbar_demo();
        }
        private DispatcherTimer timer1;
        private void Progressbar_demo()
        {
            installPbr.Value = 0;
            timer1 = new DispatcherTimer();
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = TimeSpan.FromMilliseconds(30);
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            installPbr.Value += 5;
            if (installPbr.Value == installPbr.Maximum)
            {
                timer1.Stop();
                ((Windows.MainWindow)Application.Current.MainWindow).Phase++;
            }
        }
    }
}
