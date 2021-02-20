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
using System.Windows.Forms;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.groceries;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// updatePatcher.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class updatePatcher : Page
    {
        private static ApplicationUpdater _updater = new ApplicationUpdater(GetUpdateRepository(), App.ExecutableDir, "[PH]");

        public updatePatcher()
        {
            InitializeComponent();
            Progressbar_demo();
        }
        private DispatcherTimer timer1;
        private void Progressbar_demo()
        {
            ProgBar.Value = 0;
            timer1 = new DispatcherTimer();
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = TimeSpan.FromMilliseconds(30);
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ProgBar.Value += 5;
            if (ProgBar.Value == ProgBar.Maximum)
            {
                timer1.Stop();
                ((Windows.MainWindow)Application.Current.MainWindow).Phase++;
            }
        }
        private bool TryUpdate()
        {
            var scheduledUpdateInfo = _updater.GetScheduledUpdateInfo();
            if (scheduledUpdateInfo != null)
            {
                if (_updater.IsAlreadyInstalledVersion(scheduledUpdateInfo))
                {
                    _updater.CancelScheduleInstallUpdate();
                    return false;
                }
                return InstallScheduledUpdate();
            }
            return false;
        }

        private static bool InstallScheduledUpdate()
        {
            var result = _updater.InstallScheduledUpdate();
            if (result != InstallUpdateStatus.Success)
            {
                //_logger.Error($"Failed launch install update: {result}");
                MessageBox.Show("업데이트 실패" + @" - " + result.ToString("d"), App.Name); //FOR DEBUG
                return false;
            }
            return true;
        }

        private static IUpdateRepository GetUpdateRepository()
        {
            var updateInfoFactory = GitHubUpdateInfo.Factory.NewWithVersionByTagName();
            var updateRepository = new GitHubUpdateRepository(HttpNetClient.Client,
                GitHubDownloadType.Assets, updateInfoFactory, App.Name, "marona42/StarCitizen");
            updateRepository.SetCurrentVersion(App.Version.ToString(3));
            return updateRepository;
        }
    }
}
