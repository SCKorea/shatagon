using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using NSW.StarCitizen.Tools.Lib.Helpers;
using SCTool_Redesigned.Settings;
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
            SCTool_Redesigned.Properties.Resources.Culture = CultureInfo.GetCultureInfo(Settings.ToolLanguage ?? CultureInfo.CurrentCulture.Name);
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
        private static bool SaveAppSettings(AppSettings settings)
        {
            var executableDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            return JsonHelper.WriteFile(Path.Combine(executableDir, AppSettingsFileName), settings);
        }

    }
}
