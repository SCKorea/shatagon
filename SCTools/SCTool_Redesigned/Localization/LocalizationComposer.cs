using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SCTool_Redesigned.Localization
{
    internal static class LocalizationComposer
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

        public static Dictionary<string, string> ReadIni(byte[] data, string label, bool allowEmpty = false)
        {
            var text = StrictUtf8.GetString(data);
            if (text.StartsWith('\uFEFF')) text = text[1..];
            var lines = text.Split('\n');
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (index == lines.Length - 1 && line.Length == 0) continue;
                if (line.EndsWith('\r')) line = line[..^1];
                int separator = line.IndexOf('=');
                if (separator < 0) throw new InvalidDataException($"Malformed INI line in {label}: {index + 1}");
                var key = line[..separator];
                var value = line[(separator + 1)..];
                if (key.Length == 0 || CatalogJson.IsWhitespace(key[0]) || CatalogJson.IsWhitespace(key[^1]) || key.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
                    value.IndexOfAny(['\r', '\n', '\0']) >= 0)
                    throw new InvalidDataException($"Invalid INI entry in {label}: {index + 1}");
                if (!entries.TryAdd(key, value)) throw new InvalidDataException($"Duplicate INI key in {label}: {key}");
            }
            if (!allowEmpty && entries.Count == 0) throw new InvalidDataException($"Empty base INI: {label}");
            return entries;
        }

        public static byte[] WriteIni(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var text = new StringBuilder();
            foreach (var (key, value) in entries) text.Append(key).Append('=').Append(value).Append('\n');
            var payload = StrictUtf8.GetBytes(text.ToString());
            var output = new byte[Bom.Length + payload.Length];
            Bom.CopyTo(output, 0);
            payload.CopyTo(output, Bom.Length);
            return output;
        }

        public static byte[] Compose(byte[] baseData, VariantCatalog catalog,
            IReadOnlyDictionary<string, byte[]> parts, IEnumerable<string> selection, Action<string>? warningSink = null)
        {
            if (!catalog.IsModern) throw new InvalidDataException("Legacy packs cannot be composed as suffixes");
            var selected = catalog.OrderedSelection(selection);
            var entries = ReadIni(baseData, "base");
            var suffixes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var id in selected)
            {
                if (!parts.TryGetValue(id, out var data)) throw new InvalidDataException($"Missing selected feature file: {id}");
                var part = ReadIni(data, id, allowEmpty: true);
                if (part.Keys.Any(key => !entries.ContainsKey(key))) throw new InvalidDataException($"Feature {id} contains unknown base keys");
                suffixes.Add(id, part);
            }
            foreach (var warning in catalog.PriorityWarnings(selected)) warningSink?.Invoke(warning);
            // Dictionary enumeration retains the input key order; do not sort or trim base values.
            return WriteIni(entries.Select(entry => new KeyValuePair<string, string>(entry.Key,
                entry.Value + string.Concat(selected.Select(id => suffixes[id].GetValueOrDefault(entry.Key, ""))))));
        }
    }
}
