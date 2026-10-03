using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SCTool_Redesigned.Localization
{
    internal record ReleaseAsset(string Name, string? ApiUrl, string? DownloadUrl);
    internal record LocalizationReleaseInfo(string Tag, string SourceUrl, IReadOnlyList<ReleaseAsset> Assets);
    internal enum LocalizationReleaseFormat { Features, LegacyPacks, HistoricalSource }
    internal record LocalizationRelease(LocalizationReleaseInfo Info, LocalizationReleaseFormat Format,
        VariantCatalog? Catalog, ReleaseManifest? Manifest, IReadOnlyList<string> Notices);
    internal record PreparedLocalization(string ZipPath, string LegacyVariant,
        IReadOnlyList<string>? Features, IReadOnlyList<string> Warnings);
    internal record LocalizationDownloadProgress(long Downloaded, long? Total);

    /// <summary>Tag-scoped release preparation. No game writes or settings changes occur here.</summary>
    internal sealed class LocalizationReleaseClient
    {
        private const int MetadataLimit = 4 * 1024 * 1024;
        private const int IniLimit = 150 * 1024 * 1024;
        private const int ZipLimit = 256 * 1024 * 1024;
        internal const string InstallEntry = "data/Localization/korean_(south_korea)/global.ini";
        private readonly HttpClient _httpClient;
        private readonly string _repository;
        private readonly string? _authToken;

        public LocalizationReleaseClient(HttpClient httpClient, string repository, string? authToken)
        {
            if (!Regex.IsMatch(repository, @"\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant))
                throw new ArgumentException("Invalid GitHub repository", nameof(repository));
            _httpClient = httpClient;
            _repository = repository;
            _authToken = authToken;
        }

        public async Task<LocalizationRelease> LoadAsync(LocalizationReleaseInfo info, CancellationToken cancellationToken)
        {
            if (!VariantCatalog.TagPattern.IsMatch(info.Tag)) throw new InvalidDataException("Invalid release tag");
            if (info.Assets.Any(asset => string.IsNullOrWhiteSpace(asset.Name)) ||
                info.Assets.Select(asset => asset.Name).Distinct(StringComparer.Ordinal).Count() != info.Assets.Count)
                throw new InvalidDataException("Missing or duplicate release asset name");
            var prefix = $"sc-ko-{info.Tag}-";
            var manifestAsset = FindAsset(info, prefix + "build-manifest.json");
            bool modern = manifestAsset != null || FindAsset(info, prefix + "variants.json") != null ||
                FindAsset(info, prefix + "base.zip") != null ||
                info.Assets.Any(asset => asset.Name.StartsWith(prefix, StringComparison.Ordinal) && asset.Name.EndsWith(".ini", StringComparison.Ordinal));
            if (modern)
            {
                if (manifestAsset == null) throw new InvalidDataException("Build manifest asset is missing");
                var manifestData = await ReadAssetAsync(manifestAsset, MetadataLimit, cancellationToken).ConfigureAwait(false);
                var manifest = ReleaseManifest.Parse(manifestData, info.Tag);
                var catalogAsset = RequireAsset(info, manifest.Catalog.Asset);
                var catalogData = await ReadAssetAsync(catalogAsset, MetadataLimit, cancellationToken).ConfigureAwait(false);
                ReleaseManifest.VerifyHash(catalogData, manifest.Catalog);
                var catalog = VariantCatalog.ParseModern(catalogData);
                manifest.ValidateCatalog(catalog);
                RequireAsset(info, manifest.Base.Asset);
                cancellationToken.ThrowIfCancellationRequested();
                return new LocalizationRelease(info, LocalizationReleaseFormat.Features, catalog, manifest, []);
            }
            if (info.Assets.Count == 0 && LegacyReleaseTags.All.Contains(info.Tag))
                return new LocalizationRelease(info, LocalizationReleaseFormat.HistoricalSource, null, null,
                    [$"{info.Tag} 릴리즈는 부가기능 선택을 지원하지 않습니다. 해당 릴리즈의 기존 번역을 설치합니다."]);

            // Confirm the previous ZIP contract before attempting its tag-specific catalog.
            RequireAsset(info, prefix + "standard.zip");
            var contentsUrl = $"https://api.github.com/repos/{_repository}/contents/release/variants.json?ref={Uri.EscapeDataString(info.Tag)}";
            VariantCatalog legacyCatalog;
            var notices = new List<string> { "구형 릴리즈입니다. 설치할 번역 팩을 하나 선택하세요." };
            try
            {
                var catalogData = await ReadUrlAsync(contentsUrl, "application/vnd.github.raw+json", MetadataLimit, cancellationToken).ConfigureAwait(false);
                legacyCatalog = VariantCatalog.ParseLegacy(catalogData);
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                legacyCatalog = VariantCatalog.LegacyBaseOnly();
                notices.Add("이 태그의 구형 카탈로그가 없어 확인된 기본 번역만 제공합니다.");
            }
            RequireAsset(info, prefix + legacyCatalog.Fallback + ".zip");
            cancellationToken.ThrowIfCancellationRequested();
            return new LocalizationRelease(info, LocalizationReleaseFormat.LegacyPacks, legacyCatalog, null, notices);
        }

        public async Task<PreparedLocalization> PrepareAsync(LocalizationRelease release, IEnumerable<string> selection,
            string directory, CancellationToken cancellationToken, Action<LocalizationDownloadProgress>? progress = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var warnings = new List<string>();
            if (release.Format == LocalizationReleaseFormat.Features)
            {
                var catalog = release.Catalog ?? throw new InvalidDataException("Feature catalog is missing");
                var manifest = release.Manifest ?? throw new InvalidDataException("Build manifest is missing");
                manifest.ValidateCatalog(catalog);
                var selected = catalog.OrderedSelection(selection);
                var baseData = await ReadIniAssetAsync(RequireAsset(release.Info, manifest.Base.Asset), manifest.Base, cancellationToken, progress).ConfigureAwait(false)
                    ?? throw new InvalidDataException($"Required release asset is missing: {manifest.Base.Asset}");
                ReleaseManifest.VerifyIni(baseData, manifest.Base, isBase: true);
                var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var actual = new List<string>();
                foreach (var id in selected)
                {
                    var descriptor = manifest.Features[id];
                    var asset = FindAsset(release.Info, descriptor.Asset);
                    // A listed asset's HTTP/authentication/integrity error is fatal, never a fallback.
                    var data = asset == null ? null : await ReadIniAssetAsync(asset, descriptor, cancellationToken, progress).ConfigureAwait(false);
                    if (data == null)
                    {
                        warnings.Add($"{release.Info.Tag}의 {id} 파일이 없어 이 기능을 제외하고 설치합니다.");
                        continue;
                    }
                    ReleaseManifest.VerifyIni(data, descriptor, isBase: false);
                    parts.Add(id, data);
                    actual.Add(id);
                }
                cancellationToken.ThrowIfCancellationRequested();
                var output = LocalizationComposer.Compose(baseData, catalog, parts, actual, warnings.Add);
                cancellationToken.ThrowIfCancellationRequested();
                var zipPath = await WriteComposedZipAsync(output, directory, cancellationToken).ConfigureAwait(false);
                return new PreparedLocalization(zipPath, "features", actual.ToArray(), warnings);
            }

            byte[] package;
            string variant;
            if (release.Format == LocalizationReleaseFormat.HistoricalSource)
            {
                if (selection.Any()) throw new InvalidDataException("Historical releases have no feature selection");
                package = await ReadUrlAsync(release.Info.SourceUrl, null, ZipLimit, cancellationToken, progress).ConfigureAwait(false);
                variant = "legacy";
            }
            else
            {
                var catalog = release.Catalog ?? throw new InvalidDataException("Legacy catalog is missing");
                var selected = catalog.OrderedSelection(selection);
                if (selected.Length != 1) throw new InvalidDataException("Select exactly one legacy translation pack");
                variant = selected[0];
                var asset = FindAsset(release.Info, $"sc-ko-{release.Info.Tag}-{variant}.zip");
                if (asset == null)
                {
                    warnings.Add($"{release.Info.Tag}의 {variant} 파일이 없어 구형 기본 번역 팩을 설치합니다.");
                    variant = catalog.Fallback!;
                    asset = RequireAsset(release.Info, $"sc-ko-{release.Info.Tag}-{variant}.zip");
                }
                package = await ReadAssetAsync(asset, ZipLimit, cancellationToken, progress).ConfigureAwait(false);
            }
            var path = await WriteTemporaryAsync(package, directory, cancellationToken).ConfigureAwait(false);
            return new PreparedLocalization(path, variant, null, warnings);
        }

        private static ReleaseAsset? FindAsset(LocalizationReleaseInfo info, string name) =>
            info.Assets.SingleOrDefault(asset => asset.Name == name);

        private static ReleaseAsset RequireAsset(LocalizationReleaseInfo info, string name) =>
            FindAsset(info, name) ?? throw new InvalidDataException($"Required release asset is missing: {name}");

        private async Task<byte[]?> ReadIniAssetAsync(ReleaseAsset asset, ReleaseFile descriptor,
            CancellationToken cancellationToken, Action<LocalizationDownloadProgress>? progress)
        {
            var data = await ReadAssetAsync(asset, descriptor.IsZip ? ZipLimit : IniLimit, cancellationToken, progress).ConfigureAwait(false);
            ReleaseManifest.VerifyAssetHash(data, descriptor);
            cancellationToken.ThrowIfCancellationRequested();
            if (!descriptor.IsZip) return data;
            using var source = new MemoryStream(data, writable: false);
            using var archive = new ZipArchive(source, ZipArchiveMode.Read);
            var entries = archive.Entries.Where(entry =>
                entry.FullName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            if (entries.Length == 0) return null;
            if (entries.Length != 1) throw new InvalidDataException($"ZIP must contain exactly one INI: {asset.Name}");
            var entry = entries[0];
            if (entry.Length > IniLimit) throw new InvalidDataException($"INI exceeds the supported size: {asset.Name}");
            await using var input = entry.Open();
            using var output = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (output.Length + count > IniLimit) throw new InvalidDataException($"INI exceeds the supported size: {asset.Name}");
                output.Write(buffer, 0, count);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return output.ToArray();
        }

        private Task<byte[]> ReadAssetAsync(ReleaseAsset asset, int limit, CancellationToken cancellationToken,
            Action<LocalizationDownloadProgress>? progress = null)
        {
            bool useApi = !string.IsNullOrWhiteSpace(asset.ApiUrl);
            var url = useApi ? asset.ApiUrl : asset.DownloadUrl;
            if (string.IsNullOrWhiteSpace(url)) throw new InvalidDataException($"Asset URL is missing: {asset.Name}");
            return ReadUrlAsync(url, useApi ? "application/octet-stream" : null, limit, cancellationToken, progress);
        }

        private async Task<byte[]> ReadUrlAsync(string url, string? accept, int limit, CancellationToken cancellationToken,
            Action<LocalizationDownloadProgress>? progress = null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "api.github.com" && uri.Host != "github.com" && uri.Host != "codeload.github.com"))
                throw new InvalidDataException("Unexpected GitHub release URL");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrEmpty(_authToken)) request.Headers.Authorization = new AuthenticationHeaderValue("token", _authToken);
            if (accept != null) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0) request.Headers.UserAgent.ParseAdd("Shatagon/feature-selection");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            long? length = response.Content.Headers.ContentLength;
            if (length > limit) throw new InvalidDataException("Release download exceeds the supported size");
            progress?.Invoke(new LocalizationDownloadProgress(0, length));
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (output.Length + count > limit) throw new InvalidDataException("Release download exceeds the supported size");
                output.Write(buffer, 0, count);
                progress?.Invoke(new LocalizationDownloadProgress(output.Length, length));
            }
            cancellationToken.ThrowIfCancellationRequested();
            return output.ToArray();
        }

        private static async Task<string> WriteComposedZipAsync(byte[] data, string directory, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"sc-ko-{Guid.NewGuid():N}.zip");
            try
            {
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                await using (var entry = archive.CreateEntry(InstallEntry).Open())
                    await entry.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return path;
            }
            catch
            {
                if (File.Exists(path)) File.Delete(path);
                throw;
            }
        }

        private static async Task<string> WriteTemporaryAsync(byte[] data, string directory, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"sc-ko-{Guid.NewGuid():N}.zip");
            try
            {
                await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await file.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return path;
            }
            catch
            {
                if (File.Exists(path)) File.Delete(path);
                throw;
            }
        }
    }
}
