using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Settings
{
    public class LocalizationSettings
    {
        [JsonProperty]
        public List<LocalizationSource> Repositories { get; } = new List<LocalizationSource>();

        [JsonProperty]
        public List<LocalizationInstallation> Installations { get; } = new List<LocalizationInstallation>();

        [JsonProperty]
        public int MonitorRefreshTime { get; set; } = 5;
    }

    public class LocalizationInstallation
    {
        [JsonProperty]
        public GameMode Mode { get; }

        [JsonProperty]
        public string Repository { get; }

        [JsonProperty]
        public UpdateRepositoryType Type { get; }

        [JsonProperty]
        public string InstalledVersion { get; set; }

        [JsonProperty]
        public string LastVersion { get; set; }

        [JsonProperty]
        public int MonitorRefreshTime { get; set; }

        [JsonProperty]
        public bool MonitorForUpdates { get; set; }

        [JsonProperty]
        public bool AllowPreRelease { get; set; }

        [JsonConstructor]
        public LocalizationInstallation(GameMode mode, string repository, UpdateRepositoryType type)
        {
            Mode = mode;
            Repository = repository;
            Type = type;
        }
    }

    public class LocalizationSource
    {
        [JsonProperty]
        public string Name { get; }
        [JsonProperty]
        public string Repository { get; }
        [JsonProperty]
        public UpdateRepositoryType Type { get; }

        [JsonConstructor]
        public LocalizationSource(string name, string repository, UpdateRepositoryType type)
        {
            Name = name;
            Repository = repository;
            Type = type;
        }

        public static LocalizationSource DefaultBaseModding { get; } = new LocalizationSource("Base Modding Package", "defterai/starcitizenmodding", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultRussian { get; } = new LocalizationSource("русский", "n1ghter/sc_ru", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultUkrainian { get; } = new LocalizationSource("Український", "slyf0x-ua/sc_uk", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultKorean { get; } = new LocalizationSource("한국어", "xhatagon/sc_ko", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultPolish { get; } = new LocalizationSource("Polskie", "frosty-el-banana/sc_pl", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultChinese { get; } = new LocalizationSource("简体中文", "terrencetodd/sc_cn_zh", UpdateRepositoryType.GitHub);

        public static List<LocalizationSource> DefaultList { get; } = new List<LocalizationSource>() {
            DefaultRussian,
            DefaultUkrainian,
            DefaultKorean,
            DefaultPolish,
            DefaultChinese,
        };

        public static List<LocalizationSource> StandardList { get; } = new List<LocalizationSource>() {
            DefaultRussian,
            DefaultUkrainian,
            DefaultKorean,
            DefaultPolish,
            DefaultChinese,
            DefaultBaseModding
        };
    }
}
