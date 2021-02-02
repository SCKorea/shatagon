using System.Collections.Generic;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Global;
using NSW.StarCitizen.Tools.Update;

namespace NSW.StarCitizen.Tools.Settings
{
    public class LocalizationSettings
    {
        [JsonProperty]
        public List<LocalizationSource> Repositories { get; } = new List<LocalizationSource>();
        [JsonProperty]
        public List<LocalizationInstallation> Installations { get; } = new List<LocalizationInstallation>();
        [JsonProperty]
        public int MonitorRefreshTime { get; set; } = 5;

        [JsonProperty]
        public List<LocalizationAuthToken> AuthTokens { get; set; } = new List<LocalizationAuthToken>();
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
        public string? InstalledVersion { get; set; }
        [JsonProperty]
        public string? LastVersion { get; set; }
        [JsonProperty]
        public int MonitorRefreshTime { get; set; }
        [JsonProperty]
        public bool MonitorForUpdates { get; set; }
        [JsonProperty]
        public bool AllowPreRelease { get; set; }

        [JsonConstructor]
        public LocalizationInstallation(GameMode mode, string repository, UpdateRepositoryType? type)
        {
            Mode = mode;
            Repository = repository;
            Type = type ?? UpdateRepositoryType.GitHub;
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
        public static LocalizationSource DefaultRussian { get; } = new LocalizationSource("Russian Community", "n1ghter/sc_ru", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultUkrainian { get; } = new LocalizationSource("Ukrainian Community", "slyf0x-ua/sc_uk", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultKorean { get; } = new LocalizationSource("Korean Community", "xhatagon/sc_ko", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultPolish { get; } = new LocalizationSource("Polish Community", "frosty-el-banana/sc_pl", UpdateRepositoryType.GitHub);
        public static LocalizationSource DefaultChinese { get; } = new LocalizationSource("Chinese Community", "terrencetodd/sc_cn_zh", UpdateRepositoryType.GitHub);

        public static IReadOnlyList<LocalizationSource> DefaultList { get; } = new List<LocalizationSource>() {
            DefaultRussian,
            DefaultUkrainian,
            DefaultKorean,
            DefaultPolish,
            DefaultChinese,
        };

        public static IReadOnlyList<LocalizationSource> StandardList { get; } = new List<LocalizationSource>() {
            DefaultRussian,
            DefaultUkrainian,
            DefaultKorean,
            DefaultPolish,
            DefaultChinese,
            DefaultBaseModding
        };
    }

    public class LocalizationAuthToken
    {
        [JsonProperty]
        public string Name { get; }
        [JsonProperty]
        public string VersionToken { get; }
        [JsonProperty]
        public string DownloadToken { get; set; }

        [JsonConstructor]
        public LocalizationAuthToken(string name, string versiontoken, string downloadtoken)
        {
            Name = name;
            VersionToken = versiontoken;
            DownloadToken = downloadtoken;
        }

        public static LocalizationAuthToken DefaultKoreanAuth { get; } = new LocalizationAuthToken("Korean Community", "ec294acb43fff8b9b018b6b076396473e84935fe", "");
        public static IReadOnlyList<LocalizationAuthToken> DefaultList { get; } = new List<LocalizationAuthToken>() {
            DefaultKoreanAuth
        };
    }
}