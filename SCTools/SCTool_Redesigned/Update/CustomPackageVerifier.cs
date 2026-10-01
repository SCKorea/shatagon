using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;

namespace SCTool_Redesigned.Update
{
    internal class CustomPackageVerifier : CustomApplicationUpdater.IPackageVerifier
    {
        internal const string ExecutableName = "Shatagon.exe";

        public bool VerifyPackage(string path, string? expectedVersion = null)
        {
            try
            {
                var executable = Path.Combine(path, ExecutableName);
                using var stream = File.OpenRead(executable);
                using var reader = new PEReader(stream);
                var headers = reader.PEHeaders;
                if (headers.PEHeader == null || headers.CoffHeader.Machine != Machine.Amd64 ||
                    (headers.CoffHeader.Characteristics & Characteristics.ExecutableImage) == 0 ||
                    (headers.CoffHeader.Characteristics & Characteristics.Dll) != 0)
                {
                    return false;
                }

                var info = FileVersionInfo.GetVersionInfo(executable);
                return string.Equals(info.ProductName, "Shatagon Patcher", StringComparison.Ordinal) &&
                    string.Equals(info.OriginalFilename, "Shatagon.dll", StringComparison.OrdinalIgnoreCase) &&
                    ReleaseVersion.TryParse(info.FileVersion, out _) &&
                    (expectedVersion == null || ReleaseVersion.AreEqual(info.FileVersion!, expectedVersion));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
            {
                return false;
            }
        }
    }
}
