using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HordesOfAzeroth.Launcher
{
    static class GameConfig
    {
        static readonly Regex Setting = new Regex(@"^\s*SET\s+(\w+)\s+""([^""]*)""", RegexOptions.IgnoreCase);

        public static bool IsWindowed(GameFolder folder)
        {
            Dictionary<string, string> values = Read(folder);
            return !values.TryGetValue("gxWindow", out string window) || window == "1";
        }

        public static string Resolution(GameFolder folder)
        {
            Dictionary<string, string> values = Read(folder);
            return values.TryGetValue("gxResolution", out string resolution) ? resolution : DesktopResolution();
        }

        public static void EnsureDefaults(GameFolder folder, string realmName)
        {
            Dictionary<string, string> values = Read(folder);
            Dictionary<string, string> missing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> setting in new Dictionary<string, string>
            {
                ["readEULA"] = "1",
                ["readTOS"] = "1",
                ["readScanning"] = "-1",
                ["readContest"] = "-1",
                ["realmList"] = "127.0.0.1",
                ["realmName"] = realmName,
                ["accountName"] = "player",
                ["MasterVolume"] = "0.2",
                ["showTutorials"] = "0",
            })
                if (!values.ContainsKey(setting.Key))
                    missing[setting.Key] = setting.Value;
            if (!values.ContainsKey("gxWindow"))
            {
                missing["gxWindow"] = "1";
                missing["gxMaximize"] = "1";
                missing["gxMaximized"] = "1";
            }
            if (!values.TryGetValue("gxResolution", out string resolution) || !FitsPrimaryScreen(resolution))
                missing["gxResolution"] = DesktopResolution();
            if (!values.ContainsKey("hwDetect"))
                missing["hwDetect"] = "0";
            if (!values.ContainsKey("movie"))
                missing["movie"] = "0";
            if (!values.ContainsKey("cameraDistanceMaxFactor"))
                missing["cameraDistanceMaxFactor"] = "2";
            if (!values.ContainsKey("UnitNameNPC"))
                missing["UnitNameNPC"] = "1";
            if (!values.ContainsKey("cameraSmoothStyle"))
                missing["cameraSmoothStyle"] = "0";
            if (missing.Count > 0)
                Write(folder, missing);
        }

        public static void SetWindowed(GameFolder folder, bool windowed)
        {
            Dictionary<string, string> changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["gxWindow"] = windowed ? "1" : "0",
                ["gxMaximize"] = windowed ? "1" : "0",
                ["gxMaximized"] = windowed ? "1" : "0",
            };
            if (!Read(folder).ContainsKey("gxResolution"))
                changes["gxResolution"] = DesktopResolution();
            Write(folder, changes);
        }

        static string DesktopResolution()
        {
            System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            return $"{bounds.Width}x{bounds.Height}";
        }

        static bool FitsPrimaryScreen(string resolution)
        {
            string[] parts = resolution.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int width) || !int.TryParse(parts[1], out int height) || width <= 0 || height <= 0)
                return false;
            System.Drawing.Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            return width <= bounds.Width && height <= bounds.Height;
        }

        static Dictionary<string, string> Read(GameFolder folder)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(folder.ConfigWtf))
                return values;
            foreach (string line in File.ReadAllLines(folder.ConfigWtf))
            {
                Match match = Setting.Match(line);
                if (match.Success)
                    values[match.Groups[1].Value] = match.Groups[2].Value;
            }
            return values;
        }

        static void Write(GameFolder folder, Dictionary<string, string> changes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(folder.ConfigWtf));
            List<string> lines = File.Exists(folder.ConfigWtf) ? File.ReadAllLines(folder.ConfigWtf).ToList() : new List<string>();
            HashSet<string> done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < lines.Count; i++)
            {
                Match match = Setting.Match(lines[i]);
                if (match.Success && changes.TryGetValue(match.Groups[1].Value, out string value))
                {
                    lines[i] = $"SET {match.Groups[1].Value} \"{value}\"";
                    done.Add(match.Groups[1].Value);
                }
            }
            foreach (KeyValuePair<string, string> change in changes)
                if (!done.Contains(change.Key))
                    lines.Add($"SET {change.Key} \"{change.Value}\"");
            File.WriteAllLines(folder.ConfigWtf, lines, new UTF8Encoding(false));
        }
    }
}
