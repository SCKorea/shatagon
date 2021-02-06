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

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// updateProgress.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class updateProgress : Page
    {
        private void TryUpdateLauncher()
        {
            
            //TODO: 런쳐 업데이트
            for (int i = 1; i <= 100; i++)
            {
                ProgBar.Value = i;
            }
        }
        public updateProgress()
        {
            InitializeComponent();
            TryUpdateLauncher();
        }
    }
}
