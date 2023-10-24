using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using NLog;
using NSW.StarCitizen.Tools.Lib.Global;
using NSW.StarCitizen.Tools.Lib.Helpers;
using NSW.StarCitizen.Tools.Lib.Localization;
using Salaros.Configuration;
using SCTool_Redesigned.Utils;

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

            DirectoryInfo unpackDataDir = null;
            DirectoryInfo backupDataDir = null;
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

                var newLibraryPath = Path.Combine(unpackDataDir.FullName, GameConstants.PatcherOriginalName);

                //using var libraryCertVerifier = new FileCertVerifier(Resources.CoreSigning);
                //if (!libraryCertVerifier.VerifyFile(newLibraryPath))
                //{
                //    _logger.Error("Core certificate is invalid. Abort installation");
                //    return InstallStatus.VerifyError;
                //}

                if (dataPathDir.Exists)
                {
                    var backupDataDirPath = Path.Combine(destinationFolder, "backup_" + Path.GetRandomFileName());

                    Directory.Move(dataPathDir.FullName, backupDataDirPath);
                    backupDataDir = new DirectoryInfo(backupDataDirPath);
                }

                Directory.Move(GameConstants.GetDataFolderPath(unpackDataDir.FullName), dataPathDir.FullName);

                if (backupDataDir != null)
                {
                    FileUtils.DeleteDirectoryNoThrow(backupDataDir, true);
                    backupDataDir = null;
                }

                var userConifgPath = Path.Combine(destinationFolder, "user.cfg");

                PatchLanguageManager.Enable(userConifgPath, App.Settings.GetOfficialLanauages()[App.Settings.GameLanguage]);
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

            if (archive.Entries.Count == 0)
            {
                _logger.Error($"Failed unpack archive. No entries found: {zipFileName}");
                return false;
            }

            var dataExtracted = false;
            var coreExtracted = false;
            var rootEntry = archive.Entries[0];
            var dataPathStart = GameConstants.DataFolderName + "/";

            //extract only data folder and core module
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith(rootEntry.FullName, true, CultureInfo.InvariantCulture))
                {
                    var relativePath = entry.FullName.Substring(rootEntry.FullName.Length);

                    if (string.IsNullOrEmpty(entry.Name) && relativePath.EndsWith("/"))
                    {
                        var dir = Path.Combine(destinationFolder, relativePath);

                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                    }
                    else if (relativePath.StartsWith(dataPathStart, true, CultureInfo.InvariantCulture))
                    {
                        entry.ExtractToFile(Path.Combine(destinationFolder, relativePath), true);
                        dataExtracted = true;
                    }
                    else if (relativePath.Equals(GameConstants.PatcherOriginalName, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.ExtractToFile(Path.Combine(destinationFolder, relativePath), true);
                        coreExtracted = true;
                    }
                }
            }
            if (!dataExtracted || !coreExtracted)
            {
                _logger.Error($"Wrong localization archive: hasData={dataExtracted}, hasCore={coreExtracted}");
                return false;
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
