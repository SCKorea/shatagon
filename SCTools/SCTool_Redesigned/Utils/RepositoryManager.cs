using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml;
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
        private static List<LocalizationSource> _repolist;
        private static LocalizationSource _localizationSource;
        public static GitHubLocalizationRepository TargetRepository { get; private set; }
        public static LocalizationInstallation TargetInstallation { get; private set; }
        public static UpdateInfo TargetInfo { get; private set; }

        static RepositoryManager()
        {
            _repolist = App.Settings.GetGameLanguages();
            TargetInstallation = App.Settings.LIVE_Localization.Installations.FirstOrDefault();
            _localizationSource = null;
        }
        public static void SetInstalledRepository() //does it make sense? I don't get it...
        {
            //App.Settings.LIVE_Localization += TargetInstallation;
            App.Settings.LIVE_Localization.Installations.Add(TargetInstallation);
            App.SaveAppSettings();
            //_currentInstalled.InstalledVersion = TargetInstallation.InstalledVersion;
            //_currentInstalled.LastVersion = TargetInstallation.LastVersion;
        }
        public static void RemoveInstalledRepository()
        {
            App.Settings.LIVE_Localization.Installations.Clear();
            App.SaveAppSettings();
        }
        public static void SetInstallationTarget(string select, string last, UpdateInfo info)
        {
            string lowerSelect = select.ToLower();
            GameMode mode = GameMode.LIVE;

            if (lowerSelect.Contains("ptu"))
            {
                mode = GameMode.PTU;
            }

            if (TargetInstallation == null || !TargetInstallation.Mode.Equals(mode))
            {
                TargetInstallation = new LocalizationInstallation(mode, _localizationSource.Repository, UpdateRepositoryType.GitHub);
            }

            /*
            //TODO: make selection between LIVE and PTU
            if (TargetInstallation == null)
            {
                TargetInstallation = new LocalizationInstallation(GameMode.LIVE, _localizationSource.Repository, UpdateRepositoryType.GitHub);
            }
            */

            TargetInstallation.LastVersion = last;
            TargetInstallation.InstalledVersion = select;
            TargetInstallation.AllowPreRelease = false; //TODO: options?
            TargetInfo = info;

            App.SaveAppSettings();
        }

        public static void ToggleLocalization()
        {
            try
            {
                TargetRepository.Installer.RevertLocalization(App.CurrentGame.RootFolderPath);
                TargetInstallation.IsEnabled = !TargetInstallation.IsEnabled;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during toggle localization: {App.CurrentGame.Mode}");
                //_logger.Error(e, $"Error during toggle localization: {CurrentGame.Mode}");
            }
        }

        public static List<string> GetLocalizationList()
        {
            var list = new List<string>();

            foreach (LocalizationSource localization in _repolist)
            {
                list.Add(localization.Name);
            }
            return list;
        }

        public static bool IsAvailable()
        {
            //Console.WriteLine("TargetInfo: " + (TargetInfo != null ? "OK" : "NO"));
            //Console.WriteLine("TargetRepository: " + (TargetRepository != null ? "OK" : "NO"));
            if (TargetInfo != null && TargetRepository != null)
                return true;
            else
                return false;
        }

        public static bool SetTargetRepository()
        {
            foreach (LocalizationSource localization in _repolist)
            {
                if (localization.Name.Equals(App.Settings.GameLanguage))
                {
                    TargetRepository = new GitHubLocalizationRepository(HttpNetClient.Client, GameMode.LIVE, localization.Name, localization.Repository);
                    if (localization.IsPrivate)
                        TargetRepository.AuthToken = localization.AuthToken;
                    return true;
                }
            }
            return false;
        }

        public static LocalizationSource GetLocalizationSource()
        {
            if (_localizationSource == null)
            {
                string launguageName = App.Settings.GameLanguage;

                foreach (LocalizationSource gameLanguage in App.Settings.GetGameLanguages())
                {
                    if (gameLanguage.Name.Equals(launguageName))
                    {
                        _localizationSource = gameLanguage;
                        break;
                    }
                }

                if (_localizationSource == null)
                {
                    //Get orignal loaclization Pack.
                    _localizationSource = LocalizationSource.DefaultBaseModding;
                }
            }

            return _localizationSource;
        }

        private static CustomGitHubRepository.GitRelease[] _githubReleases;

        private static CustomGitHubRepository.GitRelease[] GetReleases(bool cache)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = tokenSource.Token;

            if (!cache || _githubReleases == null)
            {
                Task.Run(() =>
                {
                    var customGithubRepo = new CustomGitHubRepository(
                        HttpNetClient.Client, GitHubDownloadType.Sources, CustomUpdateInfo.Factory.NewWithVersionByName(), "SCTools", GetLocalizationSource().Repository);

                    if (GetLocalizationSource().Repository.Equals("xhatagon/sc_ko"))
                    {
                        customGithubRepo.ChangeReleasesUrl("https://sc.galaxyhub.kr/api/v2/releases/check");
                    }

                    customGithubRepo.UpdateAsync(cancellationToken).Wait();

                    var getReleases = customGithubRepo.GetReleasesAsync(true, cancellationToken);
                    getReleases.Wait();

                    _githubReleases = getReleases.Result;
                }).Wait();
            }


            return _githubReleases;
        }


        private static IEnumerable<UpdateInfo>? _githubReleasesInfo;

        public static IEnumerable<UpdateInfo> GetInfos(bool cache = true)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = tokenSource.Token;

            if (!cache || _githubReleases == null)
            {
                Task.Run(() =>
                {
                    var customGithubRepo = new CustomGitHubRepository(
                        HttpNetClient.Client, GitHubDownloadType.Sources, CustomUpdateInfo.Factory.NewWithVersionByName(), "SCTools", GetLocalizationSource().Repository);

                    if (GetLocalizationSource().Repository.Equals("xhatagon/sc_ko"))
                    {
                        customGithubRepo.ChangeReleasesUrl("https://sc.galaxyhub.kr/api/v2/releases/check");
                    }

                    //customGithubRepo.UpdateAsync(cancellationToken).Wait();
                    customGithubRepo.RefreshUpdatesAsync(cancellationToken).Wait();
                    //var getReleases = customGithubRepo.GetReleasesAsync(true, cancellationToken);
                    //getReleases.Wait();

                    _githubReleasesInfo = customGithubRepo.UpdateReleases;
                }).Wait();
            }


            return _githubReleasesInfo;
        }

        public static string GetReleaseNote(bool cache = true)
        {
            StringBuilder sb = new StringBuilder();

            var releases = GetReleases(cache);

            if (releases != null)
            {
                foreach (CustomGitHubRepository.GitRelease release in releases)
                {
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
            }

            if (sb.Length <= 0)
            {
                sb.Append(Properties.Resources.UI_Desc_UnableMarkdown);
            }

            return sb.ToString();
        }

        public static List<string> GetReleaseVersions(bool cache = true)
        {
            var releases = GetReleases(cache);
            var list = new List<string>();

            if (releases != null)
            {
                foreach (CustomGitHubRepository.GitRelease release in releases)
                {
                    list.Add(release.Name);

                }
            }

            return list;
        }

        public static string GetMarkdownDocument(string documentName)
        {
            string markdown = string.Empty;

            LocalizationSource localization = GetLocalizationSource();
            string gitUrl = "";

            if (localization.Type.Equals(UpdateRepositoryType.GitHub))
            {
                gitUrl = "https://raw.githubusercontent.com/";
            }

            string markdownUrl = $"{gitUrl}{localization.Repository}/master/{documentName}";

            if (localization.Repository.Contains("sc_ko"))
            {
                // The sc_ko repository is private and uses its own api server.
                markdownUrl = $"https://sc.galaxyhub.kr/api/v1/translate/document/?page={documentName}";
            }

            HttpClient client = HttpNetClient.Client;

            var connectTask = client.GetAsync(markdownUrl);
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


            return markdown;
        }
    }
}
