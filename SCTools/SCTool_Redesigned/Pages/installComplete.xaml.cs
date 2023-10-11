using System.Windows.Controls;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// installComplete.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class installComplete : Page
    {
        public installComplete()
        {
            InitializeComponent();

            GoogleAnalytics.Hit(App.Settings.UUID, "/localization/install/finish", "install");
            App.Logger.Info("Localization installation complete.");
        }
    }
}
