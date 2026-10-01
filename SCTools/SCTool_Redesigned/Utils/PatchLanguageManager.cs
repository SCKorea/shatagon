using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NLog;

namespace SCTool_Redesigned.Utils
{
    public static class PatchLanguageManager
    {
        private const string DefaultLanguage = "english";
        private const string LanguageKey = "g_language";
        private const string AudioLanguageKey = "g_languageAudio";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static bool Enable(string path, string language) => SetLanguage(path, language);

        public static bool Disable(string path) => SetLanguage(path, DefaultLanguage);

        public static bool IsEnabled(string path)
        {
            var language = GetSetting(path, LanguageKey);
            return !string.IsNullOrWhiteSpace(language) &&
                !string.Equals(NormalizeValue(language), DefaultLanguage, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SetLanguage(string path, string language)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(language))
            {
                Logger.Error("Cannot update user.cfg because the path or language is empty.");
                return false;
            }

            string? temporaryPath = null;
            try
            {
                var fullPath = Path.GetFullPath(path);
                var lines = File.Exists(fullPath)
                    ? new List<string>(File.ReadAllLines(fullPath))
                    : new List<string>();
                var updatedLines = new List<string>(lines.Count + 2);
                var wroteLanguage = false;
                var wroteAudioLanguage = false;

                foreach (var line in lines)
                {
                    // Older versions treated user.cfg as an INI file and inserted this section.
                    // Star Citizen expects these settings as top-level key/value commands.
                    if (string.Equals(line.Trim(), "[Localization]", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (TryParseSetting(line, out var key, out _) &&
                        string.Equals(key, LanguageKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!wroteLanguage)
                        {
                            updatedLines.Add($"{LanguageKey}={language}");
                            wroteLanguage = true;
                        }
                        continue;
                    }

                    if (TryParseSetting(line, out key, out _) &&
                        string.Equals(key, AudioLanguageKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!wroteAudioLanguage)
                        {
                            updatedLines.Add($"{AudioLanguageKey}={DefaultLanguage}");
                            wroteAudioLanguage = true;
                        }
                        continue;
                    }

                    updatedLines.Add(line);
                }

                if (!wroteAudioLanguage)
                    updatedLines.Add($"{AudioLanguageKey}={DefaultLanguage}");
                if (!wroteLanguage)
                    updatedLines.Add($"{LanguageKey}={language}");

                var directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrWhiteSpace(directory))
                    throw new InvalidOperationException("The user.cfg directory could not be determined.");

                temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
                File.WriteAllLines(temporaryPath, updatedLines, new UTF8Encoding(false));

                if (File.Exists(fullPath))
                    File.Move(temporaryPath, fullPath, true);
                else
                    File.Move(temporaryPath, fullPath);

                var savedLanguage = GetSetting(fullPath, LanguageKey);
                var savedAudioLanguage = GetSetting(fullPath, AudioLanguageKey);
                var saved = string.Equals(savedLanguage, language, StringComparison.Ordinal) &&
                    string.Equals(savedAudioLanguage, DefaultLanguage, StringComparison.OrdinalIgnoreCase);
                if (!saved)
                    Logger.Error($"user.cfg did not retain the requested language settings: {fullPath}");

                return saved;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to save Star Citizen language settings to user.cfg: {path}");
                return false;
            }
            finally
            {
                if (temporaryPath != null && File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception exception)
                    {
                        Logger.Warn(exception, $"Failed to remove temporary user.cfg file: {temporaryPath}");
                    }
                }
            }
        }

        private static string? GetSetting(string path, string expectedKey)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                string? value = null;
                foreach (var line in File.ReadLines(path))
                {
                    if (TryParseSetting(line, out var key, out var settingValue) &&
                        string.Equals(key, expectedKey, StringComparison.OrdinalIgnoreCase))
                    {
                        value = settingValue;
                    }
                }

                return value;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to read Star Citizen language settings from user.cfg: {path}");
                return null;
            }
        }

        private static bool TryParseSetting(string line, out string key, out string value)
        {
            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                key = string.Empty;
                value = string.Empty;
                return false;
            }

            key = line.Substring(0, separator).Trim();
            value = NormalizeValue(line.Substring(separator + 1));
            return key.Length > 0;
        }

        private static string NormalizeValue(string value)
        {
            var normalized = value.Trim();
            if (normalized.Length >= 2 &&
                ((normalized[0] == '"' && normalized[^1] == '"') ||
                 (normalized[0] == '\'' && normalized[^1] == '\'')))
            {
                normalized = normalized[1..^1].Trim();
            }

            return normalized;
        }
    }
}
