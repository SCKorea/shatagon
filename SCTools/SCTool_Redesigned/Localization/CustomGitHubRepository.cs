using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Update;

namespace SCTool_Redesigned.Localization
{
    class CustomGitHubRepository : GitHubUpdateRepository
    {
        private static IDictionary<string, GitRelease[]?> _cache = new Dictionary<string, GitRelease[]?>();

        private const string GitHubApiUrl = "https://api.github.com/repos";
        private readonly HttpClient _httpClient;
        private readonly CustomUpdateInfo.Factory _gitHubUpdateInfoFactory;

        private string _repoReleasesUrl;

        public new GitHubDownloadType DownloadType { get; }
        public new string? AuthToken { get; set; }

        public CustomGitHubRepository(HttpClient httpClient, GitHubDownloadType downloadType, CustomUpdateInfo.Factory gitHubUpdateInfoFactory, string name, string repository) :
            base(httpClient, downloadType, gitHubUpdateInfoFactory.GetBaseGitHubUpdateInfo(), name, repository)
        {
            DownloadType = downloadType;
            _httpClient = httpClient;
            _repoReleasesUrl = $"{GitHubApiUrl}/{repository}/releases?per_page=100";
            _gitHubUpdateInfoFactory = gitHubUpdateInfoFactory;
        }

        public void ChangeReleasesUrl(string newReleasesUrl)
        {
            _repoReleasesUrl = newReleasesUrl;
        }

        public async Task<bool> UpdateAsync(CancellationToken cancellationToken)
        {
            using var requestMessage = buildRequestMessage(_repoReleasesUrl);
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            int rateRemain = 60;

            if (response.Headers.Contains("X-RateLimit-Remaining"))
            {
                rateRemain = int.Parse(response.Headers.GetValues("X-RateLimit-Remaining").First());
            }

            if (rateRemain >= 0)
            {
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                _cache[_repoReleasesUrl] = JsonHelper.Read<GitRelease[]>(content);

                return true;
            }

            return false;
        }

        public async Task<GitRelease[]?> GetReleasesAsync(bool cache, CancellationToken cancellationToken)
        {
            if (!cache || !_cache.ContainsKey(_repoReleasesUrl))
            {
                await UpdateAsync(cancellationToken);
            }

            return _cache[_repoReleasesUrl];
        }

        public override async Task<bool> CheckAsync(CancellationToken cancellationToken)
        {
            var cache = _cache;

            if (await UpdateAsync(cancellationToken))
            {
                return true;
            }

            _cache = cache;

            return false;
        }

        public override async Task<string> DownloadAsync(UpdateInfo updateInfo, string downloadPath, CancellationToken cancellationToken, IDownloadProgress? downloadProgress) =>
            await base.DownloadAsync(updateInfo, downloadPath, cancellationToken, downloadProgress);

        public override async Task<List<UpdateInfo>> GetAllAsync(CancellationToken cancellationToken)
        {
            var releases = await GetReleasesAsync(false, cancellationToken);

            if (releases != null && releases.Any())
            {
                var visibleReleases = AllowPreReleases ? releases : releases.Where(release =>
                    release.Draft != true && release.PreRelease != true);
                return DownloadType switch
                {
                    GitHubDownloadType.Assets => GetAssetUpdates(visibleReleases).ToList(),
                    GitHubDownloadType.Sources => GetSourceCodeUpdates(visibleReleases).ToList(),
                    _ => throw new NotSupportedException("Not supported download type"),
                };
            }
            return Enumerable.Empty<UpdateInfo>().ToList();
        }

        private IEnumerable<UpdateInfo> GetSourceCodeUpdates(IEnumerable<GitRelease> releases)
        {
            foreach (var r in releases)
            {
                var info = _gitHubUpdateInfoFactory.CreateWithDownloadSourceCode(r);
                if (info != null)
                    yield return info;
            }
        }

        private IEnumerable<UpdateInfo> GetAssetUpdates(IEnumerable<GitRelease> releases)
        {
            foreach (var r in releases)
            {
                var info = _gitHubUpdateInfoFactory.CreateWithDownloadAsset(r);
                if (info != null)
                    yield return info;
            }
        }

        private HttpRequestMessage buildRequestMessage(string requestUri)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (AuthToken != null && Uri.TryCreate(requestUri, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("token", AuthToken);
            }

            return requestMessage;
        }

        #region Git objects
        public new class GitRelease
        {
            [JsonProperty("id")]
            public int? Id { get; private set; }
            [JsonProperty("name")]
            public string? Name { get; private set; }
            [JsonProperty("body")]
            public string? Body { get; }
            [JsonProperty("url")]
            public string? Url { get; }
            [JsonProperty("tag_name")]
            public string? TagName { get; }
            [JsonProperty("draft")]
            public bool? Draft { get; private set; }
            [JsonProperty("prerelease")]
            public bool? PreRelease { get; private set; }
            [JsonProperty("zipball_url")]
            public string? ZipUrl { get; }
            [JsonProperty("published_at")]
            public DateTimeOffset? Published { get; private set; }
            [JsonProperty("created_at")]
            public DateTimeOffset Created { get; private set; }
            [JsonProperty("assets")]
            public GitAsset[] Assets { get; private set; } = new GitAsset[0];

            [JsonConstructor]
            public GitRelease(string name, string body, string url, string tagName, string zipUrl)
            {
                Name = name;
                Body = body;
                Url = url;
                TagName = tagName;
                ZipUrl = zipUrl;
            }
        }

        public new class GitAsset
        {
            [JsonProperty("name")]
            public string? Name { get; private set; }
            [JsonProperty("url")]
            public string? ApiUrl { get; private set; }
            [JsonProperty("browser_download_url")]
            public string? ZipUrl { get; private set; }
        }

        public new class GitRateLimit
        {
            [JsonProperty("rate")]
            public GitRate Rate { get; }

            [JsonConstructor]
            public GitRateLimit(GitRate rate)
            {
                Rate = rate;
            }
        }

        public new class GitRate
        {
            [JsonProperty("limit")]
            public int? Limit { get; private set; }
            [JsonProperty("remaining")]
            public int? Remaining { get; private set; }
            [JsonProperty("reset")]
            public long? Reset { get; private set; }
            [JsonProperty("used")]
            public int? Used { get; private set; }
        }
        #endregion
    }

}
