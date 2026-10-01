using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Update;

internal static class Runner
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            var root = Path.GetFullPath(args[0]);
            var cases = Path.Combine(root, "cases-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cases);
            var incoming = Path.Combine(root, "fixtures", "new-single", "Shatagon.exe");
            await CheckDownloads(cases, incoming);
            if (OperatingSystem.IsWindows())
            {
                CheckPackages(cases, incoming);
                var application = Path.Combine(root, "application");
                if (Directory.Exists(application))
                {
                    Require(new CustomPackageVerifier().VerifyPackage(application), "production single-file publish was rejected");
                    Console.WriteLine("PASS: production single-file executable metadata");
                }
                if (args.Skip(1).Contains("--unit-only"))
                {
                    Console.WriteLine("All self-update unit checks passed.");
                    return 0;
                }
                await CheckProcess(root, cases, "single-file success", "old-single", "", "new-single", "1.4.1.3", false);
                await CheckProcess(root, cases, "일반 빌드 with spaces", "old-multi", "", "new-single", "1.4.1.3", false);
                await CheckProcess(root, cases, "missing update", "old-single", "missing-update", "new-single", "1.4.1.2", true);
                await CheckProcess(root, cases, "startup crash rollback", "old-single", "startup-crash", "new-single", "1.4.1.2", true);
                await CheckProcess(root, cases, "assembly version rollback", "old-single", "", "wrong-version", "1.4.1.2", true);
                await CheckProcess(root, cases, "helper exits before ready", "old-multi", "helper-exits", "new-single", "1.4.1.2", false, "LaunchScriptError");
                await CheckProcess(root, cases, "locked target", "old-single", "", "new-single", "1.4.1.2", true, locked: true);
            }
            else
            {
                Console.WriteLine("Windows PE metadata and process scenarios require Windows.");
            }
            Console.WriteLine("All self-update regression checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task CheckDownloads(string cases, string executable)
    {
        var payload = File.ReadAllBytes(executable);
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(payload));
        var handler = new AssetHandler(payload, payload.Length, digest);
        using var client = new HttpClient(handler);
        using var repository = new ApplicationUpdateRepository(client, "Shatagon", "fixture/test");
        repository.AllowPreReleases = false;
        repository.SetCurrentVersion("1.4.1.2");
        using var updater = new CustomApplicationUpdater(repository, cases, new CustomPackageVerifier());
        var update = await updater.CheckForUpdateVersionAsync(CancellationToken.None);
        Require(update?.GetVersion() == "v1.4.1.3", "numeric selection or prerelease filtering");
        Require(update!.DownloadUrl.EndsWith("/Shatagon.exe", StringComparison.Ordinal), "first, unrelated asset was selected");
        var directory = Path.Combine(cases, "download");
        var downloaded = await repository.DownloadAsync(update, directory, CancellationToken.None, null);
        Require(File.ReadAllBytes(downloaded).SequenceEqual(payload), "downloaded bytes changed");
        Console.WriteLine("PASS: numeric release selection and named asset with quoted/irrelevant response filename");

        if (OperatingSystem.IsWindows())
        {
            var verified = await updater.DownloadVersionAsync(update, CancellationToken.None, new ProgressSink());
            Require(updater.ScheduleInstallUpdate(update, verified), "verified release could not be scheduled");
            Require(updater.GetScheduledUpdateInfo()?.GetVersion() == update.GetVersion(), "scheduled release metadata could not be read back");
            Console.WriteLine("PASS: verified release scheduling and JSON metadata round trip");
        }

        UpdateProcessHelper.HandleApplicationStartup(new[] { "--update-finished", "2147480000", "1" });
        Require(await updater.CheckForUpdateVersionAsync(CancellationToken.None) == null, "failed restart offered another automatic update");
        UpdateProcessHelper.HandleApplicationStartup([]);
        Require(await updater.CheckForUpdateVersionAsync(CancellationToken.None) != null, "a later normal start did not retry");
        Console.WriteLine("PASS: failed restart skips automatic update; normal start retries");

        foreach (var fault in new[] { "size", "digest", "interrupted" })
        {
            using var faultClient = new HttpClient(new AssetHandler(payload, payload.Length + (fault == "size" ? 1 : 0),
                fault == "digest" ? "sha256:" + new string('0', 64) : digest, fault == "interrupted"));
            using var faultRepository = new ApplicationUpdateRepository(faultClient, "Shatagon", "fixture/test");
            var candidate = (await faultRepository.GetAllAsync(CancellationToken.None))[0];
            var before = File.ReadAllBytes(downloaded);
            var rejected = false;
            try { await faultRepository.DownloadAsync(candidate, directory, CancellationToken.None, null); }
            catch (Exception exception) when (exception is IOException or InvalidDataException) { rejected = true; }
            Require(rejected, $"{fault} download was accepted");
            Require(File.ReadAllBytes(downloaded).SequenceEqual(before), $"{fault} replaced the previous download");
            Require(Directory.GetFiles(directory, "*.tmp").Length == 0, $"{fault} left a temporary download");
            Console.WriteLine($"PASS: {fault} download rejected without replacing the previous file");
        }
    }

    private static void CheckPackages(string cases, string executable)
    {
        var directory = Path.Combine(cases, "package");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "Shatagon.exe");
        var bytes = File.ReadAllBytes(executable);
        File.WriteAllBytes(target, bytes);
        var verifier = new CustomPackageVerifier();
        Require(verifier.VerifyPackage(directory, "v1.4.1.3"), "valid published package was rejected");
        Require(!verifier.VerifyPackage(directory, "1.4.1.4"), "file version mismatch was accepted");
        File.WriteAllText(target, "not an executable");
        Require(!verifier.VerifyPackage(directory, "1.4.1.3"), "text renamed to exe was accepted");
        var product = System.Text.Encoding.Unicode.GetBytes("Shatagon Patcher");
        var productOffset = bytes.AsSpan().IndexOf(product);
        Require(productOffset >= 0, "fixture product resource was not found");
        System.Text.Encoding.Unicode.GetBytes("OtherApp Patcher").CopyTo(bytes, productOffset);
        File.WriteAllBytes(target, bytes);
        Require(!verifier.VerifyPackage(directory, "1.4.1.3"), "another application's exe was accepted");
        bytes = File.ReadAllBytes(executable);
        var peOffset = BitConverter.ToInt32(bytes, 0x3c);
        bytes[peOffset + 4] = 0x4c;
        bytes[peOffset + 5] = 0x01;
        File.WriteAllBytes(target, bytes);
        Require(!verifier.VerifyPackage(directory, "1.4.1.3"), "wrong architecture was accepted");
        Console.WriteLine("PASS: Windows PE identity, version and architecture validation");
    }

    private static async Task CheckProcess(string root, string cases, string name, string oldFixture, string fault,
        string incomingFixture, string expectedVersion, bool expectedFailure, string expectedStatus = "Continue", bool locked = false)
    {
        var directory = Path.Combine(cases, name);
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "fixtures", oldFixture)))
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        var executable = Path.Combine(directory, "Shatagon.exe");
        var before = SHA256.HashData(File.ReadAllBytes(executable));
        File.WriteAllText(Path.Combine(directory, "scenario.json"), JsonConvert.SerializeObject(new
        {
            Incoming = Path.Combine(root, "fixtures", incomingFixture, "Shatagon.exe"),
            Fault = fault
        }));
        using var targetLock = locked ? new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        using var parent = Process.Start(new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        await parent.WaitForExitAsync(timeout.Token);
        var resultPath = Path.Combine(directory, "result.json");
        while (!File.Exists(resultPath))
            await Task.Delay(100, timeout.Token);
        var result = JsonConvert.DeserializeObject<Result>(File.ReadAllText(resultPath))!;
        Require(result.Version == expectedVersion && result.UpdateFailed == expectedFailure &&
            result.Status == expectedStatus && result.Error == null, $"{name}: unexpected result {File.ReadAllText(resultPath)}");
        Require(File.ReadAllText(Path.Combine(directory, "downloads.txt")) == "1", $"{name}: automatic update retried");
        if (expectedVersion == "1.4.1.2")
            Require(SHA256.HashData(File.ReadAllBytes(executable)).SequenceEqual(before), $"{name}: original executable was not preserved");
        else
            Require(!Directory.Exists(Path.Combine(directory, "updates")), $"{name}: successful update was not cleaned up");
        Console.WriteLine($"PASS: {name}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Result
    {
        public string Version { get; set; } = "";
        public bool UpdateFailed { get; set; }
        public string Status { get; set; } = "";
        public string? Error { get; set; }
    }

    private sealed class ProgressSink : IDownloadProgress
    {
        public void ReportContentSize(long value) { }
        public void ReportDownloadedSize(long value) { }
    }

    private sealed class AssetHandler(byte[] payload, long expectedSize, string digest, bool interrupted = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            HttpContent content;
            if (request.RequestUri!.Host == "api.github.com")
            {
                object Release(string tag, bool prerelease = false) => new
                {
                    name = tag, tag_name = tag, prerelease,
                    assets = new[]
                    {
                        new { name = "Installer.exe", browser_download_url = "https://example.invalid/Installer.exe", size = expectedSize, digest },
                        new { name = "Shatagon.exe", browser_download_url = "https://example.invalid/Shatagon.exe", size = expectedSize, digest }
                    }
                };
                content = new StringContent(JsonConvert.SerializeObject(new[] { Release("v1.4.1.3"), Release("1.4.1.2"), Release("1.4.1.1"), Release("9.0.0.0", true) }));
            }
            else
            {
                if (!request.RequestUri.AbsolutePath.EndsWith("/Shatagon.exe", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unrelated release asset requested.");
                content = interrupted ? new StreamContent(new InterruptedStream(payload)) : new ByteArrayContent(payload);
                content.Headers.ContentLength = payload.Length;
                content.Headers.ContentDisposition = new("attachment") { FileName = "\"Other.exe\"" };
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class InterruptedStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _source = new(bytes);
        private bool _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _source.Length;
        public override long Position { get => _source.Position; set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (_read) throw new IOException("Interrupted download.");
            _read = true;
            return _source.ReadAsync(buffer[..Math.Min(128, buffer.Length)], token);
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (_read) throw new IOException("Interrupted download.");
            _read = true;
            return _source.ReadAsync(buffer, offset, Math.Min(128, count), token);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _source.Dispose();
            base.Dispose(disposing);
        }
    }
}
