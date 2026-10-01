using System.Globalization;

namespace SCTool_Redesigned.Update
{
    internal static class ReleaseVersion
    {
        internal static bool IsNewer(string candidate, string current) =>
            TryParse(candidate, out var candidateVersion) &&
            TryParse(current, out var currentVersion) &&
            candidateVersion > currentVersion;

        internal static bool AreEqual(string left, string right)
        {
            if (TryParse(left, out var leftVersion) && TryParse(right, out var rightVersion))
                return leftVersion == rightVersion;

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryParse(string? value, out Version version)
        {
            version = null!;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim();
            if (normalized[0] is 'v' or 'V')
                normalized = normalized[1..];

            var components = normalized.Split('.');
            if (components.Length is < 2 or > 4)
                return false;

            var numbers = new int[4];
            for (var i = 0; i < components.Length; i++)
            {
                if (!int.TryParse(components[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                    return false;
            }

            version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }
    }
}
