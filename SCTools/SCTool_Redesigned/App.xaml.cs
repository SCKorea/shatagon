using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        App()
        {
            SCTool_Redesigned.Properties.Resources.Culture = new System.Globalization.CultureInfo("ko-KR");
            InitializeComponent();
        }
    }
}
