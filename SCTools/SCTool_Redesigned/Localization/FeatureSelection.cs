using System;
using System.Collections.Generic;
using System.Linq;

namespace SCTool_Redesigned.Localization
{
    internal record FeatureSelectionResult(IReadOnlyList<string> Selected, IReadOnlyList<string> Warnings);

    internal static class FeatureSelection
    {
        public static FeatureSelectionResult Restore(VariantCatalog catalog,
            IReadOnlyList<string>? installedFeatures, string? installedVariant)
        {
            if (!catalog.IsModern)
            {
                var selected = catalog.Contains(installedVariant!) ? [installedVariant!] : catalog.DefaultSelection;
                return new FeatureSelectionResult(selected, []);
            }
            // This is the only ID-specific code: migration of the old cumulative packs.
            IEnumerable<string>? previous = installedFeatures;
            if (previous == null)
            {
                previous = installedVariant switch
                {
                    "standard" => ["ship_en", "location_en"],
                    "bp" => ["bp", "rep", "item_reward", "ship_en", "location_en"],
                    "cd" => ["bp", "rep", "cd", "item_reward", "ship_en", "location_en"],
                    "all" => ["bp", "rep", "cd", "detail", "item_reward", "ship_en", "location_en"],
                    _ => null
                };
            }
            if (previous == null) return new FeatureSelectionResult(catalog.DefaultSelection, []);
            var ids = previous.Distinct(StringComparer.Ordinal).ToArray();
            var removed = ids.Where(id => !catalog.Contains(id)).ToArray();
            var warnings = removed.Length == 0 ? Array.Empty<string>() :
                new[] { $"이 릴리즈에 없는 저장된 부가기능을 제외했습니다: {string.Join(", ", removed)}" };
            return new FeatureSelectionResult(catalog.OrderedSelection(ids.Where(catalog.Contains)), warnings);
        }
    }
}
