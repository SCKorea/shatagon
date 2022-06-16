using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using NSW.StarCitizen.Tools.Lib.Global;
using SCTool_Redesigned.Settings;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Windows
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static MainWindow UI;

        public enum InstallerMode { install, uninstall, disable };
        public enum MainBtnMode { install, update, launch };
        private MainBtnMode _MainBtnState;
        private InstallerMode _installmode;
        private int _PhaseNumber;
        private PrefaceWindow _prologue;
        private AuthWindow _author;
        private ImageBrush _mainBG, _subBG;

        public MainWindow()
        {
            UI = this;
            InitializeComponent();

            GoogleAnalytics.Session(App.Settings.UUID, "start", true);

            if (!App.IsGameInstalled())
            {
                MessageBox.Show(SCTool_Redesigned.Properties.Resources.MSG_Decs_NoInstall, SCTool_Redesigned.Properties.Resources.MSG_Title_NoInstall);
                App.Logger.Info("Installation infomation directory does not exist!");
            }

            Title += " - " + App.Version.ToString(3);
            _PhaseNumber = 0;
            _prologue = new PrefaceWindow();
            _author = new AuthWindow();
            set_link(App.Settings.GameLanguage);
            _mainBG = new ImageBrush();            _subBG = new ImageBrush();
            _mainBG.ImageSource = new BitmapImage(new Uri(@"pack://application:,,,/Shatagon;component/Resources/BG1.png"));
            //_mainBG.ImageSource = new BitmapImage(new Uri(@"/Resources/BG1.png", UriKind.Relative));
            _mainBG.Stretch = Stretch.UniformToFill;
            _subBG.ImageSource = new BitmapImage(new Uri(@"pack://application:,,,/Shatagon;component/Resources/BG0.png"));
            _subBG.Stretch = Stretch.UniformToFill;
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
                        //InstallBtn.Content = Properties.Resources.UI_Button_InstallLocalization;
                        UninstallBtn.Content = Properties.Resources.UI_Button_RemoveLocalization;
                        DisableBtn.Content = Properties.Resources.UI_Button_DisableLocalization;
                        Menu_patchnote.Text = Properties.Resources.UI_Tab_Main_ReleaseNote;
                        Menu_qna.Text = Properties.Resources.UI_Tab_Main_Qna;
                        Menu_credit.Text = Properties.Resources.UI_Tab_Main_Credit;

                        _author.AuthDescLabel.Text = Properties.Resources.UI_Title_Auth;
                        _author.ErrorLabel.Content = Properties.Resources.UI_Desc_AuthError;
                        _author.Applybtn.Content = Properties.Resources.UI_Button_AuthApply;
                        break;
                    case 2:
                        logoCanvas.SetValue(Grid.ColumnSpanProperty, 1);
                        logotitle.SetValue(Grid.ColumnSpanProperty, 1);
                        break;
                    case 3: //main Install
                        if (_PhaseNumber != value && RepositoryManager.GetLocalizationSource().IsPrivate && _installmode == 0) //Try auth for private repo
                        {
                            //Console.WriteLine($"Try auth at  {_PhaseNumber} to {value}");
                            _author.Owner = this;
                            if (_author.GetAuthToken() == null)
                                _author.ShowDialog();
                            if ((RepositoryManager.TargetRepository.AuthToken = RepositoryManager.GetLocalizationSource().AuthToken = _author.GetAuthToken()) == null) //Failed to auth
                            {
                                return; //cancel Phase progressing
                            }
                        }
                        break;
                    case 5:  //select Version
                        if (!RepositoryManager.IsAvailable())
                        {
                            //_logger.Error($"Install localization mode path unavailable: {CurrentGame.RootFolderPath}");
                            MessageBox.Show(Properties.Resources.MSG_Desc_InvalidAccess,
                                Properties.Resources.MSG_Title_GeneralError, MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        if (value == 6 && !App.Settings.AcceptInstallWarning)
                        {
                            var dialogResult = MessageBox.Show(Properties.Resources.MSG_Desc_InstallWarning,
                                Properties.Resources.MSG_Title_GeneralWarning, MessageBoxButton.YesNo,
                                MessageBoxImage.Warning, MessageBoxResult.Yes);
                            if (dialogResult != MessageBoxResult.Yes)
                            {
                                return;
                            }
                            App.Settings.AcceptInstallWarning = true;
                            App.SaveAppSettings();
                        }
                        break;
                }
                //Console.WriteLine($"Change Phase {_PhaseNumber} to {value}");
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
                        Background = _mainBG;
                        Show();

                        if (App.Settings.GameLanguage != null)
                        {
                            if (!RepositoryManager.SetTargetRepository())
                                MessageBox.Show(Properties.Resources.MSG_Desc_InvalidAccess, Properties.Resources.MSG_Title_GeneralError, MessageBoxButton.OK, MessageBoxImage.Error);
                            else
                                App.SaveAppSettings();
                            Phase = 3;
                            break;
                        }

                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectPatchLang();
                        logoCanvas.Visibility = Visibility.Visible;
                        logotitle.Visibility = Visibility.Visible;
                        logoCanvas.SetValue(Grid.ColumnSpanProperty, 2);
                        logotitle.SetValue(Grid.ColumnSpanProperty, 2);
                        logotitle.Content = Properties.Resources.UI_Desc_Welcome;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Hidden;
                        PrevBtn.Visibility = Visibility.Hidden;
                        Community_link1.IsEnabled = false; Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false; Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;  Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;     Menu_credit.Visibility = Visibility.Hidden;
                        break;

                    case 3: //main Install
                        Background = _mainBG;
                        frame_left.Content = null;
                        frame_right.Content = new Pages.mainNotes(2);
                        frame_all.Content = null;
                        logoCanvas.Visibility = Visibility.Visible;
                        logotitle.Visibility = Visibility.Visible;
                        logotitle.Content = Properties.Resources.UI_Title_ProgramTitle;
                        InstallBtn.Visibility = Visibility.Visible;
                        NextBtn.Visibility = Visibility.Hidden;
                        PrevBtn.Visibility = Visibility.Hidden;
                        Community_link1.IsEnabled = true; Community_link1.Visibility = Visibility.Visible;
                        Community_link2.IsEnabled = true; Community_link2.Visibility = Visibility.Visible;
                        Menu_patchnote.IsEnabled = true;  Menu_patchnote.Visibility = Visibility.Visible;
                        Menu_qna.IsEnabled = true;        Menu_qna.Visibility = Visibility.Visible;
                        Menu_credit.IsEnabled = true;     Menu_credit.Visibility = Visibility.Visible;
                        //Menu_credit_PreviewMouseLeftButtonDown(null,null);
                        Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                        Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                        Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];

                        if (IsLocalizationInstalled())
                        {
                            UninstallBtn.Visibility = Visibility.Visible;
                            DisableBtn.Visibility = Visibility.Visible;
                            Update_ToggleBtn();
                            SetInstallbtnLabel();
                            LauchTokenManager.Instance.UpdateLauchTokenManager(App.Settings.GameFolder+ "\\LIVE", App.LocalappDir);
                        }
                        break;

                    case 4: //select Dir
                        Background = _subBG;
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectDir();
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = (App.Settings.GameFolder == null) ? Visibility.Hidden : Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Next;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Previous;
                        Community_link1.IsEnabled = false; Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false; Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;  Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;     Menu_credit.Visibility = Visibility.Hidden;
                        break;

                    case 5: //select Version
                        Background = _subBG;
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectVersion();
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Install;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Previous;
                        Community_link1.IsEnabled = false; Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false; Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;  Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;     Menu_credit.Visibility = Visibility.Hidden;
                        break;

                    case 6: //installing?
                        Background = _subBG;
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.installProgress(_installmode);
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Hidden;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Cancel;
                        Community_link1.IsEnabled = false; Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false; Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;  Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;     Menu_credit.Visibility = Visibility.Hidden;
                        break;

                    case 7: //installComplete
                        Background = _subBG;
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.installComplete();
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Quit;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Return;
                        Community_link1.IsEnabled = false; Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false; Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;  Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;     Menu_credit.Visibility = Visibility.Hidden;

                        break;

                    case 8:
                        Application.Current.Shutdown();
                        break;

                    default:
                        throw new Exception(value.ToString() + " Phase is not exist");
                }
                //Console.WriteLine($"Change Phase {value} ended");
            }
        }

        private void set_link(string language)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                Community_link1.Source = Base64ToImage(Properties.Resources.UI_Button_Image_Community_1).Source;
                Community_link1.ToolTip = Properties.Resources.UI_Button_Tooltip_Community_1;
                Community_link2.Source = Base64ToImage(Properties.Resources.UI_Button_Image_Community_2).Source;
                Community_link2.ToolTip = Properties.Resources.UI_Button_Tooltip_Community_2;
            }));
        }

        private void Update_ToggleBtn() =>
            DisableBtn.Content = RepositoryManager.TargetInstallation.IsEnabled ? Properties.Resources.UI_Button_DisableLocalization : Properties.Resources.UI_Button_EnableLocalization;

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
            GoogleAnalytics.Session(App.Settings.UUID, "end", true);
            App.Close();
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

        private void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsRunGame())
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_TurnOffGame, Properties.Resources.MSG_Title_TurnOffGame);
            }
            if (_MainBtnState != MainBtnMode.launch)
            {
                _installmode = InstallerMode.install;
                Phase = 4;
            }
            else    //Launch Game
            {
                if (LauchTokenManager.Instance.LoadToken())    //TODO: update tokens
                {
                    App.RunGame();
                }
                else
                    MessageBox.Show(Properties.Resources.MSG_Title_GeneralError, Properties.Resources.MSG_Desc_InvalidAccess);  //FIXME:Update desc string...?
            }
        }

        private void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsRunGame())
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_TurnOffGame, Properties.Resources.MSG_Title_TurnOffGame);
            }

            _installmode = InstallerMode.uninstall;
            Phase = 6;
            MessageBox.Show(Properties.Resources.MSG_Desc_Uninstall);    //왜인진 몰라도 이거 빼면 frame_all content가 안 비워짐....
            Phase = 3;
        }

        private void DisableBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsRunGame())
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_TurnOffGame, Properties.Resources.MSG_Title_TurnOffGame);
            }

            _installmode = InstallerMode.disable;
            Phase = 6;
            if (RepositoryManager.TargetInstallation.IsEnabled)
                MessageBox.Show(Properties.Resources.MSG_Desc_Enable);
            else
                MessageBox.Show(Properties.Resources.MSG_Desc_Disable);
            Phase = 3;
        }

        private void Open_Community_1(object sender, MouseButtonEventArgs e) => Process.Start(Properties.Resources.UI_Button_Link_Community_1);

        private void Open_Community_2(object sender, MouseButtonEventArgs e) => Process.Start(Properties.Resources.UI_Button_Link_Community_2);

        private Image Base64ToImage(string image)
        {
            Image img = new Image();
            byte[] binaryData = Convert.FromBase64String(image);
            BitmapImage bi = new BitmapImage();

            bi.BeginInit();
            using (bi.StreamSource = new MemoryStream(binaryData))
            {
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();

                img.Source = bi;
            }

            return img;
        }

        private void Menu_patchnote_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
            Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
            Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];

            ((Pages.mainNotes)frame_right.Content).set_note(0);
        }

        private void Menu_qna_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
            Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
            Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];

            ((Pages.mainNotes)frame_right.Content).set_note(1);
        }

        private void Menu_credit_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
            Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
            Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];

            ((Pages.mainNotes)frame_right.Content).set_note(2);
        }
        private void Border_MouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Quit(object sender, RoutedEventArgs e)
        {
            Quit();
        }

        private bool IsLocalizationInstalled()
        {
            return App.Settings.LIVE_Localization.Installations.Count > 0 || App.Settings.PTU_Localization.Installations.Count > 0;
        }

        private void SetInstallbtnLabel()
        {
            Task.Run(() =>
            {
                var installed = 0;
                var isNewVersion = 0;

                var release = RepositoryManager.GetInfos(false);

                foreach (GameMode gameMode in Enum.GetValues(typeof(GameMode)))
                {
                    var data = App.Settings.GetGameModeSettings(gameMode);

                    if (data.Installations.Count > 0)
                    {
                        ++installed;

                        var patch = data.Installations.FirstOrDefault();
                        
                        if (release.Count() > 0 && !release.FirstOrDefault().Name.Equals(patch.InstalledVersion))
                        {
                            Debug.WriteLine(release.FirstOrDefault().Name);

                            ++isNewVersion;
                        }
                    }
                }
                _MainBtnState = MainBtnMode.install;

                if (installed > 0)
                {
                    LauchTokenManager.Instance.BeginWatch();
                    if (isNewVersion > 0)
                    {
                        _MainBtnState = MainBtnMode.update;
                    }
                    else
                    {
                        //text = Properties.Resources.UI_Button_ReInstallLocalization;
                        _MainBtnState = MainBtnMode.launch;
                    }
                }


                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
                {
                    switch (_MainBtnState)
                    {
                        case MainBtnMode.install:
                            InstallBtn.Content = Properties.Resources.UI_Button_InstallLocalization;
                            break;
                        case MainBtnMode.update:
                            InstallBtn.Content = Properties.Resources.UI_Button_UpdateLocalization;
                            break;
                        case MainBtnMode.launch:
                            InstallBtn.Content = Properties.Resources.UI_Button_LaunchGame;
                            break;
                    }
                }));
            });
        }
    }
}
