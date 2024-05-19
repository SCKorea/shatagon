using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using SoftCircuits.IniFileParser;

namespace SCTool_Redesigned.Utils
{
    public static class PatchLanguageManager
    {
        private const string DEFAULT = "english";

        public static void Enable(string path, string language) => SetLanguage(path, language);

        public static void Disable(string path) => SetLanguage(path, DEFAULT);

        public static bool IsEnabled(string path)
        {

            if (!File.Exists(path))
            {
                return false;
            }

            var config = GetConfigParser(path);
            var language = config.GetSetting("Localization", "g_language");

            return !string.IsNullOrEmpty(language) && language != DEFAULT;
        }

        private static IniFile GetConfigParser(string path)
        {
            IniFile ini = new();
            ini.Load(path);

            return ini;
        }

        private static void SetLanguage(string path, string language)
        {
            var config = GetConfigParser(path);

            config.SetSetting("Localization", "g_languageAudio", DEFAULT);
            config.SetSetting("Localization", "g_language", language);

            config.Save(path);
        }
    }
}
