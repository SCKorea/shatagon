using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using Salaros.Configuration;

namespace SCTool_Redesigned.Utils
{
    public static class PatchLanguageManager
    {
        private const string DEFAULT = "english";

        public static bool Enable(string path, string language)
        {
            return SetLanguage(path, language);
        }

        public static bool Disable(string path)
        {
            return SetLanguage(path, DEFAULT);
        }

        public static bool IsEnabled(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var config = GetConfigParser(path);
            var language = config.GetValue("Localization", "g_language");

            return !string.IsNullOrEmpty(language) && language != DEFAULT;
        }

        private static ConfigParser GetConfigParser(string path)
        {
            var setting = new ConfigParserSettings
            {
                MultiLineValues = MultiLineValues.AllowEmptyTopSection
            };

            return new ConfigParser(path, setting);
        }

        private static bool SetLanguage(string path, string language)
        {
            var config = GetConfigParser(path);

            config.SetValue("Localization", "g_languageAudio", DEFAULT);
            config.SetValue("Localization", "g_language", language);

            return config.Save();
        }
    }
}
