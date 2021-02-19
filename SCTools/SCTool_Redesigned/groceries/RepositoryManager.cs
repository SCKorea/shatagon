using System;
using System.Collections.Generic;
using SCTool_Redesigned.Settings;

namespace SCTool_Redesigned.groceries
{
    public class RepositoryManager
    {
        private List<LocalizationSource> _repolist;
        private LocalizationSource _currentlyinstalled;

        public RepositoryManager()
        {
            _repolist = App.Settings.GetGameLanguages();
        }

        public List<string> GetLocalizationList()
        {
            var list = new List<string>();

            foreach (LocalizationSource localization in _repolist)
            {
                list.Add(localization.Name);
            }
            return list;
        }
    }
}
