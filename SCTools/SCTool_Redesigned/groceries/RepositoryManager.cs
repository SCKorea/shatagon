using System;
using System.Collections.Generic;
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



        //public static get_TargetRepository()
        //{

        //}
    }
}
