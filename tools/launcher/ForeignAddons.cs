using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HordesOfAzeroth.Launcher
{
    static class ForeignAddons
    {
        const string OwnAddon = "HordesOfAzeroth";
        const string BlizzardPrefix = "Blizzard_";
        const string StateFile = "AddOns.txt";
        const string SavedVariables = "SavedVariables";

        public static void Disable(GameFolder folder, string account)
        {
            if (folder.IsDevelopmentTree || !Directory.Exists(folder.AddOns))
                return;

            string[] lines = Directory.GetDirectories(folder.AddOns)
                .Select(Path.GetFileName)
                .Where(name => File.Exists(Path.Combine(folder.AddOns, name, name + ".toc")))
                .Select(name => name + ": " + (IsOurs(name) ? "enabled" : "disabled"))
                .ToArray();

            string accountDir = Path.Combine(folder.Root, "WTF", "Account", account.ToUpperInvariant());
            Directory.CreateDirectory(accountDir);
            foreach (string dir in CharacterFolders(accountDir).Prepend(accountDir))
                File.WriteAllLines(Path.Combine(dir, StateFile), lines, new UTF8Encoding(false));
        }

        static IEnumerable<string> CharacterFolders(string accountDir) =>
            Directory.GetDirectories(accountDir)
                .Where(realm => !Path.GetFileName(realm).Equals(SavedVariables, StringComparison.OrdinalIgnoreCase))
                .SelectMany(Directory.GetDirectories);

        static bool IsOurs(string name) =>
            name.Equals(OwnAddon, StringComparison.OrdinalIgnoreCase) || name.StartsWith(BlizzardPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
