using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Localization;
using SCTool_Redesigned.Settings;
using SCTool_Redesigned.Update;

namespace SCTool_Redesigned.Utils
{
    public static class RepositoryManager
    {
        private static List<LocalizationSource>? _languageRepo;
        private static LocalizationSource? _localizationSource;
        public static CustomGitHubLocalizationRepository? TargetRepository { get; private set; }
        public static LocalizationInstallation? TargetInstallation { get; private set; }
        public static UpdateInfo? TargetInfo { get; private set; }

        static RepositoryManager()
        {
            _languageRepo = App.Settings.GetGameLanguages();
            _localizationSource = null;
            TargetInstallation = null;
        }

        public static void SetTargetInstallation(string gameMode, string version, UpdateInfo updateInfo)
        {
            var localizationSource = GetLocalizationSource();
            var localizationInstallation = new LocalizationInstallation(gameMode, localizationSource.Repository, UpdateRepositoryType.GitHub)
            {
                LastVersion = version,
                InstalledVersion = version,
                IsEnabled = false,
                AllowPreRelease = App.Settings.Nightly
            };

            TargetInstallation = localizationInstallation;
            TargetInfo = updateInfo;

            App.SaveAppSettings();
        }

        public static void SetInstallationRepository(LocalizationInstallation localizationInstallation)
        {
            var installations = App.Settings.LIVE_Localization.Installations;
            var installation = installations.Find(installation => installation.Mode.Equals(localizationInstallation.Mode));

            if (installation != null)
            {
                App.Settings.LIVE_Localization.Installations.Remove(installation);
            }

            App.Settings.LIVE_Localization.Installations.Add(localizationInstallation);
            App.SaveAppSettings();
        }

        public static bool RemoveInstallationRepository(LocalizationInstallation installation)
        {
            return App.Settings.LIVE_Localization.Installations.Remove(installation);
        }


        public static LocalizationInstallation? GetInstallationRepository(string gameMode)
        {
            var installation = App.Settings.LIVE_Localization.Installations.Find(item => item.Mode.Equals(gameMode));

            return installation;
        }

        public static bool SetTargetRepository()
        {
            if (_languageRepo == null)
            {
                return false;
            }

            CustomGitHubLocalizationRepository? targetRepository = null;

            foreach (var repo in _languageRepo)
            {
                if (!repo.Name.Equals(App.Settings.GameLanguage))
                {
                    continue;
                }

                targetRepository = new CustomGitHubLocalizationRepository(
                    HttpNetClient.Client,
                    GameMode.LIVE, //Dummy flag. it doesn't affect actual operation.
                    repo.Name,
                    repo.Repository
                );

                if (repo.IsPrivate)
                {
                    targetRepository.AuthToken = repo.AuthToken;
                }
            }

            TargetRepository = targetRepository;

            return true;
        }

        public static void RemoveInstalledRepository()
        {
            App.Settings.LIVE_Localization.Installations.Clear();
            App.SaveAppSettings();
        }

        public static List<string> GetLocalizationList()
        {
            var languages = _languageRepo;

            if (languages == null)
            {
                return [];
            }

            return languages.ConvertAll(source => source.Name);
        }

        public static bool IsAvailable() => TargetInfo != null && TargetRepository != null;

        public static LocalizationSource GetLocalizationSource()
        {
            var sources = _localizationSource;

            if (sources != null)
            {
                return sources;
            }

            var gameLanguage = App.Settings.GameLanguage;
            var gameLanguages = App.Settings.GetGameLanguages();

            sources = gameLanguages.Find(language => language.Name.Equals(gameLanguage));

            if (sources == null)
            {
                sources = LocalizationSource.DefaultBaseModding;
            }

            _localizationSource = sources;

            return _localizationSource;
        }

        private static CustomGitHubRepository.GitRelease[] _githubReleases = [];

        private static CustomGitHubRepository.GitRelease[] GetReleases(bool cache)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = tokenSource.Token;

            if (cache && _githubReleases.Length > 0)
            {
                return _githubReleases;
            }

            Task.Run(() =>
            {
                var customGithubRepo = GetCustomGitHubRepository(cache);
                var getReleases = customGithubRepo.GetReleasesAsync(true, cancellationToken);
                getReleases.Wait();

                _githubReleases = getReleases.Result ?? [];
            }).Wait();

            return _githubReleases;
        }


        private static IEnumerable<UpdateInfo> _githubReleasesInfo = [];

        public static IEnumerable<UpdateInfo> GetInfos(bool cache = true)
        {
            if (cache && _githubReleases.Length > 0)
            {
                return _githubReleasesInfo;
            }

            Task.Run(() =>
            {
                var releases = GetCustomGitHubRepository(cache).UpdateReleases;

                _githubReleasesInfo = releases ?? [];
            }).Wait();

            return _githubReleasesInfo;
        }

        //

        public static string GetReleaseNote(bool cache = true)
        {
            StringBuilder sb = new StringBuilder();

            var releases = GetReleases(cache);

            if (releases != null)
            {
                App.Logger.Info("Start releases note parseing.");

                foreach (CustomGitHubRepository.GitRelease release in releases)
                {
                    if (release.Draft ?? true)
                    {
                        continue;
                    }

                    //Humanize
                    sb.Append($"# {release.Name}\n");
                    sb.Append($"{XmlConvert.ToString(release.Published.ToLocalTime(), Properties.Resources.UI_Desc_DateTimeFormat)}  \n");
                    sb.Append($"<br/>    \n");

                    var body = release.Body;
                    if (body.Length <= 0)
                    {
                        body = Properties.Resources.UI_Desc_NoContent;
                    }

                    sb.Append($"{body}\n");
                    sb.Append($"<br/>    \n");
                    sb.Append($"<br/>    \n");
                    sb.Append($"<br/>    \n");
                }

                App.Logger.Info("Finish releases note parseing.");
            }

            if (sb.Length <= 0)
            {
                sb.Append(Properties.Resources.UI_Desc_UnableMarkdown);
            }

            return sb.ToString();
        }

        public static string GetMarkdownDocument(string documentName)
        {
            App.Logger.Info("Start markdown document download.");

            string markdown = string.Empty;

            try
            {
                HttpClient client = HttpNetClient.Client;
                LocalizationSource localization = GetLocalizationSource();

                string gitUrl = "";

                if (localization.Type.Equals(UpdateRepositoryType.GitHub))
                {
                    gitUrl = "https://raw.githubusercontent.com/";
                }

                Task<HttpResponseMessage> connectTask;

                if (localization.Repository.Contains("sc_ko"))
                {
                    documentName = documentName.ToLower();

                    if (documentName == "credit.md")
                    {
                        documentName = "license.md";
                    }

                    // The sc_ko repository is private and uses its own api server.

                    var json = new JObject
                    {
                        { "account_id", "sckorea" },
                        { "repository_name", "translate-website" },
                        { "document_name", $"public/{documentName}" },
                        { "status", false }
                    };

                    var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json");
                    connectTask = client.PostAsync(App.ApiServer + "/api/v4/document", content);
                }
                else
                {
                    connectTask = client.GetAsync($"{gitUrl}{localization.Repository}/master/{documentName}");
                }

                connectTask.Wait();

                HttpResponseMessage httpResponse = connectTask.Result;

                switch (httpResponse.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        using (var httpContent = httpResponse.Content)
                        {
                            var content = httpContent.ReadAsStringAsync();
                            content.Wait();

                            markdown = content.Result;

                            App.Logger.Info("Finish markdown document download");
                        }
                        break;

                    case System.Net.HttpStatusCode.NotFound:
                        markdown = Properties.Resources.UI_Desc_NotFoundMarkdown;
                        break;

                    default:
                        markdown = Properties.Resources.UI_Desc_UnableMarkdown;
                        break;
                }

                //HttpNetClient.Dispose();

                // client.Dispose(); //DO NOT DISPOSE! - If you do this you can see System.ObjectDisposedException.

            }
            catch (Exception ex)
            {
                App.Logger.Error("" + ex.Message);
            }

            return markdown;
        }

        private static CustomGitHubRepository _customGitHubRepository = null;

        private static CustomGitHubRepository GetCustomGitHubRepository(bool cache = true)
        {
            if (!cache || _githubReleases == null)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                CancellationToken cancellationToken = tokenSource.Token;

                var customGithubRepo = new CustomGitHubRepository(
                            HttpNetClient.Client, GitHubDownloadType.Sources,
                            CustomUpdateInfo.Factory.NewWithVersionByName(),
                            "SCTools",
                            GetLocalizationSource().Repository
                );

                if (GetLocalizationSource().Repository.Equals("sckorea/sc_ko"))
                {
                    customGithubRepo.ChangeReleasesUrl(App.ApiServer + "/api/v4/release/all?account_id=sckorea&repository_name=sc_ko&status=0");
                }

                //customGithubRepo.UpdateAsync(cancellationToken).Wait();
                customGithubRepo.RefreshUpdatesAsync(cancellationToken).Wait();

                _customGitHubRepository = customGithubRepo;
            }

            return _customGitHubRepository;
        }
    }
}
