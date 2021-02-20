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
            foreach (LocalizationSource localization in _repolist)
            {
                if (localization.Name.Equals(App.Settings.GameLanguage))
                {
                    _installtarget = localization;
                    break;
                }
            }
        }

        //public static get_TargetRepository()
        {

        }
    }
}
