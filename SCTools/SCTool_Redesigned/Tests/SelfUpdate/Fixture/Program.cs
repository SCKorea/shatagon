using System.Reflection;
using Newtonsoft.Json;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Update;

internal static class Program
{
    private static int Main(string[] args)
    {
        var directory = Path.GetDirectoryName(Environment.ProcessPath!)!;
        if (UpdateProcessHelper.IsUpdateHelperInvocation(args))
        {
            var installDirectory = Directory.GetParent(Directory.GetParent(directory)!.FullName)!.FullName;
            if (ReadScenario(installDirectory).Fault == "helper-exits")
                return 31;
            return UpdateProcessHelper.RunUpdateHelper(args);
        }

        var scenario = ReadScenario(directory);
        if (scenario.Fault == "startup-crash" && args.Length >= 3 && args[0] == "--update-finished" && args[2] == "0")
            return 32;

        UpdateProcessHelper.HandleApplicationStartup(args);
        var version = Assembly.GetExecutingAssembly().GetName().Version!.ToString(4);
        try
        {
            using var repository = new FixtureRepository(directory, scenario.Incoming, version);
            using var updater = new CustomApplicationUpdater(repository, directory, new CustomPackageVerifier());
            var available = updater.CheckForUpdateVersionAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (available == null)
            {
                WriteResult(directory, version, "Continue", null);
                return 0;
            }

            var downloaded = updater.DownloadVersionAsync(available, CancellationToken.None, new ProgressSink()).GetAwaiter().GetResult();
            if (!updater.ScheduleInstallUpdate(available, downloaded))
                throw new InvalidOperationException("Scheduling failed.");
            var install = updater.InstallScheduledUpdate();
            if (install != InstallUpdateStatus.Success)
            {
                WriteResult(directory, version, install.ToString(), null);
                return 0;
            }

            if (scenario.Fault == "missing-update")
                File.Delete(downloaded);
            return 0;
        }
        catch (Exception exception)
        {
            WriteResult(directory, version, "Continue", exception.GetType().Name);
            return 0;
        }
    }

    private static Scenario ReadScenario(string directory) =>
        JsonConvert.DeserializeObject<Scenario>(File.ReadAllText(Path.Combine(directory, "scenario.json")))!;

    private static void WriteResult(string directory, string version, string status, string? error)
    {
        var temporary = Path.Combine(directory, "result.tmp");
        File.WriteAllText(temporary, JsonConvert.SerializeObject(new
        {
            Version = version,
            UpdateFailed = UpdateProcessHelper.UpdateFailed,
            Status = status,
            Error = error
        }));
        File.Move(temporary, Path.Combine(directory, "result.json"), true);
    }

    private sealed class Scenario
    {
        public string Incoming { get; set; } = "";
        public string Fault { get; set; } = "";
    }

    private sealed class ProgressSink : IDownloadProgress
    {
        public void ReportContentSize(long value) { }
        public void ReportDownloadedSize(long value) { }
    }

    private sealed class FixtureRepository(string directory, string incoming, string currentVersion) : IUpdateRepository
    {
        private readonly GitHubUpdateInfo _update = new("Update", "1.4.1.3", "https://example.invalid/Shatagon.exe");
        public string Name => "Shatagon";
        public string Repository => "fixture";
        public string RepositoryUrl => "https://example.invalid";
        public UpdateRepositoryType Type => UpdateRepositoryType.GitHub;
        public string? CurrentVersion { get; private set; } = currentVersion;
        public IEnumerable<UpdateInfo>? UpdateReleases => new[] { _update };
        public UpdateInfo? LatestUpdateInfo => _update;
        public bool AllowPreReleases { get; set; }
        public bool IsMonitorStarted => false;
        public int MonitorRefreshTime => 0;
        public void SetCurrentVersion(string version) => CurrentVersion = version;
        public Task<List<UpdateInfo>> GetAllAsync(CancellationToken token) => Task.FromResult(new List<UpdateInfo> { _update });
        public Task<IEnumerable<UpdateInfo>> RefreshUpdatesAsync(CancellationToken token) => Task.FromResult(UpdateReleases!);
        public Task<UpdateInfo?> GetLatestAsync(CancellationToken token) => Task.FromResult<UpdateInfo?>(_update);
        public Task<string> DownloadAsync(UpdateInfo update, string path, CancellationToken token, IDownloadProgress progress)
        {
            var counterPath = Path.Combine(directory, "downloads.txt");
            var count = File.Exists(counterPath) ? int.Parse(File.ReadAllText(counterPath)) : 0;
            File.WriteAllText(counterPath, (count + 1).ToString());
            var destination = Path.Combine(path, "Shatagon.exe");
            File.Copy(incoming, destination, true);
            return Task.FromResult(destination);
        }
        public Task<bool> CheckAsync(CancellationToken token) => Task.FromResult(true);
        public UpdateInfo? UpdateCurrentVersion(string? fallback) => _update;
        public void Dispose() { }
        public void MonitorStart(int refreshTime) { }
        public void MonitorStop() { }
        public event EventHandler? MonitorStarted { add { } remove { } }
        public event EventHandler? MonitorStopped { add { } remove { } }
        public event EventHandler<string>? MonitorNewVersion { add { } remove { } }
    }
}
