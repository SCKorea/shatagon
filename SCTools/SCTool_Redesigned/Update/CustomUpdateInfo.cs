using System.Linq;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Localization;

namespace SCTool_Redesigned.Update
{
    class CustomUpdateInfo : UpdateInfo
    {
        private readonly bool _namedVersion;
        public CustomGitHubRepository.GitAsset[] Assets { get; }

        public override string GetVersion() => _namedVersion ? Name : TagName;

        [JsonConstructor]
        public CustomUpdateInfo(string name, string tagName, string downloadUrl)
            : base(name, tagName, downloadUrl)
        {
            Assets = [];
        }

        private CustomUpdateInfo(string name, string tagName, string downloadUrl, bool namedVersion, CustomGitHubRepository.GitAsset[] assets)
            : base(name, tagName, downloadUrl)
        {
            _namedVersion = namedVersion;
            Assets = assets;
        }

        public class Factory
        {
            private readonly bool _namedVersion;

            public static Factory NewWithVersionByName() => new Factory(true);
            public static Factory NewWithVersionByTagName() => new Factory(false);

            Factory(bool namedVersion)
            {
                _namedVersion = namedVersion;
            }

            public UpdateInfo? CreateWithDownloadSourceCode(CustomGitHubRepository.GitRelease release)
            {
                if (string.IsNullOrEmpty(release.TagName) ||
                    (string.IsNullOrEmpty(release.ZipUrl) && release.Draft != true))
                {
                    return null;
                }
                // GitHub drafts have no source archive yet; feature releases use their assets.
                var name = string.IsNullOrEmpty(release.Name) ? release.TagName : release.Name;
                return new CustomUpdateInfo(name, release.TagName, release.ZipUrl ?? "", _namedVersion, release.Assets ?? [])
                {
                    PreRelease = release.PreRelease == true || release.Draft == true,
                    Released = release.Published ?? release.Created
                };
            }

            public UpdateInfo? CreateWithDownloadAsset(CustomGitHubRepository.GitRelease release)
            {
                var downloadUrl = release.Assets.FirstOrDefault()?.ZipUrl;
                if (string.IsNullOrEmpty(release.Name) || string.IsNullOrEmpty(release.TagName) ||
                    (downloadUrl == null) || string.IsNullOrEmpty(downloadUrl))
                {
                    return null;
                }
                return new CustomUpdateInfo(release.Name, release.TagName, downloadUrl, _namedVersion, release.Assets ?? [])
                {
                    PreRelease = release.PreRelease == true || release.Draft == true,
                    Released = release.Published ?? release.Created
                };
            }

            public GitHubUpdateInfo.Factory GetBaseGitHubUpdateInfo()
            {
                return _namedVersion ? GitHubUpdateInfo.Factory.NewWithVersionByName() : GitHubUpdateInfo.Factory.NewWithVersionByTagName();
            }
        }
    }
}
