using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Localization
{
    public class CustomGitHubLocalizationRepository : GitHubUpdateRepository, ILocalizationRepository
    {
        public GameMode Mode { get; }

        public CustomGitHubLocalizationRepository(HttpClient httpClient, GameMode mode, string name, string repository) :
            base(httpClient, GitHubDownloadType.Sources, GitHubUpdateInfo.Factory.NewWithVersionByName(), name, repository)
        {
            Mode = mode;
        }

        public ILocalizationInstaller Installer { get; } = new CustomLocalizationInstaller();

        public override async Task<List<UpdateInfo>> GetAllAsync(CancellationToken cancellationToken)
        {
            var updates = await base.GetAllAsync(cancellationToken).ConfigureAwait(false);
            return updates.Where(i => IsTagNameForMode(i.TagName, Mode)).ToList();
        }

        private static bool IsTagNameForMode(string tagName, GameMode mode)
        {
            if (mode != GameMode.LIVE)
            {
                return tagName.EndsWith($"-{mode}", StringComparison.OrdinalIgnoreCase);
            }
            int index = tagName.LastIndexOf("-", StringComparison.Ordinal);
            if (index < 0 || index == tagName.Length - 1)
            {
                return true;
            }
            if (Enum.TryParse(tagName.Substring(index + 1), true, out GameMode tagMode))
            {
                return tagMode == mode;
            }
            return true;
        }
    }
}
