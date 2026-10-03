using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SCTool_Redesigned.Localization
{
    internal record VariantOption(string Id, string Name, string Description, int Prior);

    // Shared with the dependency-free composer and its test runner, without WPF or repository state.
    internal sealed class VariantCatalog
    {
        internal static readonly Regex IdPattern = new(@"\A[a-z0-9][a-z0-9_-]*\z", RegexOptions.CultureInvariant);
        internal static readonly Regex TagPattern = new(@"\A[A-Za-z0-9][A-Za-z0-9._-]*\z", RegexOptions.CultureInvariant);
        public bool IsModern { get; }
        public IReadOnlyList<VariantOption> Options { get; }
        public IReadOnlyList<string> DefaultSelection { get; }
        public string? Fallback { get; }
        private readonly Dictionary<string, VariantOption> _byId;

        private VariantCatalog(bool modern, IEnumerable<VariantOption> options,
            IEnumerable<string> defaults, string? fallback = null)
        {
            IsModern = modern;
            Options = options.OrderBy(option => option.Prior).ThenBy(option => option.Id, StringComparer.Ordinal).ToArray();
            _byId = Options.ToDictionary(option => option.Id, StringComparer.Ordinal);
            DefaultSelection = OrderedSelection(defaults);
            Fallback = fallback;
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        public string[] OrderedSelection(IEnumerable<string> selection)
        {
            var ids = selection.ToArray();
            if (ids.Any(id => !Contains(id)) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new InvalidDataException("Unknown or duplicate selected feature ID");
            return ids.OrderBy(id => _byId[id].Prior).ThenBy(id => id, StringComparer.Ordinal).ToArray();
        }

        public IEnumerable<string> PriorityWarnings(IEnumerable<string> selection) =>
            OrderedSelection(selection).GroupBy(id => _byId[id].Prior).Where(group => group.Count() > 1)
                .Select(group => $"부가기능 prior={group.Key}가 같습니다: {string.Join(", ", group)}. ID 순서로 연결합니다.");

        public static VariantCatalog ParseModern(byte[] data) => Parse(data, true);
        public static VariantCatalog ParseLegacy(byte[] data) => Parse(data, false);

        private static VariantCatalog Parse(byte[] data, bool modern)
        {
            using var document = CatalogJson.Parse(data);
            var root = document.RootElement;
            CatalogJson.RequireObject(root);
            if (modern)
            {
                if (CatalogJson.Integer(root, "schema_version") != 2)
                    throw new InvalidDataException("Unsupported feature catalog schema");
            }
            else if (root.TryGetProperty("schema_version", out var schema) &&
                (schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version) || version != 1))
                throw new InvalidDataException("Unsupported legacy catalog schema");

            var variants = CatalogJson.Array(root, "variants");
            var options = new List<VariantOption>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in variants.EnumerateArray())
            {
                CatalogJson.RequireObject(entry);
                var id = CatalogJson.String(entry, "id");
                if (!IdPattern.IsMatch(id) || (modern && id == "base") || !seen.Add(id))
                    throw new InvalidDataException($"Invalid, reserved or duplicate feature ID: {id}");
                var name = CatalogJson.String(entry, "name");
                var description = CatalogJson.String(entry, "description");
                if (name.Length == 0 || name.All(CatalogJson.IsWhitespace))
                    throw new InvalidDataException($"Feature name is empty: {id}");
                int prior = options.Count;
                if (modern)
                {
                    prior = CatalogJson.Integer(entry, "prior");
                    if (prior < 0 || CatalogJson.String(entry, "generator").Length == 0)
                        throw new InvalidDataException($"Invalid priority or generator: {id}");
                }
                if (entry.TryGetProperty("flags", out var flags))
                    CatalogJson.StringArray(flags, "flags", unique: true);
                options.Add(new VariantOption(id, name, description, prior));
            }
            string[] defaults;
            string? fallback = null;
            if (modern)
                defaults = CatalogJson.StringArray(CatalogJson.Array(root, "default_selection"), "default_selection", unique: true);
            else
            {
                defaults = [CatalogJson.String(root, "default_selection")];
                fallback = CatalogJson.String(root, "fallback");
                if (!seen.Contains(fallback))
                    throw new InvalidDataException("Legacy fallback is not in the catalog");
            }
            var catalog = new VariantCatalog(modern, options, defaults, fallback);
            if (!modern && catalog.DefaultSelection.Count != 1)
                throw new InvalidDataException("Select one legacy translation pack");
            return catalog;
        }

        // Only used after a tagged legacy catalog returns 404 and its standard ZIP is confirmed.
        public static VariantCatalog LegacyBaseOnly() => new(false,
            [new VariantOption("standard", "기본 번역", "이 릴리즈의 기본 번역을 설치합니다.", 0)], ["standard"], "standard");
    }

    internal static class CatalogJson
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        // Python str.strip also treats these four ASCII separators as whitespace.
        public static bool IsWhitespace(char value) => char.IsWhiteSpace(value) || value is >= '\u001C' and <= '\u001F';

        public static JsonDocument Parse(byte[] data)
        {
            var value = StrictUtf8.GetString(data);
            if (value.StartsWith('\uFEFF')) value = value[1..];
            var document = JsonDocument.Parse(value);
            try { CheckProperties(document.RootElement); }
            catch { document.Dispose(); throw; }
            return document;
        }

        private static void CheckProperties(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    if (!seen.Add(property.Name)) throw new InvalidDataException($"Duplicate JSON property: {property.Name}");
                    CheckProperties(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray()) CheckProperties(item);
        }

        public static void RequireObject(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a JSON object");
        }

        public static string String(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"Expected a string: {name}");
            return item.GetString()!;
        }

        public static int Integer(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var result))
                throw new InvalidDataException($"Expected a 32-bit integer: {name}");
            return result;
        }

        public static JsonElement Array(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var item) || item.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException($"Expected an array: {name}");
            return item;
        }

        public static string[] StringArray(JsonElement value, string label, bool unique)
        {
            if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"Expected an array: {label}");
            var list = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) throw new InvalidDataException($"Expected string IDs: {label}");
                list.Add(item.GetString()!);
            }
            if (unique && list.Distinct(StringComparer.Ordinal).Count() != list.Count)
                throw new InvalidDataException($"Duplicate value: {label}");
            return list.ToArray();
        }
    }
}
