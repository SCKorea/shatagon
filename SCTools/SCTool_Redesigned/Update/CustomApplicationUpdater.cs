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
        private readonly string _updateScriptContent;
        private readonly string _updateScriptPath;
        private readonly string _updatesStoragePath;
        private readonly string _schedInstallFilePath;
        private readonly string _schedInstallJsonPath;
        private readonly string _currentVersion;

        public interface IPackageVerifier
        {
            bool VerifyPackage(string path);
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
            string updateScriptContent, IPackageVerifier packageVerifier)
        {
            if (updateRepository.CurrentVersion == null)
                throw new InvalidOperationException("update repository current version is not set");
            _updateRepository = updateRepository;
            _executableDir = executableDir;
            _updateScriptContent = updateScriptContent;
            _packageVerifier = packageVerifier;
            _updateScriptPath = Path.Combine(_executableDir, "update.bat");
            _updatesStoragePath = Path.Combine(_executableDir, "updates");
            _schedInstallFilePath = Path.Combine(_updatesStoragePath, "shatagon.exe");
            _schedInstallJsonPath = Path.Combine(_updatesStoragePath, "latest.json");
            _currentVersion = _updateRepository.CurrentVersion;
        }

        public void Dispose() => _updateRepository.Dispose();

        public void MonitorStart(int refreshTime) => _updateRepository.MonitorStart(refreshTime);

        public void MonitorStop() => _updateRepository.MonitorStop();

        public async Task<UpdateInfo?> CheckForUpdateVersionAsync(CancellationToken cancellationToken)
        {
            var latestUpdateInfo = await _updateRepository.GetLatestAsync(cancellationToken);
            if (latestUpdateInfo != null && string.Compare(latestUpdateInfo.GetVersion(),
                _currentVersion, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return latestUpdateInfo;
            }
            return null;
        }

        public async Task<string> DownloadVersionAsync(UpdateInfo version, CancellationToken cancellationToken, IDownloadProgress downloadProgress)
        {
            if (!Directory.Exists(_updatesStoragePath))
            {
                Directory.CreateDirectory(_updatesStoragePath);
            }
            return await _updateRepository.DownloadAsync(version, _updatesStoragePath, cancellationToken, downloadProgress);
        }

        public InstallUpdateStatus InstallScheduledUpdate()
        {
            _logger.Info("Install scheduled update");
            if (ExtractUpdateScript())
            {
                using var updateProcess = new Process();
                updateProcess.StartInfo.UseShellExecute = false;
                updateProcess.StartInfo.RedirectStandardInput = false;
                updateProcess.StartInfo.RedirectStandardOutput = false;
                updateProcess.StartInfo.RedirectStandardError = false;
                updateProcess.StartInfo.ErrorDialog = false;
                updateProcess.StartInfo.CreateNoWindow = true;
                updateProcess.StartInfo.WorkingDirectory = _executableDir;
                updateProcess.StartInfo.FileName = _updateScriptPath;
                if (!updateProcess.Start())
                {
                    RemoveUpdateScript();
                    _logger.Info($"Failed launch updater script: {_updateScriptPath}");
                    return InstallUpdateStatus.LaunchScriptError;
                }
                return InstallUpdateStatus.Success;
            }
            CancelScheduleInstallUpdate();
            return InstallUpdateStatus.ExtractFilesError;
        }

        public UpdateInfo? GetScheduledUpdateInfo() => File.Exists(_schedInstallFilePath) ? JsonHelper.ReadFile<GitHubUpdateInfo>(_schedInstallJsonPath) : null;

        public bool IsAlreadyInstalledVersion(UpdateInfo updateInfo) =>
            string.Compare(updateInfo.GetVersion(), _currentVersion, StringComparison.OrdinalIgnoreCase) == 0;

        public void ApplyScheduledUpdateProps(UpdateInfo updateInfo) => _updateRepository.SetCurrentVersion(updateInfo.GetVersion());

        public bool ScheduleInstallUpdate(UpdateInfo updateInfo, string filePath)
        {
            _logger.Info($"Schedule install update with version: {updateInfo.GetVersion()}");
            if (File.Exists(filePath))
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

        public bool CancelScheduleInstallUpdate()
        {
            _updateRepository.SetCurrentVersion(_currentVersion);
            if (File.Exists(_schedInstallJsonPath))
                FileUtils.DeleteFileNoThrow(_schedInstallJsonPath);
            return File.Exists(_schedInstallFilePath) &&
                FileUtils.DeleteFileNoThrow(_schedInstallFilePath);
        }

        public void RemoveUpdateScript()
        {
            if (File.Exists(_updateScriptPath))
            {
                FileUtils.DeleteFileNoThrow(_updateScriptPath);
            }
        }

        private bool ExtractUpdateScript()
        {
            try
            {
                File.WriteAllText(_updateScriptPath, _updateScriptContent);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Failed extract update script to: {_updateScriptPath}");
                return false;
            }
            return true;
        }
    }
}