using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Localization;

namespace SCTool_Redesigned.Update
{
    class CustomUpdateInfo : UpdateInfo
    {
        private readonly bool _namedVersion;

        public override string GetVersion() => _namedVersion ? Name : TagName;

        [JsonConstructor]
        public CustomUpdateInfo(string name, string tagName, string downloadUrl)
            : base(name, tagName, downloadUrl)
        {

        }

        private CustomUpdateInfo(string name, string tagName, string downloadUrl, bool namedVersion)
            : base(name, tagName, downloadUrl)
        {
            _namedVersion = namedVersion;
        }

        public new class Factory
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
                if (string.IsNullOrEmpty(release.Name) || string.IsNullOrEmpty(release.TagName) ||
                    string.IsNullOrEmpty(release.ZipUrl))
                {
                    return null;
                }
                return new CustomUpdateInfo(release.Name, release.TagName, release.ZipUrl, _namedVersion)
                {
                    PreRelease = release.PreRelease,
                    Released = release.Published
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
                return new CustomUpdateInfo(release.Name, release.TagName, downloadUrl, _namedVersion)
                {
                    PreRelease = release.PreRelease,
                    Released = release.Published
                };
            }

            public GitHubUpdateInfo.Factory GetBaseGitHubUpdateInfo()
            {
                return _namedVersion ? GitHubUpdateInfo.Factory.NewWithVersionByName() : GitHubUpdateInfo.Factory.NewWithVersionByTagName();
            }
        }
    }
}
