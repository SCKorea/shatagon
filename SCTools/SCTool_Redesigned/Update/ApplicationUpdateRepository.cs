using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Update
{
    internal sealed class ApplicationUpdateRepository : GitHubUpdateRepository
    {
        private readonly HttpClient _httpClient;
        private readonly string _releasesUrl;

        internal ApplicationUpdateRepository(HttpClient httpClient, string name, string repository)
            : base(httpClient, GitHubDownloadType.Assets, GitHubUpdateInfo.Factory.NewWithVersionByTagName(), name, repository)
        {
            _httpClient = httpClient;
            _releasesUrl = $"https://api.github.com/repos/{repository}/releases?per_page=100";
        }

        public override async Task<List<UpdateInfo>> GetAllAsync(CancellationToken cancellationToken)
        {
            using var request = CreateRequest(_releasesUrl);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var releases = JsonHelper.Read<Release[]>(content) ?? [];
            var updates = new List<UpdateInfo>();

            foreach (var release in releases)
            {
                if (release.Draft || string.IsNullOrWhiteSpace(release.TagName))
                    continue;

                var assets = release.Assets.Where(asset =>
                    string.Equals(asset.Name, CustomPackageVerifier.ExecutableName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (assets.Length != 1 || assets[0].Size <= 0 || string.IsNullOrWhiteSpace(assets[0].DownloadUrl))
                    continue;

                updates.Add(new ApplicationUpdateInfo(release.Name ?? release.TagName, release.TagName,
                    assets[0].DownloadUrl!, release.PreRelease, release.Published, assets[0].Size, assets[0].Digest));
            }

            return updates;
        }

        public override async Task<string> DownloadAsync(UpdateInfo updateInfo, string downloadPath,
            CancellationToken cancellationToken, IDownloadProgress? downloadProgress)
        {
            if (updateInfo is not ApplicationUpdateInfo applicationUpdate)
                throw new InvalidDataException("The update does not identify a Shatagon.exe release asset.");

            Directory.CreateDirectory(downloadPath);
            var executablePath = Path.Combine(downloadPath, CustomPackageVerifier.ExecutableName);
            var temporaryPath = Path.Combine(downloadPath, $"download-{Guid.NewGuid():N}.tmp");
            var progress = downloadProgress == null ? null : new Progress<long>(downloadProgress.ReportDownloadedSize);
            downloadProgress?.ReportContentSize(applicationUpdate.Size);
            try
            {
                using var request = CreateRequest(updateInfo.DownloadUrl);
                using var response = await _httpClient.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long contentLength && contentLength != applicationUpdate.Size)
                    throw new InvalidDataException("The update download size does not match the release asset.");

                await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var destination = File.Create(temporaryPath))
                {
                    if (progress == null)
                        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    else
                        await source.CopyToAsync(destination, 0x4000, cancellationToken, progress).ConfigureAwait(false);
                }

                if (new FileInfo(temporaryPath).Length != applicationUpdate.Size)
                    throw new InvalidDataException("The update download is incomplete.");

                if (!string.IsNullOrWhiteSpace(applicationUpdate.Digest))
                {
                    var digest = applicationUpdate.Digest;
                    if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                        !UpdateProcessHelper.TryParseHash(digest[7..], out var expectedHash) ||
                        !CryptographicOperations.FixedTimeEquals(UpdateProcessHelper.ComputeHash(temporaryPath), expectedHash))
                    {
                        throw new InvalidDataException("The update download does not match the release SHA-256 digest.");
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, executablePath, true);
                return executablePath;
            }
            finally
            {
                FileUtils.DeleteFileNoThrow(temporaryPath);
            }
        }

        private HttpRequestMessage CreateRequest(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(AuthToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("token", AuthToken);
            return request;
        }

        private sealed class Release
        {
            [JsonProperty("name")] public string? Name { get; set; }
            [JsonProperty("tag_name")] public string? TagName { get; set; }
            [JsonProperty("draft")] public bool Draft { get; set; }
            [JsonProperty("prerelease")] public bool PreRelease { get; set; }
            [JsonProperty("published_at")] public DateTimeOffset Published { get; set; }
            [JsonProperty("assets")] public Asset[] Assets { get; set; } = [];
        }

        private sealed class Asset
        {
            [JsonProperty("name")] public string? Name { get; set; }
            [JsonProperty("browser_download_url")] public string? DownloadUrl { get; set; }
            [JsonProperty("size")] public long Size { get; set; }
            [JsonProperty("digest")] public string? Digest { get; set; }
        }

        private sealed class ApplicationUpdateInfo : UpdateInfo
        {
            internal long Size { get; }
            internal string? Digest { get; }

            internal ApplicationUpdateInfo(string name, string tag, string url, bool preRelease,
                DateTimeOffset published, long size, string? digest) : base(name, tag, url)
            {
                PreRelease = preRelease;
                Released = published;
                Size = size;
                Digest = digest;
            }

            public override string GetVersion() => TagName;
        }
    }
}
