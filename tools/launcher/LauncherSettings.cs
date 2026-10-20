using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HordesOfAzeroth.Launcher
{
    sealed class LauncherSettings
    {
        readonly string m_path;

        public bool KeepInTray = true;
        public bool BuildWhilePlaying = true;
        public bool Widescreen = true;

        LauncherSettings(string path)
        {
            m_path = path;
        }

        public static LauncherSettings Load(GameFolder folder)
        {
            LauncherSettings settings = new LauncherSettings(Path.Combine(folder.LauncherData, "launcher.ini"));
            if (!File.Exists(settings.m_path))
                return settings;
            Dictionary<string, string> values = File.ReadAllLines(settings.m_path)
                .Select(l => l.Split(new[] { '=' }, 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
            if (values.TryGetValue("KeepInTray", out string tray))
                settings.KeepInTray = tray != "0";
            if (values.TryGetValue("BuildWhilePlaying", out string build))
                settings.BuildWhilePlaying = build != "0";
            if (values.TryGetValue("Widescreen", out string widescreen))
                settings.Widescreen = widescreen != "0";
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(m_path));
            File.WriteAllLines(m_path, new[]
            {
                "KeepInTray=" + (KeepInTray ? "1" : "0"),
                "BuildWhilePlaying=" + (BuildWhilePlaying ? "1" : "0"),
                "Widescreen=" + (Widescreen ? "1" : "0"),
            });
        }
    }
}
