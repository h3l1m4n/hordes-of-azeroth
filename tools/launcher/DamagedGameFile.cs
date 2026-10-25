using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HordesOfAzeroth.Launcher
{
    static class DamagedGameFile
    {
        static readonly Regex FileLine = new Regex(@"^File:\s+(.+\.mpq)\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        static readonly string[] LauncherBuiltArchives = { "patch-3.MPQ" };

        public static string CrashTextSince(GameFolder folder, DateTime launchedAtUtc)
        {
            string archive = ArchiveInCrashSince(folder, launchedAtUtc);
            if (archive == null)
                return null;
            string name = Path.GetFileName(archive);
            if (LauncherBuiltArchives.Contains(name, StringComparer.OrdinalIgnoreCase))
                return $"The game crashed because it couldn't read {archive}, which is damaged.\n\n" +
                    "Choose \"Repair the game…\" in the ••• menu next to Play to build it again. Your characters and unlocks stay as they are.";
            return $"The game crashed because it couldn't read {archive} in your World of Warcraft folder. " +
                "This file is damaged or isn't the standard 1.12.1 version, so some sounds or models in it can't be loaded.\n\n" +
                $"Replace {name} in the Data folder with the one from a clean World of Warcraft 1.12.1 (build 5875) client, then press Play again.";
        }

        static string ArchiveInCrashSince(GameFolder folder, DateTime launchedAtUtc)
        {
            string errors = Path.Combine(folder.Root, "Errors");
            if (!Directory.Exists(errors))
                return null;
            foreach (string report in Directory.GetFiles(errors, "*.txt")
                .Where(f => File.GetLastWriteTimeUtc(f) >= launchedAtUtc)
                .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                string text;
                try
                {
                    text = File.ReadAllText(report);
                }
                catch (IOException)
                {
                    continue;
                }
                Match file = FileLine.Match(text);
                if (file.Success)
                    return file.Groups[1].Value.Trim();
            }
            return null;
        }
    }
}
