using System.IO;
using System.Reflection;
using NSW.StarCitizen.Tools.Lib.Update;

namespace SCTool_Redesigned.Update
{
    internal class CustomPackageVerifier : ApplicationUpdater.IPackageVerifier
    {
        private string ExecutorName = Assembly.GetExecutingAssembly().GetName().Name;

        public bool VerifyPackage(string path)
        {
            if (!File.Exists(Path.Combine(path, $"{ExecutorName}.exe")) ||
                !File.Exists(Path.Combine(path, $"{ExecutorName}.exe.config")))
            {
                return false;
            }
            return true;
        }
    }
}