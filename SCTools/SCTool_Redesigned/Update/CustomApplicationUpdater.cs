using System.Diagnostics;
using System.IO;
using NLog;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Update
{
    public class CustomApplicationUpdater : IDisposable
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly IUpdateRepository _updateRepository;
        private readonly IPackageVerifier _packageVerifier;
        private readonly string _executableDir;
        private readonly string _updatesStoragePath;
        private readonly string _schedInstallFilePath;
        private readonly string _schedInstallJsonPath;
        private readonly string _updateHelperPath;
        private readonly string _currentVersion;

        public interface IPackageVerifier
        {
            bool VerifyPackage(string path, string? expectedVersion = null);
        }

        public event EventHandler MonitorStarted
        {
            add { _updateRepository.MonitorStarted += value; }
            remove { _updateRepository.MonitorStarted -= value; }
        }

        public event EventHandler MonitorStopped
        {
            add { _updateRepository.MonitorStopped += value; }
            remove { _updateRepository.MonitorStopped -= value; }
        }

        public event EventHandler<string> MonitorNewVersion
        {
            add { _updateRepository.MonitorNewVersion += value; }
            remove { _updateRepository.MonitorNewVersion -= value; }
        }

        public bool AllowPreReleases
        {
            get => _updateRepository.AllowPreReleases;
            set => _updateRepository.AllowPreReleases = value;
        }

        public CustomApplicationUpdater(IUpdateRepository updateRepository, string executableDir,
            IPackageVerifier packageVerifier)
        {
            if (updateRepository.CurrentVersion == null)
                throw new InvalidOperationException("update repository current version is not set");
            _updateRepository = updateRepository;
            _executableDir = executableDir;
            _packageVerifier = packageVerifier;
            _updatesStoragePath = Path.Combine(_executableDir, "updates");
            _schedInstallFilePath = Path.Combine(_updatesStoragePath, "Shatagon.exe");
            _schedInstallJsonPath = Path.Combine(_updatesStoragePath, "latest.json");
            _updateHelperPath = Path.Combine(_updatesStoragePath, $"helper-{Environment.ProcessId}-{Guid.NewGuid():N}", "Shatagon.exe");
            _currentVersion = _updateRepository.CurrentVersion;
        }

        public void Dispose() => _updateRepository.Dispose();

        public void MonitorStart(int refreshTime) => _updateRepository.MonitorStart(refreshTime);

        public void MonitorStop() => _updateRepository.MonitorStop();

        public async Task<UpdateInfo?> CheckForUpdateVersionAsync(CancellationToken cancellationToken)
        {
            if (UpdateProcessHelper.UpdateFailed)
                return null;

            if (!ReleaseVersion.TryParse(_currentVersion, out var currentVersion))
            {
                _logger.Warn($"Cannot compare current application version: {_currentVersion}");
                return null;
            }

            var releases = await _updateRepository.GetAllAsync(cancellationToken);
            UpdateInfo? newestUpdateInfo = null;
            var newestVersion = currentVersion;

            foreach (var release in releases)
            {
                if (!_updateRepository.AllowPreReleases && release.PreRelease)
                    continue;

                if (!ReleaseVersion.TryParse(release.GetVersion(), out var releaseVersion))
                {
                    _logger.Warn($"Ignoring release with an invalid version: {release.GetVersion()}");
                    continue;
                }

                if (releaseVersion > newestVersion)
                {
                    newestVersion = releaseVersion;
                    newestUpdateInfo = release;
                }
            }

            return newestUpdateInfo;
        }

        public async Task<string> DownloadVersionAsync(UpdateInfo version, CancellationToken cancellationToken, IDownloadProgress downloadProgress)
        {
            if (!ReleaseVersion.IsNewer(version.GetVersion(), _currentVersion))
                throw new InvalidOperationException($"Refusing to download non-newer version {version.GetVersion()} over {_currentVersion}.");

            if (!Directory.Exists(_updatesStoragePath))
            {
                Directory.CreateDirectory(_updatesStoragePath);
            }

            var downloadedFilePath = await _updateRepository.DownloadAsync(version, _updatesStoragePath, cancellationToken, downloadProgress);
            if (!File.Exists(downloadedFilePath))
                throw new FileNotFoundException("The downloaded update executable was not found.", downloadedFilePath);

            var updatesDirectory = Path.GetFullPath(_updatesStoragePath);
            var downloadedFullPath = Path.GetFullPath(downloadedFilePath);
            var downloadedDirectory = Path.GetDirectoryName(downloadedFullPath);
            var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (downloadedDirectory == null || !string.Equals(downloadedDirectory, updatesDirectory, pathComparison))
                throw new InvalidDataException("The downloaded update is outside the updates directory.");

            if (!string.Equals(Path.GetExtension(downloadedFullPath), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded update is not an executable file.");

            if (!PathsEqual(downloadedFullPath, _schedInstallFilePath))
                throw new InvalidDataException("The downloaded asset is not Shatagon.exe.");

            if (!_packageVerifier.VerifyPackage(_updatesStoragePath, version.GetVersion()))
                throw new InvalidDataException("The downloaded update does not contain a valid Shatagon.exe executable.");

            return _schedInstallFilePath;
        }

        public InstallUpdateStatus InstallScheduledUpdate() =>
            InstallScheduledUpdateAsync(CancellationToken.None).GetAwaiter().GetResult();

        public async Task<InstallUpdateStatus> InstallScheduledUpdateAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Install scheduled update");
            var scheduledUpdate = GetScheduledUpdateInfo();
            if (scheduledUpdate == null || !ReleaseVersion.IsNewer(scheduledUpdate.GetVersion(), _currentVersion) ||
                !_packageVerifier.VerifyPackage(_updatesStoragePath, scheduledUpdate.GetVersion()))
            {
                _logger.Error($"Scheduled update executable is missing or invalid: {_schedInstallFilePath}");
                CancelScheduleInstallUpdate();
                return InstallUpdateStatus.ExtractFilesError;
            }

            try
            {
                var currentExecutable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(currentExecutable) || !File.Exists(currentExecutable))
                    throw new FileNotFoundException("The running application executable could not be found.", currentExecutable);
                if (!PathsEqual(currentExecutable, Path.Combine(_executableDir, CustomPackageVerifier.ExecutableName)))
                    throw new InvalidOperationException("Self-update requires running the installed Shatagon.exe.");

                if (!Directory.Exists(_updatesStoragePath))
                    Directory.CreateDirectory(_updatesStoragePath);

                var helperDirectory = Path.GetDirectoryName(_updateHelperPath);
                if (string.IsNullOrWhiteSpace(helperDirectory))
                    throw new InvalidOperationException("The update helper directory could not be determined.");
                Directory.CreateDirectory(helperDirectory);

                UpdateProcessHelper.PrepareHelperFiles(currentExecutable, _updateHelperPath);
                var pipeName = UpdateProcessHelper.CreateStartupPipeName();
                using var startupPipe = UpdateProcessHelper.CreateStartupPipe(pipeName);

                using var updateProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _updateHelperPath,
                        WorkingDirectory = _executableDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                updateProcess.StartInfo.ArgumentList.Add(UpdateProcessHelper.UpdateHelperArgument);
                updateProcess.StartInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                updateProcess.StartInfo.ArgumentList.Add(scheduledUpdate.GetVersion());
                updateProcess.StartInfo.ArgumentList.Add(Convert.ToHexString(UpdateProcessHelper.ComputeHash(_schedInstallFilePath)));
                updateProcess.StartInfo.ArgumentList.Add(pipeName);

                var helperReady = false;
                try
                {
                    helperReady = updateProcess.Start() &&
                        await UpdateProcessHelper.WaitForStartupAsync(startupPipe, updateProcess, cancellationToken).ConfigureAwait(false) == "ready";
                    if (helperReady)
                        return InstallUpdateStatus.Success;
                }
                finally
                {
                    if (!helperReady)
                        UpdateProcessHelper.StopProcess(updateProcess);
                }

                _logger.Info($"Failed to launch update helper: {_updateHelperPath}");
                return InstallUpdateStatus.LaunchScriptError;
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed to launch update helper: {_updateHelperPath}");
                return InstallUpdateStatus.LaunchScriptError;
            }
        }

        public UpdateInfo? GetScheduledUpdateInfo() => File.Exists(_schedInstallFilePath) ? JsonHelper.ReadFile<GitHubUpdateInfo>(_schedInstallJsonPath) : null;

        public bool IsAlreadyInstalledVersion(UpdateInfo updateInfo) =>
            ReleaseVersion.AreEqual(updateInfo.GetVersion(), _currentVersion);

        public void ApplyScheduledUpdateProps(UpdateInfo updateInfo) => _updateRepository.SetCurrentVersion(updateInfo.GetVersion());

        public bool ScheduleInstallUpdate(UpdateInfo updateInfo, string filePath)
        {
            _logger.Info($"Schedule install update with version: {updateInfo.GetVersion()}");
            if (!ReleaseVersion.IsNewer(updateInfo.GetVersion(), _currentVersion))
            {
                _logger.Warn($"Refusing to schedule non-newer version {updateInfo.GetVersion()} over {_currentVersion}.");
                return false;
            }

            if (File.Exists(filePath) && PathsEqual(filePath, _schedInstallFilePath) &&
                _packageVerifier.VerifyPackage(_updatesStoragePath, updateInfo.GetVersion()))
            {
                _updateRepository.SetCurrentVersion(_currentVersion);
                try
                {
                    if (!Directory.Exists(_updatesStoragePath))
                    {
                        Directory.CreateDirectory(_updatesStoragePath);
                    }
                    if (JsonHelper.WriteFile(_schedInstallJsonPath, updateInfo))
                    {
                        _updateRepository.SetCurrentVersion(updateInfo.GetVersion());
                        return true;
                    }
                    _logger.Error($"Failed write schedule json: {_schedInstallJsonPath}");
                    return false;
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Exception during schedule install update at: {filePath}");
                    CancelScheduleInstallUpdate();
                    return false;
                }
            }
            _logger.Error($"No schedule update package: {filePath}");
            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), pathComparison);
        }

        public bool CancelScheduleInstallUpdate()
        {
            _updateRepository.SetCurrentVersion(_currentVersion);
            if (File.Exists(_schedInstallJsonPath))
                FileUtils.DeleteFileNoThrow(_schedInstallJsonPath);
            return File.Exists(_schedInstallFilePath) &&
                FileUtils.DeleteFileNoThrow(_schedInstallFilePath);
        }

    }
}
