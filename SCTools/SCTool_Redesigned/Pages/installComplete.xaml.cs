using System.Windows.Controls;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// installComplete.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class installComplete : Page
    {
        public installComplete(MainWindow.InstallerMode mode)
        {
            InitializeComponent();

            switch (mode)
            {
                case MainWindow.InstallerMode.install:
                    PhaseNumber.Content = "04";
                    Phasetext.Content = "설치 완료";
                    App.Logger.Info("Localization installation complete.");
                    break;

                case MainWindow.InstallerMode.uninstall:
                    PhaseNumber.Content = "03";
                    Phasetext.Content = "제거 완료";
                    App.Logger.Info("Localization delete complete.");
                    break;

                case MainWindow.InstallerMode.disable:
                    PhaseNumber.Content = "03";
                    Phasetext.Content = "비활성화 완료";
                    App.Logger.Info("Localization disable complete.");
                    break;
            }

            //GoogleAnalytics.Hit(App.Settings.UUID, "/localization/install/finish", "install");
            //App.Logger.Info("Localization installation complete.");
        }
    }
}
