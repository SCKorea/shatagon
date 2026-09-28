using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Localization;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Localization
{
    public class CustomGitHubLocalizationRepository : GitHubUpdateRepository, ILocalizationRepository
    {
        private readonly HttpClient _httpClient;
        public GameMode Mode { get; }

        public CustomGitHubLocalizationRepository(HttpClient httpClient, GameMode mode, string name, string repository) :
            base(httpClient, GitHubDownloadType.Sources, GitHubUpdateInfo.Factory.NewWithVersionByName(), name, repository)
        {
            Mode = mode;
            _httpClient = httpClient;
        }

        internal async Task<string> DownloadVariantAsync(VariantDownload selection, string downloadPath,
            CancellationToken cancellationToken, IDownloadProgress? progress)
        {
            var uri = new Uri(selection.Url);
            if (uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "api.github.com" && uri.Host != "github.com" && uri.Host != "codeload.github.com"))
                throw new InvalidOperationException("Unexpected release asset URL");

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(AuthToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("token", AuthToken);
            if (selection.UseApi)
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length)
                progress?.ReportContentSize(length);

            Directory.CreateDirectory(downloadPath);
            var tempFile = Path.Combine(downloadPath, $"sc-ko-{Guid.NewGuid():N}.zip");
            try
            {
                await using var output = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                var buffer = new byte[64 * 1024];
                long downloaded = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                    downloaded += count;
                    progress?.ReportDownloadedSize(downloaded);
                }
                return tempFile;
            }
            catch
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                throw;
            }
        }

        public ILocalizationInstaller Installer { get; } = new CustomLocalizationInstaller();

        public override async Task<List<UpdateInfo>> GetAllAsync(CancellationToken cancellationToken)
        {
            var updates = await base.GetAllAsync(cancellationToken).ConfigureAwait(false);

            return updates;

            //return updates.Where(i => IsTagNameForMode(i.TagName, Mode)).ToList();
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
