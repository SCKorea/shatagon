using System;
using System.Collections.Generic;
using System.IO;

namespace NSW.StarCitizen.Tools.Lib.Global
{
    public class GameFolders
    {
        public static List<GameInfo> GetGameModes(string gameFolder)
        {
            List<GameInfo> gameModes = [];

            if (string.IsNullOrEmpty(gameFolder))
            {
                return gameModes;
            }

            if (!Directory.Exists(gameFolder))
            {
                return gameModes;
            }

            DirectoryInfo folder = new DirectoryInfo(gameFolder);

            if (!folder.Exists)
            {
                return gameModes;
            }

            var gameFolders = folder.GetDirectories();

            foreach (var childFolder in gameFolders)
            {
                GameInfo? gameInfo = GameInfo.Create(childFolder.Name, gameFolder);

                if (gameInfo == null)
                {
                    continue;
                }

                gameModes.Add(gameInfo);
            }

            return gameModes;
        }

        public static string? SearchGameFolder(string searchPath)
        {
            var directory = new DirectoryInfo(searchPath);

            if (!directory.Exists)
            {
                return null;
            }

            if (IsGameModeFolderName(directory))
            {
                return directory.Parent?.FullName;
            }

            if (IsContainGameModes(searchPath))
            {
                return searchPath;
            }

            if (string.Compare(directory.Name, GameConstants.BinFolderName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return directory.Parent?.Parent?.FullName;
            }

            var searchFolder = Path.Combine(searchPath, GameConstants.GameFolderName);

            return Directory.Exists(searchFolder) ? searchFolder : searchPath;
        }

        private static bool IsContainGameModes(string gameFolder) => GetGameModes(gameFolder).Count > 0;

        private static bool IsGameModeFolderName(DirectoryInfo folder)
        {
            if (folder.Parent == null)
            {
                return false;
            }

            GameInfo? gameInfo = GameInfo.Create(folder.Name, folder.Parent.FullName);

            if (gameInfo == null)
            {
                return false;
            }

            return false;
        }
    }
}
