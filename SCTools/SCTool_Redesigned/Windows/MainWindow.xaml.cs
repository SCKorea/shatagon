using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Update;
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
        public enum MainBtnMode { install, update, launch, reinstall };
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

            if (!IsGameInstalled())
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_NoInstall, Properties.Resources.MSG_Title_NoInstall);
                App.Logger.Info("Game isn't installed.");
            }

            Title += " - " + App.Version?.ToString(4);
            _PhaseNumber = 0;
            _prologue = new PrefaceWindow();
            _author = new AuthWindow();
            _mainBG = new ImageBrush();
            _subBG = new ImageBrush();
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
                        SetLink(App.Settings.ToolLanguage);
                        if (_PhaseNumber != value && RepositoryManager.GetLocalizationSource().IsPrivate && _installmode == 0) //Try auth for private repo
                        {
                            //Console.WriteLine($"Try auth at  {_PhaseNumber} to {value}");
                            _author.Owner = this;

                            if (_author.GetAuthToken() == null)
                            {
                                _author.ShowDialog();
                            }

                            if (RepositoryManager.TargetRepository == null)
                            {
                                return;
                            }

                            string authToken = _author.GetAuthToken();

                            if (authToken == null)
                            {
                                return;
                            }

                            RepositoryManager.GetLocalizationSource().AuthToken = _author.GetAuthToken();
                            RepositoryManager.TargetRepository.AuthToken = _author.GetAuthToken();
                        }

                        break;


                    case 4: //select dir
                        if (string.IsNullOrWhiteSpace(App.Settings.GameFolder))
                        {
                            MessageBox.Show("게임이 설치된 폴더를 선택하십시오.", "게임 설치 폴더 선택", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        break;


                    case 5: //select game mode 
                        if (string.IsNullOrWhiteSpace(App.SelectedGameMode))
                        {
                            MessageBox.Show("설치된 게임 버전을 선택하십시오.", "설치된 게임 버전 선택", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        break;

                    case 6:  //select Version
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
                        if (string.IsNullOrWhiteSpace(App.Settings.ToolLanguage) == false)
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

                        if (string.IsNullOrWhiteSpace(App.Settings.GameLanguage) == false)
                        {

                            if (!RepositoryManager.SetTargetRepository())
                            {
                                MessageBox.Show(Properties.Resources.MSG_Desc_InvalidAccess, Properties.Resources.MSG_Title_GeneralError, MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                                App.SaveAppSettings();
                            }

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
                        Community_link1.IsEnabled = false;
                        Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false;
                        Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;
                        Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;
                        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;
                        Menu_credit.Visibility = Visibility.Hidden;

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
                        Community_link1.IsEnabled = true;
                        Community_link1.Visibility = Visibility.Visible;
                        Community_link2.IsEnabled = true;
                        Community_link2.Visibility = Visibility.Visible;
                        Menu_patchnote.IsEnabled = true;
                        Menu_patchnote.Visibility = Visibility.Visible;
                        Menu_qna.IsEnabled = true;
                        Menu_qna.Visibility = Visibility.Visible;
                        Menu_credit.IsEnabled = true;
                        Menu_credit.Visibility = Visibility.Visible;
                        Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                        Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                        Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];

                        SetInstallbtnLabel();

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
                        Community_link1.IsEnabled = false;
                        Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false;
                        Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;
                        Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;
                        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;
                        Menu_credit.Visibility = Visibility.Hidden;

                        break;

                    case 5: //selesct installed game mode
                        Background = _subBG;
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.selectMode(_installmode);
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = (App.Settings.GameFolder == null) ? Visibility.Hidden : Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Next;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Previous;
                        Community_link1.IsEnabled = false;
                        Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false;
                        Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;
                        Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;
                        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;
                        Menu_credit.Visibility = Visibility.Hidden;
                        break;

                    case 6: //select Version //old 5
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
                        Community_link1.IsEnabled = false;
                        Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false;
                        Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;
                        Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;
                        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;
                        Menu_credit.Visibility = Visibility.Hidden;

                        break;

                    case 7: //installing?
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
                        Community_link1.IsEnabled = false;
                        Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false;
                        Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;
                        Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;
                        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;
                        Menu_credit.Visibility = Visibility.Hidden;

                        break;

                    case 8: //installComplete
                        Background = _subBG;
                        frame_left.Content = null;
                        frame_right.Content = null;
                        frame_all.Content = new Pages.installComplete(_installmode);
                        logoCanvas.Visibility = Visibility.Hidden;
                        logotitle.Visibility = Visibility.Hidden;
                        InstallBtn.Visibility = Visibility.Hidden;
                        UninstallBtn.Visibility = Visibility.Hidden;
                        DisableBtn.Visibility = Visibility.Hidden;
                        NextBtn.Visibility = Visibility.Visible;
                        NextBtn.Text = Properties.Resources.UI_Button_Return;
                        PrevBtn.Visibility = Visibility.Visible;
                        PrevBtn.Text = Properties.Resources.UI_Button_Quit;
                        Community_link1.IsEnabled = false;
                        Community_link1.Visibility = Visibility.Hidden;
                        Community_link2.IsEnabled = false;
                        Community_link2.Visibility = Visibility.Hidden;
                        Menu_patchnote.IsEnabled = false;
                        Menu_patchnote.Visibility = Visibility.Hidden;
                        Menu_qna.IsEnabled = false;
                        Menu_qna.Visibility = Visibility.Hidden;
                        Menu_credit.IsEnabled = false;
                        Menu_credit.Visibility = Visibility.Hidden;

                        break;

                    case 9:
                        Application.Current.Shutdown();

                        break;

                    default:
                        throw new Exception(value.ToString() + " Phase is not exist");
                }
                //Console.WriteLine($"Change Phase {value} ended");
            }
        }

        private void SetLink(string language)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                Community_link1.Source = Base64ToImage(Properties.Resources.UI_Button_Image_Community_1).Source;
                Community_link1.ToolTip = Properties.Resources.UI_Button_Tooltip_Community_1;
                Community_link2.Source = Base64ToImage(Properties.Resources.UI_Button_Image_Community_2).Source;
                Community_link2.ToolTip = Properties.Resources.UI_Button_Tooltip_Community_2;
            }));
        }
        private void NextBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            switch (Phase)
            {
                case 5:
                    if (_installmode == InstallerMode.install)
                    {
                        Phase++;
                        break;
                    }

                    Phase = 7;
                    break;

                case 7:
                    if (_installmode == InstallerMode.install)
                    {
                        Phase++;
                        break;
                    }

                    Phase = 8;
                    break;

                case 8:
                    Phase = 3;
                    break;

                default:
                    Phase++;
                    break;
            }
        }

        private void PrevBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            switch (Phase)
            {
                case 5:
                    if (_installmode == InstallerMode.install)
                    {
                        Phase--;
                        break;
                    }

                    Phase = 4;
                    break;

                case 7:
                    if (_installmode == InstallerMode.install)
                    {
                        Phase--;
                        break;
                    }

                    Phase = 5;
                    break;

                case 8:
                    Phase = 3;
                    break;

                default:
                    Phase--;
                    break;
            }
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
            else
            {
                App.RunLauncher();
            }
        }

        private void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsRunGame())
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_TurnOffGame, Properties.Resources.MSG_Title_TurnOffGame);
            }

            _installmode = InstallerMode.uninstall;
            Phase = 4;
        }

        private void DisableBtn_Click(object sender, RoutedEventArgs e)
        {
            // TODO

            /*
            if (App.IsRunGame())
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_TurnOffGame, Properties.Resources.MSG_Title_TurnOffGame);
            }

            _installmode = InstallerMode.disable;
            Phase = 6;
            if (IsLocalizationInstalled())
                MessageBox.Show(Properties.Resources.MSG_Desc_Enable);
            else
                MessageBox.Show(Properties.Resources.MSG_Desc_Disable);
            Phase = 3;
            */
        }

        private void Open_Community_1(object sender, MouseButtonEventArgs e) => Process.Start(new ProcessStartInfo
        {
            FileName = Properties.Resources.UI_Button_Link_Community_1,
            UseShellExecute = true
        });

        private void Open_Community_2(object sender, MouseButtonEventArgs e) => Process.Start(new ProcessStartInfo
        {
            FileName = Properties.Resources.UI_Button_Link_Community_2,
            UseShellExecute = true
        });

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

        private void SetInstallbtnLabel()
        {
            Task.Run(() =>
            {
                List<string> installed = [];
                List<string> update = [];
                List<string> mismatch = [];
                var release = RepositoryManager.GetInfos(false);
                var gameFolder = App.Settings.GameFolder;
                
                List<string> installedGameFolders = [];

                if (!string.IsNullOrEmpty(gameFolder))
                {
                    installedGameFolders = GameFolderManager.GetInstalledFolder(gameFolder);
                }

                App.Logger.Info("Installed Game Mode");
                App.Logger.Info(string.Join(", ", [.. installedGameFolders]));

                var installData = App.Settings.GetLocalizationSettings();
                var releasedVersion = release.FirstOrDefault();

                foreach (var mode in installedGameFolders)
                {
                    var installation = installData.Installations.Find(installation => installation.Mode == mode);

                    if (installation == null)
                    {
                        // Registering patch that actually exist but are not registered
                        var userCfgPath = Path.Combine(gameFolder, mode, "user.cfg");

                        if (!PatchLanguageManager.IsEnabled(userCfgPath))
                        {
                            continue;
                        }

                        installation = new LocalizationInstallation(mode, "", UpdateRepositoryType.GitHub)
                        {
                            IsEnabled = true
                        };

                        RepositoryManager.SetInstallationRepository(installation);
                        App.SaveAppSettings();
                    }

                    var installedVersion = installation.InstalledVersion;

                    if (releasedVersion != null && !installedVersion.Equals(releasedVersion.Name))
                    {
                        update.Add(mode);
                    }

                    var LanguageName = App.Settings.GetOfficialLanauages()[App.Settings.GameLanguage];
                    var localizationFile = Path.Combine(gameFolder, mode, "data", "Localization", LanguageName, "global.ini");
                    var userConfig = Path.Combine(gameFolder, mode, "user.cfg");
                    var existLocalizationFile = File.Exists(localizationFile);
                    var existUserConfig = File.Exists(userConfig);
                    var isEnablePatchUserConfig = false;

                    if (existUserConfig)
                    {
                        isEnablePatchUserConfig = PatchLanguageManager.IsEnabled(userConfig);
                    }

                    if (installation.IsEnabled && isEnablePatchUserConfig)
                    {
                        if (existLocalizationFile == true)
                        {
                            installed.Add(mode);
                        }

                        if (existLocalizationFile == false)
                        {
                            mismatch.Add(mode);
                        }
                    }
                }

                App.Logger.Info("Installation Status");
                App.Logger.Info($"Installed: {installed.Count} | Mismatch: {mismatch.Count} | Update: {update.Count}");
                

                _MainBtnState = MainBtnMode.install;

                if (installedGameFolders.Count == installed.Count && installed.Count > 0 && mismatch.Count == 0 && update.Count == 0)
                {
                    _MainBtnState = MainBtnMode.launch;
                }

                if (mismatch.Count > 0)
                {
                    App.Logger.Info($"Mismatch: {string.Join(", ", [.. mismatch])}");

                    _MainBtnState = MainBtnMode.reinstall;
                }

                if (update.Count > 0)
                {
                    App.Logger.Info($"Update: {string.Join(", ", [.. update])}");

                    _MainBtnState = MainBtnMode.update;
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
                        case MainBtnMode.reinstall:
                            InstallBtn.Content = Properties.Resources.UI_Button_ReInstallLocalization;
                            break;
                    }

                    if (installed.Count > 0)
                    {
                        UninstallBtn.Visibility = Visibility.Visible;
                        //DisableBtn.Visibility = Visibility.Visible; //TODO
                    }
                }));
            });
        }

        private bool IsGameInstalled()
        {
            return !string.IsNullOrEmpty(GameLauncherManager.GetInstalledPath()) && Directory.Exists(App.LocalappDir);
        }

    }
}
