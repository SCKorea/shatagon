using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Threading;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Windows
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static MainWindow UI;

        private int _PhaseNumber;
        private PrefaceWindow _prologue;
        private AuthWindow _author;

        
        public MainWindow()
        {
            UI = this;
            InitializeComponent();
            _PhaseNumber = 0;
            _prologue = new PrefaceWindow();
            _author = new AuthWindow();
            Phase = 0;
        }

        public int Phase
        {
            get { return _PhaseNumber; }
            set
            {
                switch (_PhaseNumber)   //required some works before Phase progression
                {
                    case 1:     //select laucher language
                        //refresh with new UI language
                        logotitle.Content = Properties.Resources.UI_Title_ProgramTitle;
                        PrevBtn.Text = Properties.Resources.UI_Button_Previous;
                        NextBtn.Text = Properties.Resources.UI_Button_Next;
                        InstallBtn.Content = Properties.Resources.UI_Button_InstallLocalization;
                        UninstallBtn.Content = Properties.Resources.UI_Button_RemoveLocalization;
                        DisableBtn.Content = Properties.Resources.UI_Button_DisableLocalization;
                        WelcomeText.Content = Properties.Resources.UI_Desc_Welcome;
                        _author.AuthDescLabel.Text = Properties.Resources.UI_Title_Auth;
                        _author.ErrorLabel.Content = Properties.Resources.UI_Desc_AuthError;
                        _author.Applybtn.Content = Properties.Resources.UI_Button_AuthApply;
                        break;
                    case 3: //main Install
                        if (RepositoryManager.GetLocalizationSource().IsPrivate) //Try auth for private repo
                        {
                            _author.Owner = this;
                            if (_author.GetAuthToken() == null)
                                _author.ShowDialog();
                            if ((RepositoryManager.GetLocalizationSource().AuthToken = _author.GetAuthToken()) == null) //Failed to auth
                            {
                                return; //cancel Phase progressing
                            }
                        }
                        break;
                    case 5:  //select Version
                        break;
                }
                _PhaseNumber = value;
                switch (value)
                {
                    case 0:     //launcher update
                        Hide();
                        _prologue.Content = new Pages.updatePatcher();
                        _prologue.Show();
                        break;

                    case 1:     //select laucher language
                        if (App.Settings.ToolLanguage != null)
                        {
                            Phase = 2;
                            break;
                        }

                        Hide();
                        _prologue.Content = new Pages.selectLang();
                        _prologue.Show();
                        break;
                        
                    case 2: //select patch Language
                        DoNotCloseMainWindow = true;
                        _prologue.Close();
                        Show();

                        if (App.Settings.GameLanguage != null)
                        {
                            Phase = 3;
                            break;
                        }

                        frame_left.Content = null;
                        frame_right.Content = new Pages.selectPatchLang();
                        frame_all.Content = null;
                        logoCanvas.Visibility = Visibility.Visible;
                        logotitle.Visibility = Visibility.Visible;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        WelcomeText.Visibility = Visibility.Visible;
                        NextBtn.Visibility = Visibility.Hidden;
                        PrevBtn.Visibility = Visibility.Hidden;
                        break;

                    case 3: //main Install
                        frame_left.Content = null;
                        frame_right.Content = new Pages.mainNotes();
                        frame_all.Content = null;
                        logoCanvas.Visibility = Visibility.Visible;
                        logotitle.Visibility = Visibility.Visible;
                        InstallBtn.Visibility = Visibility.Visible;
                        UninstallBtn.Visibility = Visibility.Visible;
                        WelcomeText.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Hidden;
                        PrevBtn.Visibility = Visibility.Hidden;
                        break;

                    case 4: //select Dir
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectDir();
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        WelcomeText.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = (App.Settings.GameFolder == null) ? Visibility.Hidden : Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Next;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Previous;
                        break;

                    case 5: //select Version
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectVersion();
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        WelcomeText.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Install;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Previous;
                        break;

                    case 6: //installing?
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.installProgress(0);
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        WelcomeText.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Hidden;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Cancel;
                        break;

                    case 7: //installComplete
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.installComplete();
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        WelcomeText.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Quit;

                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Return;
                        break;

                    case 8:
                        Application.Current.Shutdown();
                        break;

                    default: throw new Exception(value.ToString()+" Phase is not exist");
                }
                Console.WriteLine($"Change Phase to {_PhaseNumber}");
            }
        }

        private void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            Phase = 4;
        }

        private void NextBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Phase++;
        }

        private void PrevBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Phase != 7)
                Phase--;
            else
                Phase = 3;
        }

        internal bool DoNotCloseMainWindow = false;

        internal void Quit()
        {
            Dispatcher.Invoke(DispatcherPriority.Normal, new Action(delegate
            {
                //Process.GetCurrentProcess().Kill();
                _author.Close();
                Application.Current.Shutdown();
            }));
        }

        private void LazePageLoad(Frame frame, Page page)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                frame.Content = page;
            }));
        }

        private void Quit(object sender, EventArgs e)
        {
            Quit();
        }
    }
}
