using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows;
using System.IO;

namespace SCTool_Redesigned.Utils
{
    public static class GameLauncherManager
    {

        public static void Start()
        {
            var processes = Process.GetProcessesByName("RSI Launcher");

            if (processes.Length > 0)
            {
                foreach (var process in processes)
                {
                    process.Kill();
                }
            }

            var launcher = new Process();
            var path = GetInstalledPath();

            if (string.IsNullOrEmpty(path))
            {
                throw new DllNotFoundException("NOT FOUND RSI LAUNCHER");
            }

            launcher.StartInfo.FileName = Path.Combine(GetInstalledPath(), "RSI Launcher.exe");
            launcher.Start();
        }

        public static string GetInstalledPath()
        {
            var path = string.Empty;

            // DO NOT USING ANYCPU PLATFORM!!!!!
            /* DO NOT USING ANYCPU PLATFORM!!!!!
             * ref: https://stackoverflow.com/questions/66366722/local-machine-registry-key-values-not-matching-when-trying-to-fetch-from-c-sharp 
             */
            using (RegistryKey registry = Registry.LocalMachine.OpenSubKey("SOFTWARE\\81bfc699-f883-50c7-b674-2483b6baae23", false))
            {
                if (registry == null)
                {
                    //MessageBox.Show(SCTool_Redesigned.Properties.Resources.MSG_Desc_NotFoundLauncher, SCTool_Redesigned.Properties.Resources.MSG_Title_GeneralError);
                    return null;
                }

                path = registry.GetValue("InstallLocation").ToString();
            }

            return path;
        }
    }
}
