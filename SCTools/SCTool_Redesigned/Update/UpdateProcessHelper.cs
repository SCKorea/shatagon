using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace SCTool_Redesigned.Update
{
    internal static class UpdateProcessHelper
    {
        internal const string UpdateHelperArgument = "--update-helper";
        private const string UpdateFinishedArgument = "--update-finished";
        private const string InstalledExecutableName = "Shatagon.exe";
        private const string UpdateHelperDirectoryPrefix = "helper-";
        private const string StartupPipePrefix = "shatagon-update-";
        internal static bool UpdateFailed { get; private set; }

        internal static bool IsUpdateHelperInvocation(string[] args) =>
            args.Length > 0 && string.Equals(args[0], UpdateHelperArgument, StringComparison.Ordinal);

        internal static int RunUpdateHelper(string[] args)
        {
            if (args.Length != 5 ||
                !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parentProcessId) ||
                parentProcessId <= 0 || !ReleaseVersion.TryParse(args[2], out _) ||
                !TryParseHash(args[3], out var expectedHash) || !IsStartupPipeName(args[4]))
            {
                return 2;
            }

            if (!TryGetHelperPaths(out var helperPath, out var updatesDirectory, out var installDirectory))
                return 3;

            var installedExecutable = Path.Combine(installDirectory, InstalledExecutableName);
            var sourcePath = Path.Combine(updatesDirectory, InstalledExecutableName);
            var backupPath = Path.Combine(Path.GetDirectoryName(helperPath)!, "previous.exe");
            try
            {
                if (!ReleaseVersion.IsNewer(args[2], Assembly.GetExecutingAssembly().GetName().Version!.ToString(4)) ||
                    !new CustomPackageVerifier().VerifyPackage(updatesDirectory, args[2]) ||
                    !CryptographicOperations.FixedTimeEquals(ComputeHash(sourcePath), expectedHash) ||
                    !NotifyStartup(args[4], "ready"))
                {
                    return 3;
                }
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Failed to prepare the update helper: {exception}");
                return 3;
            }

            if (!WaitForParentProcess(parentProcessId, installedExecutable))
                return 4;

            var updateInstalled = false;
            try
            {
                updateInstalled = ReplaceInstalledExecutable(sourcePath, installedExecutable, backupPath, expectedHash);
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Failed to replace the installed executable: {exception}");
            }

            if (updateInstalled && StartInstalledApplication(installedExecutable, args[2]))
                return 0;

            if (File.Exists(backupPath) && !RestoreInstalledExecutable(backupPath, installedExecutable))
                return 5;

            return File.Exists(installedExecutable) && StartInstalledApplication(installedExecutable) ? 1 : 5;
        }

        internal static void HandleApplicationStartup(string[] args)
        {
            UpdateFailed = false;
            RemoveLegacyUpdateBatch();

            if (args.Length is not (3 or 4) ||
                !string.Equals(args[0], UpdateFinishedArgument, StringComparison.Ordinal) ||
                !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var helperProcessId) ||
                !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var updateStatus) ||
                helperProcessId <= 0)
            {
                return;
            }

            if (updateStatus != 0)
            {
                UpdateFailed = true;
                return;
            }

            // Acknowledge before waiting: the helper waits for this process to reach Main.
            if (args.Length == 4 && (!IsStartupPipeName(args[3]) ||
                !NotifyStartup(args[3], Assembly.GetExecutingAssembly().GetName().Version!.ToString(4))))
            {
                UpdateFailed = true;
                return;
            }

            var executablePath = Environment.ProcessPath;
            var installDirectory = string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Path.GetDirectoryName(executablePath);
            if (string.IsNullOrWhiteSpace(installDirectory) || !WaitForHelperProcess(helperProcessId, installDirectory))
                return;

            DeleteUpdatesDirectory(Path.Combine(installDirectory, "updates"));
        }

        private static bool TryGetHelperPaths(out string helperPath, out string updatesDirectory, out string installDirectory)
        {
            helperPath = Environment.ProcessPath ?? string.Empty;
            updatesDirectory = string.Empty;
            installDirectory = string.Empty;

            if (string.IsNullOrWhiteSpace(helperPath))
                return false;

            helperPath = Path.GetFullPath(helperPath);
            var helperDirectory = Path.GetDirectoryName(helperPath) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(helperDirectory))
                return false;

            var updatesInfo = Directory.GetParent(helperDirectory);
            var installInfo = updatesInfo?.Parent;
            if (updatesInfo == null || installInfo == null)
                return false;

            updatesDirectory = updatesInfo.FullName;
            installDirectory = installInfo.FullName;

            return !string.IsNullOrWhiteSpace(updatesDirectory) &&
                !string.IsNullOrWhiteSpace(installDirectory) &&
                PathsEqual(updatesDirectory, Path.Combine(installDirectory, "updates")) &&
                Path.GetFileName(helperDirectory).StartsWith(UpdateHelperDirectoryPrefix, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(helperPath), InstalledExecutableName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool WaitForParentProcess(int processId, string installedExecutable)
        {
            try
            {
                using var parentProcess = Process.GetProcessById(processId);
                var parentPath = parentProcess.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(parentPath))
                    return false;

                if (!PathsEqual(parentPath, installedExecutable))
                {
                    // The parent may exit before this helper starts waiting. If the OS has
                    // already reused its PID, the replacement process is not the app to wait for.
                    Trace.WriteLine($"The application process {processId} has exited; its PID now belongs to {parentPath}.");
                    return true;
                }

                return parentProcess.WaitForExit(TimeSpan.FromMinutes(2));
            }
            catch (ArgumentException)
            {
                // The application may have exited before the helper was scheduled.
                return true;
            }
            catch (InvalidOperationException)
            {
                // The parent process exited while its executable path was being read.
                return true;
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Failed while waiting for the application to exit: {exception}");
                return false;
            }
        }

        private static bool ReplaceInstalledExecutable(string sourcePath, string targetPath, string backupPath, byte[] sourceHash)
        {
            if (!File.Exists(sourcePath))
                return false;

            var sourceLength = new FileInfo(sourcePath).Length;
            if (sourceLength <= 0)
                return false;

            var replacementPath = targetPath + ".new";

            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    File.Copy(sourcePath, replacementPath, true);
                    if (!FilesMatch(replacementPath, sourceLength, sourceHash))
                        throw new IOException("The staged update executable does not match the downloaded file.");

                    File.Replace(replacementPath, targetPath, backupPath);
                    return FilesMatch(targetPath, sourceLength, sourceHash);
                }
                catch (IOException exception)
                {
                    Trace.WriteLine($"Update replacement attempt {attempt + 1} failed: {exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    Trace.WriteLine($"Update replacement attempt {attempt + 1} failed: {exception.Message}");
                }

                // A backup means replacement may have changed the target. Roll back before retrying or launching.
                if (File.Exists(backupPath))
                    break;
                Thread.Sleep(1000);
            }

            TryDeleteFile(replacementPath);
            return false;
        }

        private static bool RestoreInstalledExecutable(string backupPath, string targetPath)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    File.Move(backupPath, targetPath, true);
                    return true;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Trace.WriteLine($"Update rollback attempt {attempt + 1} failed: {exception.Message}");
                    Thread.Sleep(1000);
                }
            }
            return false;
        }

        private static bool StartInstalledApplication(string executablePath, string? expectedVersion = null)
        {
            Process? startedProcess = null;
            var started = false;
            try
            {
                var pipeName = CreateStartupPipeName();
                using var startupPipe = expectedVersion == null ? null : CreateStartupPipe(pipeName);
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add(UpdateFinishedArgument);
                startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add(expectedVersion == null ? "1" : "0");
                if (expectedVersion != null)
                    startInfo.ArgumentList.Add(pipeName);

                startedProcess = Process.Start(startInfo);
                if (startedProcess == null)
                    return false;
                if (expectedVersion == null)
                    return started = true;

                var version = WaitForStartupAsync(startupPipe!, startedProcess, CancellationToken.None).GetAwaiter().GetResult();
                return started = version != null && ReleaseVersion.AreEqual(version, expectedVersion);
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Failed to start the installed application: {exception}");
                return false;
            }
            finally
            {
                if (!started && startedProcess != null)
                    StopProcess(startedProcess);
                startedProcess?.Dispose();
            }
        }

        internal static void PrepareHelperFiles(string currentExecutable, string helperExecutable)
        {
            var helperDirectory = Path.GetDirectoryName(helperExecutable)!;
            Directory.CreateDirectory(helperDirectory);
            File.Copy(currentExecutable, helperExecutable, true);

            // A normal build produces an apphost plus sidecars; only publish creates the single-file bundle.
            if (!File.Exists(Path.ChangeExtension(currentExecutable, ".dll")))
                return;

            var sourceDirectory = Path.GetDirectoryName(currentExecutable)!;
            foreach (var library in Directory.EnumerateFiles(sourceDirectory, "*.dll"))
                File.Copy(library, Path.Combine(helperDirectory, Path.GetFileName(library)), true);
            foreach (var suffix in new[] { ".deps.json", ".runtimeconfig.json", ".runtimeconfig.dev.json" })
            {
                var sidecar = Path.ChangeExtension(currentExecutable, suffix);
                if (File.Exists(sidecar))
                    File.Copy(sidecar, Path.Combine(helperDirectory, Path.GetFileName(sidecar)), true);
            }
        }

        internal static string CreateStartupPipeName() => StartupPipePrefix + Guid.NewGuid().ToString("N");

        private static bool IsStartupPipeName(string name) =>
            name.StartsWith(StartupPipePrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(name[StartupPipePrefix.Length..], "N", out _);

        internal static NamedPipeServerStream CreateStartupPipe(string name) =>
            new(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        internal static async Task<string?> WaitForStartupAsync(NamedPipeServerStream pipe, Process process, CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                var exited = process.WaitForExitAsync(timeout.Token);
                var connected = pipe.WaitForConnectionAsync(timeout.Token);
                if (await Task.WhenAny(connected, exited).ConfigureAwait(false) == exited)
                    return null;
                await connected.ConfigureAwait(false);

                using var reader = new StreamReader(pipe, Encoding.UTF8, false, 128, true);
                var ready = reader.ReadLineAsync(timeout.Token).AsTask();
                return await Task.WhenAny(ready, exited).ConfigureAwait(false) == ready
                    ? await ready.ConfigureAwait(false) : null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            finally
            {
                timeout.Cancel();
            }
        }

        private static bool NotifyStartup(string pipeName, string message)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                pipe.Connect(10000);
                using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(message);
                return true;
            }
            catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
            {
                Trace.WriteLine($"Failed to acknowledge application startup: {exception}");
                return false;
            }
        }

        internal static void StopProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(TimeSpan.FromSeconds(10));
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                Trace.WriteLine($"Failed to stop the unsuccessful update process: {exception}");
            }
        }

        private static bool WaitForHelperProcess(int processId, string installDirectory)
        {
            if (processId == Environment.ProcessId)
                return false;

            try
            {
                using var helperProcess = Process.GetProcessById(processId);
                var helperPath = helperProcess.MainModule?.FileName;
                var expectedUpdatesDirectory = Path.Combine(installDirectory, "updates");
                var helperDirectory = string.IsNullOrWhiteSpace(helperPath)
                    ? null
                    : Path.GetDirectoryName(helperPath);
                var helperDirectoryInfo = string.IsNullOrWhiteSpace(helperDirectory)
                    ? null
                    : Directory.GetParent(helperDirectory);
                if (string.IsNullOrWhiteSpace(helperPath))
                {
                    return false;
                }

                if (helperDirectoryInfo == null ||
                    !PathsEqual(helperDirectoryInfo.FullName, expectedUpdatesDirectory) ||
                    !Path.GetFileName(helperDirectory ?? string.Empty)
                        .StartsWith(UpdateHelperDirectoryPrefix, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetFileName(helperPath), InstalledExecutableName, StringComparison.OrdinalIgnoreCase))
                {
                    // A different executable owns this PID, so the update helper has exited.
                    Trace.WriteLine($"The update helper process {processId} has exited; its PID now belongs to {helperPath}.");
                    return true;
                }

                return helperProcess.WaitForExit(TimeSpan.FromMinutes(2));
            }
            catch (ArgumentException)
            {
                // The helper may have exited before the updated application started.
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Exception exception)
            {
                Trace.WriteLine($"Failed while waiting for the update helper to exit: {exception}");
                return false;
            }
        }

        private static void DeleteUpdatesDirectory(string updatesDirectory)
        {
            for (var attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    if (!Directory.Exists(updatesDirectory))
                        return;

                    Directory.Delete(updatesDirectory, true);
                    return;
                }
                catch (IOException exception)
                {
                    Trace.WriteLine($"Update directory cleanup attempt {attempt + 1} failed: {exception.Message}");
                }
                catch (UnauthorizedAccessException exception)
                {
                    Trace.WriteLine($"Update directory cleanup attempt {attempt + 1} failed: {exception.Message}");
                }

                Thread.Sleep(500);
            }
        }

        private static void RemoveLegacyUpdateBatch()
        {
            var executablePath = Environment.ProcessPath;
            var installDirectory = string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Path.GetDirectoryName(executablePath);
            if (!string.IsNullOrWhiteSpace(installDirectory))
                TryDeleteFile(Path.Combine(installDirectory, "update.bat"));
        }

        internal static bool TryParseHash(string value, out byte[] hash)
        {
            hash = [];
            if (value.Length != 64 || !value.All(Uri.IsHexDigit))
                return false;
            hash = Convert.FromHexString(value);
            return true;
        }

        internal static byte[] ComputeHash(string path)
        {
            using var stream = File.OpenRead(path);
            return SHA256.HashData(stream);
        }

        private static bool FilesMatch(string path, long expectedLength, byte[] expectedHash)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != expectedLength)
                return false;

            return CryptographicOperations.FixedTimeEquals(ComputeHash(path), expectedHash);
        }

        private static bool PathsEqual(string left, string right)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException exception)
            {
                Trace.WriteLine($"Failed to remove legacy update file {path}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Trace.WriteLine($"Failed to remove legacy update file {path}: {exception.Message}");
            }
        }
    }
}
