using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Global;

namespace SCTool_Redesigned.Settings
{
    public class AppSettings
    {
        [JsonProperty]
        public string GameFolder { get; set; }

        [JsonProperty]
        public string ToolLanguage { get; set; }

        [JsonProperty]
        public string GameLanguage { get; set; }

        [JsonProperty]
        public bool AcceptInstallWarning { get; set; }

        [JsonProperty]
        public LocalizationSettings LIVE_Localization { get; } = new LocalizationSettings();

        [JsonProperty]
        public LocalizationSettings PTU_Localization { get; } = new LocalizationSettings();

        public LocalizationSettings GetGameModeSettings(GameMode gameMode)
        {
            switch (gameMode)
            {
                case GameMode.LIVE:
                    return LIVE_Localization;
                case GameMode.PTU:
                    return PTU_Localization;
                default:
                    throw new NotSupportedException("Not supported game mode: " + gameMode);
            }
        }

        public Dictionary<string, string> GetToolLanguages() => new Dictionary<string, string> {
                { "en-US", "English" },
                { "ko-KR", "한국어" }
        };

        public List<LocalizationSource> GetGameLanguages() => LocalizationSource.DefaultList;

    }
}
