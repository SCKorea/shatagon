using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Update
{
    public class GitHubDevUpdateRepository : GitHubUpdateRepository
    {
        public new IEnumerable<UpdateInfo> UpdateReleases { get; private set; }

        public GitHubDevUpdateRepository(HttpClient httpClient, GitHubDownloadType downloadType,
            GitHubUpdateInfo.Factory gitHubUpdateInfoFactory, string name, string repository) :
            base(httpClient, downloadType, gitHubUpdateInfoFactory, name, repository)
        {
            Console.WriteLine("lllll");
        }

        public async new Task<UpdateInfo> GetLatestAsync(CancellationToken cancellationToken)
        {
            var boss = await base.GetLatestAsync(cancellationToken);

            Console.WriteLine("----------sss" + boss.DownloadUrl);

            var releases = await GetAllAsync(cancellationToken);
            if (releases.Any())
            {
                UpdateReleases = SortAndFilterReleases(releases).ToList();

                Console.WriteLine("----------");

                return UpdateReleases.FirstOrDefault();
            }
            return null;
        }

        private IEnumerable<UpdateInfo> SortAndFilterReleases(IEnumerable<UpdateInfo> releases)
        {
            Console.WriteLine("KKKKKK");

            return releases.Where(v => v.TagName == "dev");
        }
    }
}
