using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using NLog;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Localization;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Update;

namespace SCTool_Redesigned.Localization
{
    public class CustomLocalizationInstaller : ILocalizationInstaller
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public InstallStatus Install(string zipFileName, string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
            {
                _logger.Error($"Install directory is not exist: {destinationFolder}");
                return InstallStatus.FileError;
            }

            DirectoryInfo? unpackDataDir = null;
            DirectoryInfo? backupDataDir = null;
            var dataPathDir = new DirectoryInfo(GameConstants.GetDataFolderPath(destinationFolder));

            try
            {
                var unpackDataDirPath = Path.Combine(destinationFolder, "temp_" + Path.GetRandomFileName());
                unpackDataDir = Directory.CreateDirectory(unpackDataDirPath);

                if (!Unpack(zipFileName, unpackDataDir.FullName))
                {
                    _logger.Error($"Failed unpack install package to: {unpackDataDirPath}");
                    return InstallStatus.PackageError;
                }

                if (dataPathDir.Exists)
                {
                    var backupDataDirPath = Path.Combine(destinationFolder, "backup_" + Path.GetRandomFileName());

                    Directory.Move(dataPathDir.FullName, backupDataDirPath);
                    backupDataDir = new DirectoryInfo(backupDataDirPath);
                }

                Directory.Move(GameConstants.GetDataFolderPath(unpackDataDir.FullName), dataPathDir.FullName);

                var userConifgPath = Path.Combine(destinationFolder, "user.cfg");

                PatchLanguageManager.Enable(userConifgPath, App.Settings.GetOfficialLanauages()[App.Settings.GameLanguage]);

                if (backupDataDir != null)
                {
                    FileUtils.DeleteDirectoryNoThrow(backupDataDir, true);
                    backupDataDir = null;
                }
            }
            catch (CryptographicException e)
            {
                _logger.Error(e, "Exception during verify core");
                _logger.Error(e.Message);

                return InstallStatus.VerifyError;
            }
            catch (IOException e)
            {
                _logger.Error(e, "I/O exception during install");
                _logger.Error(e.Message);

                return InstallStatus.FileError;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Unexpected exception during install");
                _logger.Error(e.Message);

                return InstallStatus.UnknownError;
            }
            finally
            {
                if (unpackDataDir != null)
                {
                    FileUtils.DeleteDirectoryNoThrow(unpackDataDir, true);
                }
                if (backupDataDir != null)
                {
                    RestoreDirectory(backupDataDir, dataPathDir);
                }
            }

            return InstallStatus.Success;
        }

        public UninstallStatus Uninstall(string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
            {
                return UninstallStatus.Failed;
            }

            var userConifgPath = Path.Combine(destinationFolder, "user.cfg");

            if (File.Exists(userConifgPath))
            {
                PatchLanguageManager.Disable(userConifgPath);
            }

            var result = UninstallStatus.Success;
            var dataPathDir = new DirectoryInfo(GameConstants.GetDataFolderPath(destinationFolder));

            if (dataPathDir.Exists && !FileUtils.DeleteDirectoryNoThrow(dataPathDir, true))
                result = UninstallStatus.Partial;

            return result;
        }

        public LocalizationInstallationType GetInstallationType(string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
            {
                return LocalizationInstallationType.None;
            }

            if (!Directory.Exists(Path.Combine(destinationFolder, "data")))
            {
                return LocalizationInstallationType.None;
            }

            var userConifgPath = Path.Combine(destinationFolder, "user.cfg");

            if (!File.Exists(userConifgPath))
            {
                return LocalizationInstallationType.Disabled;
            }

            if (PatchLanguageManager.IsEnabled(userConifgPath))
            {
                return LocalizationInstallationType.Disabled;
            }

            return LocalizationInstallationType.Enabled;
        }

        public LocalizationInstallationType RevertLocalization(string destinationFolder)
        {
            var userConifgPath = Path.Combine(destinationFolder, "user.cfg");

            if (!File.Exists(userConifgPath))
            {
                return LocalizationInstallationType.None;
            }


            if (PatchLanguageManager.IsEnabled(userConifgPath))
            {
                PatchLanguageManager.Disable(userConifgPath);

                return LocalizationInstallationType.Disabled;
            }
            else
            {
                PatchLanguageManager.Enable(userConifgPath, App.Settings.GetOfficialLanauages()[App.Settings.GameLanguage]);

                return LocalizationInstallationType.Enabled;
            }
        }

        private static bool Unpack(string zipFileName, string destinationFolder)
        {
            using var archive = ZipFile.OpenRead(zipFileName);
            var language = App.Settings.GetOfficialLanauages()[App.Settings.GameLanguage];
            var target = $"data/Localization/{language}/global.ini";
            var variantEnabled = RepositoryManager.GetLocalizationSource().HasVariant;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selected = new List<(string Relative, ZipArchiveEntry Entry)>();
            var matches = 0;
            var newAsset = archive.Entries.Count == 1 &&
                archive.Entries[0].FullName.Equals(target, StringComparison.Ordinal);
            if (variantEnabled && RepositoryManager.TargetInfo is CustomUpdateInfo info &&
                !VariantCatalog.IsLegacy(info) && !newAsset)
            {
                _logger.Error($"Variant ZIP must contain only {target}: {zipFileName}");
                return false;
            }

            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                var segments = name.Split('/');
                if (name.StartsWith('/') || name.Contains('\\') || name.Contains(':') ||
                    segments.Any(segment => segment == ".." || segment == ".") || !seen.Add(name))
                {
                    _logger.Error($"Unsafe or duplicate ZIP entry: {name}");
                    return false;
                }
                if (newAsset)
                {
                    selected.Add((target, entry));
                    matches++;
                    break;
                }

                // Historical source ZIPs have exactly one repository root directory.
                if (segments.Length < 2 || string.IsNullOrWhiteSpace(segments[0]))
                    continue;
                var relative = name.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                    ? name : name.Substring(segments[0].Length + 1);
                if (relative.Equals(target, StringComparison.OrdinalIgnoreCase))
                    matches++;
                if (string.IsNullOrEmpty(entry.Name) ||
                    !relative.StartsWith("data/", StringComparison.OrdinalIgnoreCase) ||
                    (variantEnabled && !relative.Equals(target, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var destination = variantEnabled ? target : relative;
                if (!destinations.Add(destination) || entry.Length > 150L * 1024 * 1024)
                    return false;
                selected.Add((destination, entry));
            }

            if (matches != 1 || selected.Count == 0 ||
                selected.SingleOrDefault(pair => pair.Relative.Equals(target, StringComparison.OrdinalIgnoreCase)).Entry?.Length == 0)
            {
                _logger.Error($"ZIP must contain one valid {target}: {zipFileName}");
                return false;
            }
            foreach (var (relative, entry) in selected)
            {
                var output = Path.Combine(destinationFolder, Path.Combine(relative.Split('/')));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                entry.ExtractToFile(output);
            }
            return true;
        }

        private static void RestoreDirectory(DirectoryInfo dir, DirectoryInfo destDir)
        {
            if (dir.Exists)
            {
                try
                {
                    FileUtils.DeleteDirectoryNoThrow(destDir, true);
                    Directory.Move(dir.FullName, destDir.FullName);
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Unable restore data from directory: {dir.FullName}");
                    _logger.Error(e.Message);

                    FileUtils.DeleteDirectoryNoThrow(dir, true);
                }
            }
        }
    }
}
