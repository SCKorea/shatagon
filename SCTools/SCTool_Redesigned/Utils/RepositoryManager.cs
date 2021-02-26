using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
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
        private static LocalizationInstallation _currentInstalled, _installTarget;
        private static LocalizationSource _localizationSource;

        static RepositoryManager()
        {
            _repolist = App.Settings.GetGameLanguages();
            _currentInstalled = null;
            _installTarget = null;
            _localizationSource = null;
        }
        public static void SetInstallationTarget(string select, string last)
        {   //TODO: make selection between LIVE and PTU
            if(_installTarget == null)
                _installTarget = new LocalizationInstallation(GameMode.LIVE,_localizationSource.Repository, UpdateRepositoryType.GitHub);
            _installTarget.LastVersion = last;
            _installTarget.InstalledVersion = select;
            _installTarget.AllowPreRelease = false; //TODO: options?
            App.SaveAppSettings();
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
            if (_localizationSource != null && _installTarget.InstalledVersion != null)
                return true;
            else
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
                        HttpNetClient.Client, NSW.StarCitizen.Tools.Lib.Update.GitHubDownloadType.Sources, CustomUpdateInfo.Factory.NewWithVersionByName(), "SCTools", GetLocalizationSource().Repository);

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

        public static string GetReleaseNote(bool cache = true)
        {
            StringBuilder sb = new StringBuilder();

            var releases = GetReleases(cache);

            if (releases != null)
            {
                foreach (CustomGitHubRepository.GitRelease release in releases)
                {
                    
                    sb.Append($"### {release.Name}\n");

                    var body = release.Body;
                    if (body.Length <= 0)
                    {
                        body = Properties.Resources.UI_Desc_NoContent;
                    }

                    sb.Append($"{body}\n");
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

    }
}
