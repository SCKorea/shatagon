using System;
using System.Collections.Generic;
using System.Linq;
using SCTool_Redesigned.Update;

namespace SCTool_Redesigned.Localization
{
    internal record VariantOption(string Id, string Name, string Description)
    {
        public override string ToString() => $"{Name} — {Description}";
    }

    internal record VariantDownload(string ActualVariant, string Url, bool UseApi, string? Warning);

    internal static class VariantCatalog
    {
        // Mirrors release/variants.json in the translation repository until its tagged manifest is available.
        public const string DefaultSelection = "cd";
        public const string Fallback = "standard";
        public static readonly IReadOnlyList<VariantOption> Options = new[]
        {
            new VariantOption("standard", "기본", "추가 정보 기능 없음"),
            new VariantOption("bp", "청사진", "청사진 + 평판"),
            new VariantOption("cd", "쿨다운", "청사진 + 평판 + 쿨다운"),
            new VariantOption("all", "전체", "모든 미션 정보")
        };

        public static bool IsLegacy(CustomUpdateInfo info) =>
            (info.Assets?.Length ?? 0) == 0 && LegacyReleaseTags.All.Contains(info.TagName);

        public static VariantDownload Resolve(CustomUpdateInfo info, string requested)
        {
            if (IsLegacy(info))
            {
                return new VariantDownload("legacy", info.DownloadUrl, false,
                    $"{info.TagName} 릴리즈는 기능선택을 지원하지 않습니다. 해당 릴리즈의 기존 번역을 설치합니다.");
            }
            if (!Options.Any(option => option.Id == requested))
                throw new InvalidOperationException($"Unknown variant: {requested}");

            var assets = info.Assets ?? [];
            var chosen = assets.SingleOrDefault(asset => asset.Name == $"sc-ko-{info.TagName}-{requested}.zip");
            string? warning = null;
            var actual = requested;
            if (chosen == null)
            {
                actual = Fallback;
                chosen = assets.SingleOrDefault(asset => asset.Name == $"sc-ko-{info.TagName}-{Fallback}.zip");
                warning = $"{info.TagName}의 {requested} 파일이 없어 일반 번역을 적용합니다.";
            }
            if (chosen == null)
                throw new InvalidOperationException($"No default variant asset for {info.TagName}");

            var url = !string.IsNullOrWhiteSpace(chosen.ApiUrl) ? chosen.ApiUrl : chosen.ZipUrl;
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException($"Asset URL is missing for {info.TagName}/{actual}");
            return new VariantDownload(actual, url, !string.IsNullOrWhiteSpace(chosen.ApiUrl), warning);
        }
    }
}
