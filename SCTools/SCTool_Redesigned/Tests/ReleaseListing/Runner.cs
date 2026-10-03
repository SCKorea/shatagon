using System.Net;
using System.Text;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Localization;
using SCTool_Redesigned.Settings;
using SCTool_Redesigned.Update;

internal static class Runner
{
    private const string Releases = """
        [
          {"id":1,"name":"0.67.0","tag_name":"0.67.0","draft":false,"prerelease":false,
           "published_at":"2026-10-01T00:00:00Z","created_at":"2026-09-30T00:00:00Z",
           "zipball_url":"https://api.github.com/repos/test/patch/zipball/0.67.0","assets":[]},
          {"id":2,"name":"0.67.1","tag_name":"0.67.1","draft":false,"prerelease":true,
           "published_at":"2026-10-02T00:00:00Z","created_at":"2026-10-01T00:00:00Z",
           "zipball_url":"https://api.github.com/repos/test/patch/zipball/0.67.1","assets":[]},
          {"id":3,"name":"0.67.2","tag_name":"0.67.2","draft":true,"prerelease":false,
           "published_at":null,"created_at":"2026-10-03T00:00:00Z","zipball_url":null,
           "assets":[
             {"name":"sc-ko-0.67.2-build-manifest.json","url":"https://api.github.com/assets/manifest",
              "browser_download_url":"https://github.com/test/patch/releases/download/0.67.2/sc-ko-0.67.2-build-manifest.json"},
             {"name":"sc-ko-0.67.2-base.zip","url":"https://api.github.com/assets/base",
              "browser_download_url":"https://github.com/test/patch/releases/download/0.67.2/sc-ko-0.67.2-base.zip"}
           ]}
        ]
        """;
    private static int _passed;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(["--live-only"]))
                await RunLive();
            else if (args.Length == 0)
            {
                RunSettings();
                RunMetadata();
                await RunLists();
            }
            else throw new ArgumentException("Use no arguments for offline checks or --live-only for the authorized 0.67.2 draft");
            Console.WriteLine($"PASS: {_passed} release listing checks");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static async Task RunAsync(string name, Func<Task> test)
    {
        await test();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void RunSettings()
    {
        var settings = JsonHelper.Read<AppSettings>("""
            {"Nightly":false,"LIVE_Localization":{"Installations":[
              {"Mode":"PTU","Repository":"sckorea/sc_ko","Type":"gitHub","AllowPreRelease":true},
              {"Mode":"LIVE","Repository":"sckorea/sc_ko","Type":"gitHub","AllowPreRelease":false}
            ]}}
            """)!;
        Run("saved true overrides Nightly false and matches mode/repository case", () =>
            Check(settings.GetAllowPreRelease("ptu", "SCKorea/SC_ko"), "Saved AllowPreRelease was ignored"));
        Run("saved false overrides Nightly true", () =>
        {
            settings.Nightly = true;
            Check(!settings.GetAllowPreRelease("LIVE", "sckorea/sc_ko"), "Nightly overwrote explicit false");
        });
        Run("missing installation and other repositories use Nightly as the initial default", () =>
        {
            Check(settings.GetAllowPreRelease("EPTU", "sckorea/sc_ko"), "New installation lost the Nightly default");
            settings.Nightly = false;
            Check(!settings.GetAllowPreRelease("PTU", "other/repository"), "Another repository inherited the old selection");
        });
        Run("settings round-trip preserves independent mode preferences", () =>
        {
            var restored = JsonHelper.Read<AppSettings>(JsonHelper.Write(settings))!;
            Check(restored.GetAllowPreRelease("PTU", "sckorea/sc_ko") && !restored.GetAllowPreRelease("LIVE", "sckorea/sc_ko"), "Settings serialization changed AllowPreRelease");
        });
    }

    private static void RunMetadata()
    {
        var raw = JsonHelper.Read<CustomGitHubRepository.GitRelease[]>(Releases)!;
        var factory = CustomUpdateInfo.Factory.NewWithVersionByName();
        Run("draft without source URL or published date retains its tag and assets", () =>
        {
            Check(raw[2].Published == null && raw[2].ZipUrl == null, "Draft null fields were not parsed");
            var draft = factory.CreateWithDownloadSourceCode(raw[2]) as CustomUpdateInfo;
            Check(draft != null && draft.TagName == "0.67.2" && draft.Assets.Length == 2, "Valid draft was dropped");
            Check(draft!.Assets[1].Name == "sc-ko-0.67.2-base.zip" && draft.Assets[1].ApiUrl == "https://api.github.com/assets/base", "Draft attachment metadata was lost");
            Check(draft.PreRelease && draft.Released == DateTimeOffset.Parse("2026-10-03T00:00:00Z"), "Draft classification/date is wrong");
        });
        Run("published release keeps its publication date and stable status", () =>
        {
            var stable = factory.CreateWithDownloadSourceCode(raw[0])!;
            Check(!stable.PreRelease && stable.Released == DateTimeOffset.Parse("2026-10-01T00:00:00Z"), "Published metadata changed");
        });
        Run("unnamed draft displays its tag", () =>
        {
            var rawDraft = JsonHelper.Read<CustomGitHubRepository.GitRelease>("""
                {"tag_name":"0.67.3","name":null,"draft":true,"published_at":null,"zipball_url":null,"assets":[]}
                """)!;
            var draft = factory.CreateWithDownloadSourceCode(rawDraft)!;
            Check(draft.Name == "0.67.3" && draft.GetVersion() == "0.67.3", "Unnamed draft cannot be selected");
        });
        Run("missing tag and missing published source URL remain invalid", () =>
        {
            foreach (string json in new[] {
                """{"name":"invalid","tag_name":"","draft":true,"zipball_url":null}""",
                """{"name":"invalid","tag_name":"0.67.4","draft":false,"zipball_url":null}"""
            }) Check(factory.CreateWithDownloadSourceCode(JsonHelper.Read<CustomGitHubRepository.GitRelease>(json)!) == null, "Invalid metadata was accepted");
        });
        Run("draft asset factory also uses the creation date", () =>
        {
            var draft = factory.CreateWithDownloadAsset(raw[2])!;
            Check(draft.PreRelease && draft.Released == raw[2].Created, "Asset update lost its draft status/date");
        });
    }

    private static CustomGitHubRepository Repository(HttpClient http, bool allow, string repository = "test/patch") =>
        new(http, GitHubDownloadType.Sources, CustomUpdateInfo.Factory.NewWithVersionByName(), "patch", repository)
        { AllowPreReleases = allow, AuthToken = "test-token" };

    private static async Task RunLists()
    {
        await RunAsync("allowed list includes draft and prerelease through authenticated GitHub API", async () =>
        {
            using var handler = new FakeHandler();
            using var http = new HttpClient(handler);
            using var repository = Repository(http, true);
            var list = (await repository.RefreshUpdatesAsync(CancellationToken.None)).ToArray();
            Check(list.Select(info => info.TagName).SequenceEqual(["0.67.2", "0.67.1", "0.67.0"]), "Allowed version list omitted the draft");
            Check(handler.Requests.Count == 1 && handler.Requests[0].Url == "https://api.github.com/repos/test/patch/releases?per_page=100" &&
                handler.Requests[0].Auth == "token test-token", "Draft list was not fetched/authenticated from GitHub");
        });
        await RunAsync("disabled list excludes draft even when its prerelease field is false", async () =>
        {
            using var handler = new FakeHandler();
            using var http = new HttpClient(handler);
            using var repository = Repository(http, false);
            var list = await repository.GetAllAsync(CancellationToken.None);
            Check(list.Select(info => info.TagName).SequenceEqual(["0.67.0"]), "Disabled list included a draft or prerelease");
        });
        await RunAsync("GitHub credentials are not sent to an intermediary release URL", async () =>
        {
            using var handler = new FakeHandler();
            using var http = new HttpClient(handler);
            using var repository = Repository(http, false);
            repository.ChangeReleasesUrl("https://example.test/releases");
            await repository.GetAllAsync(CancellationToken.None);
            Check(handler.Requests.Count == 1 && handler.Requests[0].Auth == null, "GitHub token was attached to an intermediary request");
        });
        await RunAsync("true/false/true refresh does not keep a previously visible draft", async () =>
        {
            using var handler = new FakeHandler();
            using var http = new HttpClient(handler);
            using var repository = Repository(http, true);
            foreach (bool allow in new[] { true, false, true })
            {
                repository.AllowPreReleases = allow;
                var list = (await repository.RefreshUpdatesAsync(CancellationToken.None)).ToArray();
                Check(list.Length == (allow ? 3 : 1) && list.Any(info => info.TagName == "0.67.2") == allow, "Visibility did not follow the current flag");
            }
        });
        await RunAsync("authentication failure stays fatal", async () =>
        {
            using var handler = new FakeHandler { Status = HttpStatusCode.Forbidden };
            using var http = new HttpClient(handler);
            using var repository = Repository(http, true);
            try { await repository.RefreshUpdatesAsync(CancellationToken.None); }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
            {
                Check(handler.Requests.Count == 1, "Authentication failure fell back to another release source");
                return;
            }
            throw new Exception("Authentication failure was swallowed");
        });
    }

    private static async Task RunLive()
    {
        await RunAsync("actual private 0.67.2 draft follows enabled/disabled visibility", async () =>
        {
            var token = Environment.GetEnvironmentVariable("SC_DRAFT_LIST_TEST_TOKEN");
            if (string.IsNullOrEmpty(token)) throw new InvalidOperationException("Set SC_DRAFT_LIST_TEST_TOKEN for the authorized read-only live check");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Shatagon/draft-list-verification");
            using var repository = Repository(http, true, "SCKorea/SC_ko");
            repository.AuthToken = token;
            var allowed = (await repository.RefreshUpdatesAsync(CancellationToken.None)).ToArray();
            var raw = await repository.GetReleasesAsync(true, CancellationToken.None);
            Check(raw!.Any(release => release.TagName == "0.67.2" && release.Draft == true), "Actual draft was not returned by GitHub");
            var draft = allowed.OfType<CustomUpdateInfo>().SingleOrDefault(info => info.TagName == "0.67.2");
            Check(draft != null && draft.Assets.Any(asset => asset.Name == "sc-ko-0.67.2-base.zip"), "Actual draft is absent from the allowed list or lost its compressed base");
            repository.AllowPreReleases = false;
            var stable = (await repository.RefreshUpdatesAsync(CancellationToken.None)).ToArray();
            Check(stable.All(info => !info.PreRelease) && stable.All(info => info.TagName != "0.67.2"), "Disabled list contains the draft");
            Console.WriteLine($"Live 0.67.2 draft: enabled=visible, disabled=hidden; allowed={allowed.Length}, stable={stable.Length}");
        });
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public List<(string Url, string? Auth)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString()));
            var response = new HttpResponseMessage(Status) { Content = new StringContent(Releases, Encoding.UTF8, "application/json") };
            response.Headers.Add("X-RateLimit-Remaining", "4999");
            return Task.FromResult(response);
        }
    }
}
