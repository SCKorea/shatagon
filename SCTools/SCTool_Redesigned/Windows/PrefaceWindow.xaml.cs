using System;
using System.Windows;

namespace SCTool_Redesigned.Windows
{
    /// <summary>
    /// preface.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PrefaceWindow : Window
    {
        public PrefaceWindow()
        {
            InitializeComponent();

            Closed += PrefaceWindow_Closed;
        }

        private void PrefaceWindow_Closed(object sender, EventArgs e)
        {
            if (!MainWindow.UI.DoNotCloseMainWindow)
            {
                MainWindow.UI.Quit();
            }
        }
    }
}
