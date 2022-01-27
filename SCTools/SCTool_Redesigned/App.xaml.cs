using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Global;
using SCTool_Redesigned.Settings;
using SCTool_Redesigned.Windows;
using System.Windows.Controls;
using System.Net.NetworkInformation;
using SCTool_Redesigned.Utils;
using Microsoft.Win32;

namespace SCTool_Redesigned
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        public static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        App()
        {
            if (Settings.Console)
            {
                ConsoleManager.Show();
            }

            Logger.Info("Shataogn Patcher");
            Logger.Info("version: " + typeof(App).Assembly.GetName().Version);

            SCTool_Redesigned.Properties.Resources.Culture = CultureInfo.GetCultureInfo(Settings.ToolLanguage ?? CultureInfo.CurrentCulture.Name);

            if (!IsOnline())
            {
                MessageBox.Show(SCTool_Redesigned.Properties.Resources.MSG_Decs_NoInternet, SCTool_Redesigned.Properties.Resources.MSG_Title_NoInternet);
                Close();

                return;
            }

            if (Settings.UUID == null)
            {
                Settings.UUID = Guid.NewGuid().ToString();
                SaveAppSettings();
            }

            InitializeComponent();
        }

        private const string AppSettingsFileName = "settings.json";

        private static AppSettings _appSettings;
        public static AppSettings Settings => GetAppSettings();

        private static AppSettings GetAppSettings()
        {
            if (_appSettings == null)
            {
                var executableDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                _appSettings = JsonHelper.ReadFile<AppSettings>(Path.Combine(executableDir, AppSettingsFileName)) ?? new AppSettings();
            }

            return _appSettings;
        }

        public static bool SaveAppSettings() => SaveAppSettings(Settings);
        public static bool SaveAppSettings(AppSettings settings)
        {
            //var executableDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName); //save to excutabledir
            //return JsonHelper.WriteFile(Path.Combine(executableDir, AppSettingsFileName), settings);
            return JsonHelper.WriteFile(Path.Combine(LocalappDir, AppSettingsFileName), settings);
        }

        public static bool IsRunGame()
        {
            Process[] pname = Process.GetProcessesByName("StarCitizen");

            return pname.Length > 0;
        }

        //from Program.Global
        public static GameInfo CurrentGame { get; set; }

        public static string Name { get; } = Assembly.GetExecutingAssembly().GetName().Name;

        public static Version Version { get; } = Assembly.GetExecutingAssembly().GetName().Version;

        public static string ExecutableDir { get; } = GetExecutableDir();

        public static string LocalappDir { get; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Star Citizen\\";

        public static void Close()
        {
            if (!ConsoleManager.HasConsole)
            {
                Current.Shutdown();
            }

            for (int intCounter = Current.Windows.Count - 1; intCounter >= 0; intCounter--)
            {
                Current.Windows[intCounter].Close();
            }

            Logger.Info("Program is terminated.");
        }

        private static string GetExecutableDir()
        {
            var location = new Uri(Assembly.GetExecutingAssembly().GetName().CodeBase);
            var dirInfo = new FileInfo(location.LocalPath).Directory;
            if (dirInfo == null)
                throw new NullReferenceException("No assembly executable directory");
            return dirInfo.FullName;
        }

        private static bool IsOnline()
        {
            Logger.Info("Check Internet connection.");

            var pass = new Ping().Send("1.1.1.1", 1000).Status.Equals(IPStatus.Success);

            if (!pass)
            {
                Logger.Info("ICMP port is not available, Try different way...");

                try
                {
                    pass = HttpNetClient.Client.GetStringAsync("https://api.ipify.org").Result.Length > 0;

                } catch (Exception ex)
                {
                    Logger.Warn("FAIL: Internet unavailable.");

                    pass = false;
                }
                
            }

            Logger.Info("SUCCESS: Internet available.");
            return pass;
        }
    }
}
