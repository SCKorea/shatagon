using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SCTool_Redesigned.Localization;

internal static class Runner
{
    private static int _passed;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--live-only")
        {
            try { await RunLiveRelease(args[1]); return 0; }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        }
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Pass NEWTRY/tools/tests/fixtures/feature-compose.json, optionally followed by a generated candidate root.");
            return 2;
        }
        try
        {
            var fixture = JsonNode.Parse(File.ReadAllBytes(args[0]))!.AsObject();
            RunFixture(fixture);
            RunCatalogValidation(fixture);
            RunSelectionMigration();
            await RunDownloads(fixture);
            await RunCompressedDownloads(fixture);
            if (args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal)) await RunGeneratedCandidate(args[1]);
            int liveIndex = Array.IndexOf(args, "--live-tag");
            if (liveIndex >= 0)
            {
                if (liveIndex + 1 >= args.Length) throw new ArgumentException("--live-tag needs a tag");
                await RunLiveRelease(args[liveIndex + 1]);
            }
            Console.WriteLine($"PASS: {_passed} feature composition/release checks");
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

    private static void Run(string name, Action action)
    {
        action();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static async Task RunAsync(string name, Func<Task> action)
    {
        await action();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static T Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T exception) { return exception; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static async Task<T> RejectAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T exception) { return exception; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private static byte[] Bytes(JsonNode node) => Utf8.GetBytes(node.GetValue<string>());
    private static string[] Ids(JsonNode node) => node.AsArray().Select(item => item!.GetValue<string>()).ToArray();
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static Dictionary<string, byte[]> Parts(JsonNode fixture) => fixture["parts"]!.AsObject()
        .ToDictionary(item => item.Key, item => Bytes(item.Value!), StringComparer.Ordinal);

    private static void RunFixture(JsonObject fixture)
    {
        var catalog = VariantCatalog.ParseModern(Utf8.GetBytes(fixture["catalog"]!.ToJsonString()));
        var baseData = Bytes(fixture["base"]!);
        foreach (var item in fixture["cases"]!.AsArray())
        {
            var test = item!.AsObject();
            Run("shared fixture: " + test["name"]!.GetValue<string>(), () =>
            {
                var parts = Parts(fixture);
                if (test["parts"] is JsonObject overrides)
                    foreach (var replacement in overrides) parts[replacement.Key] = Bytes(replacement.Value!);
                var selected = Ids(test["selected"]!);
                var warnings = new List<string>();
                if (test["error"]?.GetValue<bool>() == true)
                    Reject<InvalidDataException>(() => LocalizationComposer.Compose(baseData, catalog, parts, selected, warnings.Add));
                else
                {
                    var output = LocalizationComposer.Compose(baseData, catalog, parts, selected, warnings.Add);
                    Check(output.SequenceEqual(Bytes(test["expected"]!)), "Python/C# fixture bytes differ");
                    Check(warnings.Count == test["warnings"]!.GetValue<int>(), "Priority warning count differs");
                }
            });
        }
        Run("different priorities override selection and catalog order", () =>
        {
            var changed = fixture["catalog"]!.DeepClone();
            changed["variants"]![0]!["prior"] = 1;
            var ordered = VariantCatalog.ParseModern(Utf8.GetBytes(changed.ToJsonString()));
            var output = Utf8.GetString(LocalizationComposer.Compose(baseData, ordered, Parts(fixture), ["marker", "ship_en"]));
            Check(output.Contains("RSI 오로라 Mk I MR [RSI Aurora Mk I MR] <EM4>[태그]</EM4>\n"), "Priority ordering failed");
        });
        Run("strict UTF-8 and physical lines", () =>
        {
            Reject<DecoderFallbackException>(() => LocalizationComposer.ReadIni([0xFF], "bad"));
            foreach (var bad in new[] { "key=value\nkey=again\n", "key=one\rtwo\n", " key=value\n", "\u001Ckey=value\n", "key=bad\0\n", "\n" })
                Reject<InvalidDataException>(() => LocalizationComposer.ReadIni(Utf8.GetBytes(bad), "bad"));
            Check(LocalizationComposer.ReadIni(Utf8.GetBytes("key=one\u2028two\n"), "unicode")["key"] == "one\u2028two", "Unicode separator was treated as a physical line");
        });
    }

    private static void RunCatalogValidation(JsonObject fixture)
    {
        foreach (var change in new Action<JsonNode>[]
        {
            node => node["variants"]![0]!["id"] = "base",
            node => node["variants"]![0]!["id"] = "bad\n",
            node => node["variants"]![1]!["id"] = "ship_en",
            node => node["variants"]![0]!["prior"] = -1,
            node => node["variants"]![0]!["prior"] = true,
            node => node["variants"]![0]!["prior"] = 1.5,
            node => node["variants"]![0]!["prior"] = 2147483648L,
            node => node["variants"]![0]!["description"] = null,
            node => node["variants"]![0]!["name"] = "\u001C",
            node => node["variants"]![0]!["flags"] = new JsonArray("bp", "bp"),
            node => node["default_selection"] = new JsonArray("ship_en", "ship_en"),
            node => node["default_selection"] = new JsonArray("unknown"),
            node => node["schema_version"] = 3,
        })
        {
            Run("invalid modern catalog", () =>
            {
                var node = fixture["catalog"]!.DeepClone();
                change(node);
                Reject<InvalidDataException>(() => VariantCatalog.ParseModern(Utf8.GetBytes(node.ToJsonString())));
            });
        }
        Run("duplicate JSON properties rejected", () =>
            Reject<InvalidDataException>(() => VariantCatalog.ParseModern(Utf8.GetBytes("{\"schema_version\":2,\"schema_version\":2}"))));
        Run("empty catalog permits base only", () =>
        {
            var catalog = VariantCatalog.ParseModern(Utf8.GetBytes("{\"schema_version\":2,\"variants\":[],\"default_selection\":[]}"));
            Check(catalog.Options.Count == 0 && catalog.DefaultSelection.Count == 0, "Empty catalog failed");
            var output = LocalizationComposer.Compose(Utf8.GetBytes("x=한글\n"), catalog, new Dictionary<string, byte[]>(), []);
            Check(Utf8.GetString(output) == "\uFEFFx=한글\n", "Empty catalog composition failed");
        });
    }

    private static void RunSelectionMigration()
    {
        var ids = new[] { "bp", "rep", "cd", "detail", "item_reward", "ship_en", "location_en", "future" };
        var catalog = VariantCatalog.ParseModern(JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 2, default_selection = new[] { "bp", "future" },
            variants = ids.Select((id, index) => new { id, name = id, description = "", prior = index, generator = "unknown_future_generator" })
        }));
        Run("null record uses defaults; empty record stays empty", () =>
        {
            Check(FeatureSelection.Restore(catalog, null, "legacy").Selected.SequenceEqual(["bp", "future"]), "Null selection did not use defaults");
            Check(FeatureSelection.Restore(catalog, [], "all").Selected.Count == 0, "Empty selection was overwritten");
            Check(FeatureSelection.Restore(catalog, ["rep"], "legacy").Selected.SequenceEqual(["rep"]), "New defaults were automatically added");
        });
        foreach (var old in new[] { "standard", "bp", "cd", "all" })
            Run("migration from " + old, () =>
            {
                var restored = FeatureSelection.Restore(catalog, null, old);
                Check(restored.Selected.Contains("ship_en") && restored.Selected.Contains("location_en"), "English labels lost during migration");
                Check(restored.Selected.Contains("item_reward") == (old != "standard"), "Old unconditional item rewards lost");
                Check(restored.Selected.Contains("detail") == (old == "all"), "Detail migration failed");
                Check(restored.Selected.Contains("cd") == (old is "cd" or "all"), "Cooldown migration failed");
                Check(!restored.Selected.Contains("future"), "New defaults were added during migration");
            });
        Run("removed stored ID is omitted with notice", () =>
        {
            var restored = FeatureSelection.Restore(catalog, ["rep", "removed"], "all");
            Check(restored.Selected.SequenceEqual(["rep"]) && restored.Warnings.Count == 1, "Removed selection was not handled");
        });
    }

    private static async Task RunDownloads(JsonObject fixture)
    {
        await RunAsync("tag-scoped private assets and selected downloads only", async () =>
        {
            using var sample = new ModernSample(fixture);
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            Check(sample.Handler.Requests.Count == 2, "Base/features were downloaded during catalog loading");
            var prepared = await sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, CancellationToken.None);
            Check(ReadZip(prepared.ZipPath).SequenceEqual(LocalizationComposer.Compose(sample.Base, release.Catalog!, sample.Parts, ["ship_en"])), "Downloaded composition is wrong");
            Check(prepared.Features!.SequenceEqual(["ship_en"]), "Actual features are wrong");
            Check(sample.Handler.Requests.Count == 4 && !sample.Handler.Requests.Any(request => request.Url == sample.Url("marker")), "Unselected feature was downloaded");
            Check(sample.Handler.Requests.All(request => request.Auth == "token test-token" && request.Accept == "application/octet-stream"), "Private API headers missing");
            Check(!sample.Handler.Requests.Any(request => request.Url.Contains("master")), "Used master metadata");
        });
        await RunAsync("base-only download and reinstall are stable", async () =>
        {
            using var sample = new ModernSample(fixture);
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            var first = await sample.Client.PrepareAsync(release, ["marker", "ship_en"], sample.Directory, CancellationToken.None);
            Check(first.Warnings.Count == 1, "Priority tie was not reported");
            var second = await sample.Client.PrepareAsync(release, [], sample.Directory, CancellationToken.None);
            Check(second.Features!.Count == 0 && ReadZip(second.ZipPath).SequenceEqual(sample.Base), "Old suffixes survived base-only reinstall");
        });
        await RunAsync("missing selected asset warns and saves only applied features", async () =>
        {
            using var sample = new ModernSample(fixture);
            var info = sample.Info with { Assets = sample.Info.Assets.Where(asset => asset.Name != "sc-ko-0.67.2-marker.ini").ToArray() };
            var release = await sample.Client.LoadAsync(info, CancellationToken.None);
            var prepared = await sample.Client.PrepareAsync(release, ["marker", "ship_en"], sample.Directory, CancellationToken.None);
            Check(prepared.Features!.SequenceEqual(["ship_en"]) && prepared.Warnings.Count == 1, "Missing feature did not fall back correctly");
            Check(ReadZip(prepared.ZipPath).SequenceEqual(LocalizationComposer.Compose(sample.Base, release.Catalog!, sample.Parts, ["ship_en"])), "Missing feature leaked into composition");
        });
        await RunAsync("corruption fails before making an installation package", async () =>
        {
            using var sample = new ModernSample(fixture);
            sample.Handler.Routes[sample.Url("ship_en")] = new Reply(Utf8.GetBytes("corrupt"));
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, CancellationToken.None));
            Check(!System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "Failure left a package behind");
        });
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound })
            await RunAsync("listed feature HTTP failure: " + status, async () =>
            {
                using var sample = new ModernSample(fixture);
                sample.Handler.Routes[sample.Url("ship_en")] = new Reply([], status);
                var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
                var exception = await RejectAsync<HttpRequestException>(() => sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, CancellationToken.None));
                Check(exception.StatusCode == status && !System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "HTTP error was swallowed or left a package");
            });
        await RunAsync("metadata hash and tag validation", async () =>
        {
            using var sample = new ModernSample(fixture);
            sample.Handler.Routes[sample.Url("catalog")] = new Reply(Utf8.GetBytes("{}"));
            await RejectAsync<InvalidDataException>(() => sample.Client.LoadAsync(sample.Info, CancellationToken.None));
            Reject<InvalidDataException>(() => ReleaseManifest.Parse(sample.Manifest, "different-tag"));
        });
        await RunAsync("missing new metadata is never treated as a legacy release", async () =>
        {
            using var sample = new ModernSample(fixture);
            var assets = sample.Info.Assets.Where(asset => asset.Name != "sc-ko-0.67.2-build-manifest.json")
                .Append(new ReleaseAsset("sc-ko-0.67.2-standard.zip", "https://api.github.com/legacy", null)).ToArray();
            await RejectAsync<InvalidDataException>(() => sample.Client.LoadAsync(sample.Info with { Assets = assets }, CancellationToken.None));
        });
        await RunAsync("missing base metadata fails", async () =>
        {
            using var sample = new ModernSample(fixture);
            var info = sample.Info with { Assets = sample.Info.Assets.Where(asset => asset.Name != "sc-ko-0.67.2-base.ini").ToArray() };
            await RejectAsync<InvalidDataException>(() => sample.Client.LoadAsync(info, CancellationToken.None));
        });
        await RunAsync("cancellation interrupts catalog and feature requests", async () =>
        {
            using var sample = new ModernSample(fixture);
            using var cancel = new CancellationTokenSource();
            sample.Handler.PausedUrl = sample.Url("ship_en");
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            var preparing = sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, cancel.Token);
            await sample.Handler.Paused.Task;
            cancel.Cancel();
            await RejectAsync<OperationCanceledException>(() => preparing);
            Check(!System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "Cancellation left a package");
            sample.Handler.PausedUrl = sample.Url("manifest");
            sample.Handler.Paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancelLoad = new CancellationTokenSource();
            var loading = sample.Client.LoadAsync(sample.Info, cancelLoad.Token);
            await sample.Handler.Paused.Task;
            cancelLoad.Cancel();
            await RejectAsync<OperationCanceledException>(() => loading);
        });
        await RunAsync("legacy catalog uses selected tag and keeps single-pack behavior", async () =>
        {
            using var sample = new ModernSample(fixture);
            var (info, contents) = AddLegacy(sample.Handler);
            var release = await sample.Client.LoadAsync(info, CancellationToken.None);
            Check(release.Format == LocalizationReleaseFormat.LegacyPacks && release.Catalog!.DefaultSelection.SequenceEqual(["bp"]), "Legacy catalog failed");
            var request = sample.Handler.Requests.Single();
            Check(request.Url == contents && request.Accept == "application/vnd.github.raw+json" && request.Auth == "token test-token", "Legacy catalog request was not tag-scoped/authenticated");
            await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, ["standard", "bp"], sample.Directory, CancellationToken.None));
            var prepared = await sample.Client.PrepareAsync(release, ["bp"], sample.Directory, CancellationToken.None);
            Check(prepared.LegacyVariant == "bp" && prepared.Features == null, "Legacy installation state failed");
            Check(Utf8.GetString(ReadZip(prepared.ZipPath)) == "legacy=BP\n", "Legacy pack was composed or changed");
            var missing = info with { Assets = info.Assets.Where(asset => !asset.Name.EndsWith("-bp.zip", StringComparison.Ordinal)).ToArray() };
            var fallback = await sample.Client.PrepareAsync(release with { Info = missing }, ["bp"], sample.Directory, CancellationToken.None);
            Check(fallback.LegacyVariant == "standard" && fallback.Warnings.Count == 1, "Legacy fallback failed");
        });
        await RunAsync("confirmed legacy catalog 404 offers base only", async () =>
        {
            using var sample = new ModernSample(fixture);
            var (info, contents) = AddLegacy(sample.Handler);
            sample.Handler.Routes[contents] = new Reply([], HttpStatusCode.NotFound);
            var release = await sample.Client.LoadAsync(info, CancellationToken.None);
            Check(release.Catalog!.Options.Count == 1 && release.Catalog.DefaultSelection.SequenceEqual(["standard"]) && release.Notices.Count == 2, "Legacy 404 fallback failed");
            sample.Handler.Routes[contents] = new Reply([], HttpStatusCode.Unauthorized);
            await RejectAsync<HttpRequestException>(() => sample.Client.LoadAsync(info, CancellationToken.None));
        });
        await RunAsync("historical source requires the known assetless tag", async () =>
        {
            using var sample = new ModernSample(fixture);
            var url = "https://codeload.github.com/SCKorea/SC_ko/zip/refs/tags/0.67.0";
            sample.Handler.Routes[url] = new Reply(Zip(Utf8.GetBytes("legacy=source\n")));
            var info = new LocalizationReleaseInfo("0.67.0", url, []);
            var release = await sample.Client.LoadAsync(info, CancellationToken.None);
            var prepared = await sample.Client.PrepareAsync(release, [], sample.Directory, CancellationToken.None);
            Check(prepared.LegacyVariant == "legacy" && release.Format == LocalizationReleaseFormat.HistoricalSource, "Historical source fallback failed");
            await RejectAsync<InvalidDataException>(() => sample.Client.LoadAsync(info with { Tag = "0.67.2" }, CancellationToken.None));
        });
    }

    private static (LocalizationReleaseInfo Info, string Contents) AddLegacy(FakeHandler handler)
    {
        var catalog = "{\"default_selection\":\"bp\",\"fallback\":\"standard\",\"variants\":[{\"id\":\"standard\",\"name\":\"기본\",\"description\":\"\"},{\"id\":\"bp\",\"name\":\"BP\",\"description\":\"\"}]}";
        var contents = "https://api.github.com/repos/SCKorea/SC_ko/contents/release/variants.json?ref=0.67.1";
        handler.Routes[contents] = new Reply(Utf8.GetBytes(catalog));
        handler.Routes["https://api.github.com/assets/standard"] = new Reply(Zip(Utf8.GetBytes("legacy=standard\n")));
        handler.Routes["https://api.github.com/assets/bp"] = new Reply(Zip(Utf8.GetBytes("legacy=BP\n")));
        return (new LocalizationReleaseInfo("0.67.1", "https://codeload.github.com/legacy", [
            new ReleaseAsset("sc-ko-0.67.1-standard.zip", "https://api.github.com/assets/standard", null),
            new ReleaseAsset("sc-ko-0.67.1-bp.zip", "https://api.github.com/assets/bp", null)]), contents);
    }

    private static async Task RunCompressedDownloads(JsonObject fixture)
    {
        await RunAsync("compressed base and feature preserve composed bytes", async () =>
        {
            using var sample = new ModernSample(fixture);
            sample.SetZip("base", TransportZip(("global.ini", sample.Base)));
            sample.SetZip("ship_en", TransportZip(("readme.txt", Utf8.GetBytes("text")),
                ("ignored.ini/", []), ("nested/FEATURE.INI", sample.Parts["ship_en"])));
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            var prepared = await sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, CancellationToken.None);
            Check(ReadZip(prepared.ZipPath).SequenceEqual(LocalizationComposer.Compose(sample.Base, release.Catalog!, sample.Parts, ["ship_en"])), "ZIP transport changed the composition");
            Check(prepared.Features!.SequenceEqual(["ship_en"]) && prepared.Warnings.Count == 0, "ZIP transport changed the actual selection");
        });
        foreach (bool emptyArchive in new[] { true, false })
        {
            await RunAsync("base ZIP without INI has the same error as a missing base: " + emptyArchive, async () =>
            {
                using var sample = new ModernSample(fixture);
                sample.SetZip("base", emptyArchive ? TransportZip() : TransportZip(("readme.txt", []), ("ignored.ini/", [])));
                var missing = sample.Info with { Assets = sample.Info.Assets.Where(asset => asset.Name != "sc-ko-0.67.2-base.zip").ToArray() };
                var expected = await RejectAsync<InvalidDataException>(() => sample.Client.LoadAsync(missing, CancellationToken.None));
                var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
                var actual = await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, [], sample.Directory, CancellationToken.None));
                Check(actual.Message == expected.Message && !System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "No-INI base did not receive the missing-base failure");
            });
            await RunAsync("selected ZIP without INI has the same warning and omission as a missing feature: " + emptyArchive, async () =>
            {
                using var sample = new ModernSample(fixture);
                sample.SetZip("marker", emptyArchive ? TransportZip() : TransportZip(("readme.txt", []), ("ignored.ini/", [])));
                var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
                var missing = release with { Info = sample.Info with { Assets = sample.Info.Assets.Where(asset => asset.Name != "sc-ko-0.67.2-marker.zip").ToArray() } };
                var expected = await sample.Client.PrepareAsync(missing, ["marker", "ship_en"], sample.Directory, CancellationToken.None);
                var actual = await sample.Client.PrepareAsync(release, ["marker", "ship_en"], sample.Directory, CancellationToken.None);
                Check(actual.Features!.SequenceEqual(["ship_en"]) && actual.Warnings.SequenceEqual(expected.Warnings), "No-INI feature was not omitted like a missing feature");
                Check(ReadZip(actual.ZipPath).SequenceEqual(ReadZip(expected.ZipPath)), "No-INI feature changed the remaining composition");
            });
        }
        foreach (string id in new[] { "base", "ship_en" })
        {
            foreach (bool malformed in new[] { true, false })
                await RunAsync("invalid ZIP fails before installation: " + id + "/" + malformed, async () =>
                {
                    using var sample = new ModernSample(fixture);
                    var data = id == "base" ? sample.Base : sample.Parts[id];
                    sample.SetZip(id, malformed ? Utf8.GetBytes("not a zip") : TransportZip(("one.ini", data), ("nested/two.INI", data)));
                    var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
                    await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, CancellationToken.None));
                    Check(!System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "Invalid ZIP left an installation package");
                });
        }
        await RunAsync("ZIP transport hash is verified before no-INI handling", async () =>
        {
            using var sample = new ModernSample(fixture);
            sample.SetZip("ship_en", TransportZip(("feature.ini", sample.Parts["ship_en"])));
            sample.Handler.Routes[sample.Url("ship_en")] = new Reply(TransportZip());
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            var error = await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, CancellationToken.None));
            Check(error.Message.Contains("SHA-256 mismatch") && !System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "Corrupt transport was treated as a missing feature");
        });
        await RunAsync("ZIP content hash is verified after extraction", async () =>
        {
            using var sample = new ModernSample(fixture);
            sample.SetZip("base", TransportZip(("global.ini", Utf8.GetBytes("\uFEFFchanged=wrong\n"))));
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            var error = await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, [], sample.Directory, CancellationToken.None));
            Check(error.Message.Contains("SHA-256 mismatch") && !System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "Extracted content was not verified");
        });
        Run("ZIP manifests require a transport hash and direct INI hashes must agree", () =>
        {
            using var sample = new ModernSample(fixture);
            sample.SetZip("base", TransportZip(("global.ini", sample.Base)));
            foreach (var change in new Action<JsonNode>[] {
                node => node["base"]!.AsObject().Remove("asset_sha256"),
                node => node["base"]!["asset_sha256"] = "invalid",
                node => node["base"]!["asset"] = "sc-ko-0.67.2-base.tar",
                node => { node["base"]!["asset"] = "sc-ko-0.67.2-base.ini"; node["base"]!["asset_sha256"] = new string('0', 64); }
            })
            {
                var node = JsonNode.Parse(sample.Manifest)!;
                change(node);
                Reject<InvalidDataException>(() => ReleaseManifest.Parse(Utf8.GetBytes(node.ToJsonString()), sample.Info.Tag));
            }
        });
        await RunAsync("base ZIP alone identifies broken modern metadata", async () =>
        {
            using var sample = new ModernSample(fixture);
            var info = sample.Info with { Assets = new[] {
                new ReleaseAsset("sc-ko-0.67.2-base.zip", sample.Url("base"), null),
                new ReleaseAsset("sc-ko-0.67.2-standard.zip", "https://api.github.com/legacy", null)
            } };
            var error = await RejectAsync<InvalidDataException>(() => sample.Client.LoadAsync(info, CancellationToken.None));
            Check(error.Message == "Build manifest asset is missing" && sample.Handler.Requests.Count == 0, "Broken ZIP release was treated as legacy");
        });
        await RunAsync("ZIP rejects INI lengths exceeding the extraction limit", async () =>
        {
            using var sample = new ModernSample(fixture);
            var payload = TransportZip(("global.ini", sample.Base));
            // Change only the central-directory size, avoiding a huge fixture allocation.
            int central = -1;
            for (int index = 0; index <= payload.Length - 4; index++)
                if (payload[index] == 0x50 && payload[index + 1] == 0x4B && payload[index + 2] == 0x01 && payload[index + 3] == 0x02) { central = index; break; }
            Check(central >= 0, "ZIP central directory was not found");
            BitConverter.GetBytes(150 * 1024 * 1024 + 1).CopyTo(payload, central + 24);
            sample.SetZip("base", payload);
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            var error = await RejectAsync<InvalidDataException>(() => sample.Client.PrepareAsync(release, [], sample.Directory, CancellationToken.None));
            Check(error.Message.Contains("supported size") && !System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "INI size limit was not enforced");
        });
        await RunAsync("ZIP feature downloads preserve cancellation", async () =>
        {
            using var sample = new ModernSample(fixture);
            sample.SetZip("ship_en", TransportZip(("feature.ini", sample.Parts["ship_en"])));
            sample.Handler.PausedUrl = sample.Url("ship_en");
            var release = await sample.Client.LoadAsync(sample.Info, CancellationToken.None);
            using var cancel = new CancellationTokenSource();
            var preparing = sample.Client.PrepareAsync(release, ["ship_en"], sample.Directory, cancel.Token);
            await sample.Handler.Paused.Task;
            cancel.Cancel();
            await RejectAsync<OperationCanceledException>(() => preparing);
            Check(!System.IO.Directory.EnumerateFiles(sample.Directory).Any(), "Canceled ZIP download left an installation package");
        });
    }

    private static byte[] TransportZip(params (string Name, byte[] Data)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, data) in entries)
            {
                using var entry = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                entry.Write(data);
            }
        return memory.ToArray();
    }

    private static byte[] Zip(byte[] data) => TransportZip((LocalizationReleaseClient.InstallEntry, data));

    private static byte[] ReadZip(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        Check(archive.Entries.Count == 1 && archive.Entries[0].FullName == LocalizationReleaseClient.InstallEntry, "Installation ZIP has the wrong layout");
        using var entry = archive.Entries[0].Open();
        using var memory = new MemoryStream();
        entry.CopyTo(memory);
        return memory.ToArray();
    }

    private static async Task RunGeneratedCandidate(string root)
    {
        await RunAsync("Python-generated candidate end-to-end composition", async () =>
        {
            var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "release/build-manifest.json")))!;
            string tag = manifest["tag"]!.GetValue<string>();
            using var handler = new FakeHandler();
            using var http = new HttpClient(handler);
            var assets = new List<ReleaseAsset>();
            foreach (var path in System.IO.Directory.EnumerateFiles(Path.Combine(root, "release/assets")))
            {
                string name = Path.GetFileName(path);
                var url = "https://api.github.com/assets/" + Uri.EscapeDataString(name);
                handler.Routes[url] = new Reply(File.ReadAllBytes(path));
                assets.Add(new ReleaseAsset(name, url, null));
            }
            var client = new LocalizationReleaseClient(http, "SCKorea/SC_ko", "test-token");
            var release = await client.LoadAsync(new LocalizationReleaseInfo(tag, "https://codeload.github.com/unused", assets), CancellationToken.None);
            var local = Path.Combine(root, "release/local");
            System.IO.Directory.CreateDirectory(local);
            foreach (var (name, selected) in new[] { ("base", Array.Empty<string>()),
                ("default", release.Catalog!.DefaultSelection.ToArray()), ("all", release.Catalog.Options.Select(option => option.Id).ToArray()) })
            {
                var prepared = await client.PrepareAsync(release, selected, local, CancellationToken.None);
                try
                {
                    var data = ReadZip(prepared.ZipPath);
                    await File.WriteAllBytesAsync(Path.Combine(local, $"csharp-{name}.ini"), data);
                    var expected = Path.Combine(local, $"python-{name}.ini");
                    if (File.Exists(expected))
                    {
                        var expectedData = await File.ReadAllBytesAsync(expected);
                        Check(data.SequenceEqual(expectedData), $"Python/C# {name} bytes differ");
                    }
                }
                finally { File.Delete(prepared.ZipPath); }
            }
        });
    }

    // Opt-in environment check, never run by the ordinary offline fixture suite.
    private static async Task RunLiveRelease(string tag)
    {
        await RunAsync("live private release download: " + tag, async () =>
        {
            var token = Environment.GetEnvironmentVariable("SC_FEATURE_TEST_TOKEN");
            if (string.IsNullOrEmpty(token)) throw new InvalidOperationException("Set SC_FEATURE_TEST_TOKEN for --live-tag");
            if (!VariantCatalog.TagPattern.IsMatch(tag)) throw new ArgumentException("Invalid live tag");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("sc-feature-live-verification");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/SCKorea/SC_ko/releases/tags/{Uri.EscapeDataString(tag)}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request);
            JsonElement root;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // GitHub drafts have no tag ref yet and are absent from the tag endpoint.
                using var listRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/SCKorea/SC_ko/releases?per_page=100");
                listRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                listRequest.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var listed = await http.SendAsync(listRequest);
                listed.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await listed.Content.ReadAsByteArrayAsync());
                var matches = document.RootElement.EnumerateArray().Where(item => item.GetProperty("tag_name").GetString() == tag).ToArray();
                if (matches.Length != 1) throw new InvalidDataException("Expected one authorized draft release with this tag");
                root = matches[0].Clone();
            }
            else
            {
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
                root = document.RootElement.Clone();
            }
            var assets = root.GetProperty("assets").EnumerateArray().Select(asset => new ReleaseAsset(
                asset.GetProperty("name").GetString()!, asset.GetProperty("url").GetString(),
                asset.GetProperty("browser_download_url").GetString())).ToArray();
            var info = new LocalizationReleaseInfo(root.GetProperty("tag_name").GetString()!, root.GetProperty("zipball_url").GetString() ?? "", assets);
            var client = new LocalizationReleaseClient(http, "SCKorea/SC_ko", token);
            var release = await client.LoadAsync(info, CancellationToken.None);
            var directory = Path.Combine(Path.GetTempPath(), "sc-feature-live-" + Guid.NewGuid().ToString("N"));
            try
            {
                var prepared = await client.PrepareAsync(release, release.Catalog?.DefaultSelection ?? [], directory, CancellationToken.None);
                var data = ReadZip(prepared.ZipPath);
                Check(data.Length > 0, "Live release is empty");
                var expectedHash = Environment.GetEnvironmentVariable("SC_FEATURE_EXPECTED_SHA256");
                if (!string.IsNullOrEmpty(expectedHash)) Check(Hash(data) == expectedHash, "Live composition differs from the verified Python candidate");
                Console.WriteLine($"Live tag={tag}, format={release.Format}, entries={LocalizationComposer.ReadIni(data, "live").Count}, sha256={Hash(data)}");
            }
            finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, recursive: true); }
        });
    }

    private sealed record Reply(byte[] Bytes, HttpStatusCode Status = HttpStatusCode.OK);
    private sealed record Request(string Url, string? Auth, string Accept);
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Dictionary<string, Reply> Routes { get; } = new(StringComparer.Ordinal);
        public List<Request> Requests { get; } = [];
        public string? PausedUrl { get; set; }
        public TaskCompletionSource Paused { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(new Request(url, request.Headers.Authorization?.ToString(), string.Join(",", request.Headers.Accept)));
            if (url == PausedUrl)
            {
                Paused.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (!Routes.TryGetValue(url, out var reply)) throw new Exception("Unexpected request: " + url);
            return new HttpResponseMessage(reply.Status) { Content = new ByteArrayContent(reply.Bytes) };
        }
    }

    private sealed class ModernSample : IDisposable
    {
        public FakeHandler Handler { get; } = new();
        private readonly HttpClient _http;
        public LocalizationReleaseClient Client { get; }
        public LocalizationReleaseInfo Info { get; private set; }
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "sc-feature-test-" + Guid.NewGuid().ToString("N"));
        public byte[] Base { get; }
        public Dictionary<string, byte[]> Parts { get; }
        public byte[] Manifest { get; private set; }
        public string Url(string label) => "https://api.github.com/assets/" + label;

        public ModernSample(JsonObject fixture)
        {
            System.IO.Directory.CreateDirectory(Directory);
            _http = new HttpClient(Handler);
            Client = new LocalizationReleaseClient(_http, "SCKorea/SC_ko", "test-token");
            var catalogData = Utf8.GetBytes(fixture["catalog"]!.ToJsonString());
            Base = LocalizationComposer.WriteIni(LocalizationComposer.ReadIni(Bytes(fixture["base"]!), "base"));
            Parts = Runner.Parts(fixture).ToDictionary(pair => pair.Key,
                pair => LocalizationComposer.WriteIni(LocalizationComposer.ReadIni(pair.Value, pair.Key, allowEmpty: true)));
            object FileDescriptor(string path, string asset, byte[] data) => new { path, asset, sha256 = Hash(data), entries = LocalizationComposer.ReadIni(data, path, allowEmpty: true).Count };
            Manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version = 2, tag = "0.67.2", target_game_version = "4.10.2",
                catalog = new { path = "variants.json", asset = "sc-ko-0.67.2-variants.json", sha256 = Hash(catalogData) },
                @base = FileDescriptor("base/global.ini", "sc-ko-0.67.2-base.ini", Base),
                features = Parts.ToDictionary(pair => pair.Key, pair => FileDescriptor("features/" + pair.Key + ".ini", "sc-ko-0.67.2-" + pair.Key + ".ini", pair.Value))
            });
            Handler.Routes[Url("manifest")] = new Reply(Manifest);
            Handler.Routes[Url("catalog")] = new Reply(catalogData);
            Handler.Routes[Url("base")] = new Reply(Base);
            var assets = new List<ReleaseAsset> {
                new("sc-ko-0.67.2-build-manifest.json", Url("manifest"), null),
                new("sc-ko-0.67.2-variants.json", Url("catalog"), null),
                new("sc-ko-0.67.2-base.ini", Url("base"), null)
            };
            foreach (var (id, data) in Parts)
            {
                Handler.Routes[Url(id)] = new Reply(data);
                assets.Add(new ReleaseAsset("sc-ko-0.67.2-" + id + ".ini", Url(id), null));
            }
            Info = new LocalizationReleaseInfo("0.67.2", "https://codeload.github.com/unused", assets);
        }

        public void SetZip(string id, byte[] payload)
        {
            var manifest = JsonNode.Parse(Manifest)!;
            var descriptor = id == "base" ? manifest["base"]! : manifest["features"]![id]!;
            var oldName = descriptor["asset"]!.GetValue<string>();
            var name = "sc-ko-0.67.2-" + id + ".zip";
            descriptor["asset"] = name;
            descriptor["asset_sha256"] = Hash(payload);
            Manifest = Utf8.GetBytes(manifest.ToJsonString());
            Handler.Routes[Url("manifest")] = new Reply(Manifest);
            Handler.Routes[Url(id)] = new Reply(payload);
            Info = Info with { Assets = Info.Assets.Select(asset => asset.Name == oldName ? asset with { Name = name } : asset).ToArray() };
        }

        public void Dispose()
        {
            _http.Dispose();
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
