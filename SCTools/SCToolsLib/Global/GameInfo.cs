using System.Diagnostics;
using System.IO;

namespace NSW.StarCitizen.Tools.Lib.Global
{
    public sealed class GameInfo
    {
        public string Mode { get; }
        public string RootFolderPath { get; }
        public string ExeFilePath { get; }
        public string ExeVersion { get; }

        public static GameInfo? Create(GameMode mode, string gamePath)
        {
            var rootFolderPath = GameConstants.GetGameModePath(gamePath, mode);

            if (Directory.Exists(rootFolderPath))
            {
                var exeFilePath = GameConstants.GetGameExePath(rootFolderPath);
                if (File.Exists(exeFilePath))
                {
                    var exeVersionInfo = FileVersionInfo.GetVersionInfo(exeFilePath);
                    if (exeVersionInfo.FileVersion != null)
                    {
                        return new GameInfo(mode.ToString(), rootFolderPath, exeFilePath, exeVersionInfo.FileVersion);
                    }
                }
            }
            return null;
        }

        public static GameInfo? Create(string mode, string gamePath)
        {
            var rootFolderPath = GameConstants.GetGameModePath(gamePath, mode);

            if (!Directory.Exists(rootFolderPath))
            {
                return null;
            }

            var exeFilePath = GameConstants.GetGameExePath(rootFolderPath);

            if (!File.Exists(exeFilePath))
            {
                return null;
            }

            var exeVersionInfo = FileVersionInfo.GetVersionInfo(exeFilePath);

            if (exeVersionInfo.FileVersion == null)
            {
                return null;
            }

            return new GameInfo(mode, rootFolderPath, exeFilePath, exeVersionInfo.FileVersion);
        }

        private GameInfo(string mode, string rootFolderPath, string exeFilePath, string exeFileVersion)
        {
            Mode = mode;
            RootFolderPath = rootFolderPath;
            ExeFilePath = exeFilePath;
            ExeVersion = exeFileVersion.Replace(',', '.');
        }

        public bool IsAvailable() => File.Exists(ExeFilePath);

        public override string ToString() => Mode.ToString();
    }
}