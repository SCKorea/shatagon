using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SCTool_Redesigned.Localization
{
    internal record ReleaseFile(string Path, string Asset, string Sha256, int? Entries, string? AssetSha256 = null)
    {
        public bool IsZip => Asset.EndsWith(".zip", StringComparison.Ordinal);
    }

    internal sealed record ReleaseManifest(string Tag, ReleaseFile Catalog, ReleaseFile Base,
        IReadOnlyDictionary<string, ReleaseFile> Features)
    {
        private static readonly Regex HashPattern = new(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
        private static readonly Regex VersionPattern = new(@"\A[0-9]+\.[0-9]+\.[0-9]+\z", RegexOptions.CultureInvariant);

        public static ReleaseManifest Parse(byte[] data, string expectedTag)
        {
            using var document = CatalogJson.Parse(data);
            var root = document.RootElement;
            CatalogJson.RequireObject(root);
            if (CatalogJson.Integer(root, "schema_version") != 2) throw new InvalidDataException("Unsupported build manifest schema");
            var tag = CatalogJson.String(root, "tag");
            if (!VariantCatalog.TagPattern.IsMatch(tag) || tag != expectedTag)
                throw new InvalidDataException("Manifest tag differs from the selected release");
            if (!VersionPattern.IsMatch(CatalogJson.String(root, "target_game_version")))
                throw new InvalidDataException("Invalid target game version");
            var catalog = ReadFile(root.GetProperty("catalog"), "variants.json", $"sc-ko-{tag}-variants.json", false);
            var baseFile = ReadFile(root.GetProperty("base"), "base/global.ini", $"sc-ko-{tag}-base.ini", true);
            if (baseFile.Entries == 0) throw new InvalidDataException("Base entry count must be positive");
            var featureObject = root.GetProperty("features");
            CatalogJson.RequireObject(featureObject);
            var features = new Dictionary<string, ReleaseFile>(StringComparer.Ordinal);
            foreach (var property in featureObject.EnumerateObject())
            {
                var id = property.Name;
                if (!VariantCatalog.IdPattern.IsMatch(id) || id == "base") throw new InvalidDataException($"Invalid manifest feature ID: {id}");
                features.Add(id, ReadFile(property.Value, $"features/{id}.ini", $"sc-ko-{tag}-{id}.ini", true));
            }
            return new ReleaseManifest(tag, catalog, baseFile, features);
        }

        private static ReleaseFile ReadFile(System.Text.Json.JsonElement value, string expectedPath, string expectedAsset, bool needsCount)
        {
            CatalogJson.RequireObject(value);
            var path = CatalogJson.String(value, "path");
            var asset = CatalogJson.String(value, "asset");
            var hash = CatalogJson.String(value, "sha256");
            bool compressed = needsCount && asset == expectedAsset[..^4] + ".zip";
            if (path != expectedPath || (asset != expectedAsset && !compressed) || !HashPattern.IsMatch(hash))
                throw new InvalidDataException($"Invalid manifest descriptor: {expectedPath}");
            string? assetHash = value.TryGetProperty("asset_sha256", out _) ? CatalogJson.String(value, "asset_sha256") : null;
            if ((compressed && assetHash == null) || (assetHash != null &&
                (!HashPattern.IsMatch(assetHash) || (!compressed && assetHash != hash))))
                throw new InvalidDataException($"Invalid asset checksum: {expectedPath}");
            int? count = needsCount ? CatalogJson.Integer(value, "entries") : null;
            if (count < 0) throw new InvalidDataException($"Invalid entry count: {path}");
            return new ReleaseFile(path, asset, hash, count, assetHash);
        }

        public void ValidateCatalog(VariantCatalog catalog)
        {
            if (!catalog.IsModern || !Features.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(catalog.Options.Select(option => option.Id)))
                throw new InvalidDataException("Manifest feature IDs differ from the catalog");
        }

        public static void VerifyHash(byte[] data, ReleaseFile file) => VerifyHash(data, file.Sha256, file.Asset);

        public static void VerifyAssetHash(byte[] data, ReleaseFile file) =>
            VerifyHash(data, file.AssetSha256 ?? file.Sha256, file.Asset);

        private static void VerifyHash(byte[] data, string expectedHash, string asset)
        {
            var digest = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            if (digest != expectedHash) throw new InvalidDataException($"SHA-256 mismatch: {asset}");
        }

        public static void VerifyIni(byte[] data, ReleaseFile file, bool isBase)
        {
            VerifyHash(data, file);
            var entries = LocalizationComposer.ReadIni(data, file.Asset, allowEmpty: !isBase);
            if (entries.Count != file.Entries || !data.AsSpan().SequenceEqual(LocalizationComposer.WriteIni(entries)))
                throw new InvalidDataException($"Invalid entry count or noncanonical INI: {file.Asset}");
        }
    }
}
