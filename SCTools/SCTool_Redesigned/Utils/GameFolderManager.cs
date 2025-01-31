using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NSW.StarCitizen.Tools.Lib.Global;

namespace SCTool_Redesigned.Utils
{
    public static class GameFolderManager
    {
        public static List<string> GetInstalledFolder(string path)
        {
            DirectoryInfo parentFolder = new DirectoryInfo(path);
            DirectoryInfo[] gamefolders = parentFolder.GetDirectories();

            List<string> installedGameFolders = [];

            foreach (var innerFolder in gamefolders)
            {
                var gameExe = GameConstants.GetGameExePath(innerFolder.FullName);

                if (string.IsNullOrEmpty(gameExe) || !File.Exists(GameConstants.GetGameExePath(innerFolder.FullName)))
                {
                    continue;
                }

                installedGameFolders.Add(innerFolder.Name);
            }

            return installedGameFolders;
        }

    }
}
