using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Octokit;
using SCTool_Redesigned.Settings;

namespace SCTool_Redesigned.groceries
{
    public static class RepositoryManager
    {
        private static List<LocalizationSource> _repolist;
        private static LocalizationSource _currentlyinstalled;
        private static LocalizationSource _installtarget;

        static RepositoryManager()
        {
            _repolist = App.Settings.GetGameLanguages();
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

        public static void installTarget() //FIXME: name and its mechanism
        {
            //Maro, Please check below name of GetLocalizationSource method! I think that method is a little safer.  - Laeng - 
            foreach (LocalizationSource localization in _repolist)
            {
                if (localization.Name.Equals(App.Settings.GameLanguage))
                {
                    _installtarget = localization;
                    break;
                }
            }
        }

        //I already wrote this at AppSettings.cs... I brought this here to avoid confusion. - Laeng -
        private static LocalizationSource _localizationSource;
        public static LocalizationSource GetLocalizationSource()
        {
            if (_localizationSource == null)
            {
                var launguageName = App.Settings.GameLanguage;

                foreach (var gameLanguage in App.Settings.GetGameLanguages())
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

        private static List<Release> _githubReleases;

        public static List<Release> GetGithubReleases(bool cache, bool isCredential, string token)
        {
            if (!cache || _githubReleases == null)
            {
                Task.Run(() =>
                {
                    var client = new GitHubClient(new ProductHeaderValue("SCTools"));

                    if (isCredential)
                    {
                        client.Credentials = new Credentials(token);
                    }

                    string[] repo = GetLocalizationSource().Repository.Split('/');
                    
                    try
                    {
                        var releases = client.Repository.Release.GetAll(repo[0], repo[1]);
                        releases.Wait();

                        if (client.GetLastApiInfo().RateLimit.Remaining >= 0)
                        {
                            _githubReleases = releases.Result.ToList();
                        }

                    }
                    catch(AggregateException)
                    {
                        
                    }
                    

                }).Wait();
            }

            return _githubReleases;
        }


        public static string GetLatestReleaseNote(bool cache = true, bool isCredential = false, string token = "")
        {
            var releases = GetGithubReleases(cache, isCredential, token);
            var sb = new StringBuilder();

            if (releases == null)
            {
                return sb.Append(Properties.Resources.UI_Desc_UnableMarkdown).ToString();
            }

            var i = 10;

            foreach(var release in releases)
            {
                if (--i <= 0) break;

                sb.Append($"### {release.Name}\n");

                var body = release.Body;
                if (body.Length <= 0)
                {
                    body = Properties.Resources.UI_Desc_NoContent;
                }

                sb.Append($"{body}\n");
            }

            return sb.ToString();
        }




        //public static get_TargetRepository()
        //{

        //}
    }
}
