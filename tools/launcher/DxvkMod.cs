using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HordesOfAzeroth.Launcher
{
    static class DxvkMod
    {
        const string DllName = "d3d9.dll";
        const string DisabledSuffix = ".disabled";
        const string StateFileName = "dxvk.txt";
        const int StackFramesChecked = 4;

        static readonly Regex ExceptionAddress = new Regex(@"^Exception:\s+0x[0-9A-F]+\s+\(\w+\)\s+at\s+[0-9A-F]+:([0-9A-F]{8})",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        static readonly Regex ModuleRange = new Regex(@"^0x([0-9A-F]{8})\s+-\s+0x([0-9A-F]{8})\s+(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        static readonly Regex StackFrame = new Regex(@"^[0-9A-F]{8}\s+[0-9A-F]{8}\s+[0-9A-F]{4}:[0-9A-F]{8}\s+(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        sealed class State
        {
            public bool Allowed = true;
            public DateTime CheckedUtc = DateTime.MinValue;
            public bool Loaded;
            public string Reason = "";
        }

        public static bool HasVulkanLoader =>
            File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "vulkan-1.dll"));

        public static bool IsEnabled(GameFolder folder) => Load(folder).Allowed;

        public static void Apply(GameFolder folder) => TurnOffAfterCrash(folder);

        public static bool TurnOffAfterCrash(GameFolder folder)
        {
            State state = Load(folder);
            bool turnedOff = false;
            if (state.Allowed)
                foreach (string report in CrashReportsSince(folder, state.CheckedUtc))
                {
                    string module = VulkanModuleInCrash(folder, report);
                    if (module == null)
                        continue;
                    state.Allowed = false;
                    state.Reason = $"turned off after a crash inside {module} ({Path.GetFileName(report)})";
                    turnedOff = true;
                    break;
                }
            state.CheckedUtc = DateTime.UtcNow;
            Store(folder, state);
            return turnedOff;
        }

        public static void SetEnabled(GameFolder folder, bool enabled)
        {
            State state = Load(folder);
            state.Allowed = enabled;
            state.CheckedUtc = DateTime.UtcNow;
            state.Reason = enabled ? "" : "turned off in the ••• menu";
            Store(folder, state);
        }

        public static string Describe(GameFolder folder)
        {
            State state = Load(folder);
            return $"{(state.Loaded ? "on" : "off")} ({state.Reason})";
        }

        static void Store(GameFolder folder, State state)
        {
            bool loader = HasVulkanLoader;
            if (state.Allowed)
                state.Reason = loader ? "Vulkan loader found" : "no 32-bit Vulkan loader, using Direct3D 9";
            state.Loaded = SetDllEnabled(folder, state.Allowed && loader);
            Save(folder, state);
        }

        static bool SetDllEnabled(GameFolder folder, bool enabled)
        {
            string active = Path.Combine(folder.Root, DllName);
            string parked = active + DisabledSuffix;
            if (enabled && !File.Exists(active) && File.Exists(parked))
                File.Move(parked, active);
            else if (!enabled && File.Exists(active))
            {
                if (File.Exists(parked))
                    File.Delete(parked);
                File.Move(active, parked);
            }
            return File.Exists(active);
        }

        static IEnumerable<string> CrashReportsSince(GameFolder folder, DateTime sinceUtc)
        {
            string errors = Path.Combine(folder.Root, "Errors");
            if (!Directory.Exists(errors))
                return Enumerable.Empty<string>();
            return Directory.GetFiles(errors, "*.txt")
                .Where(f => File.GetLastWriteTimeUtc(f) > sinceUtc)
                .OrderBy(File.GetLastWriteTimeUtc);
        }

        static string VulkanModuleInCrash(GameFolder folder, string report)
        {
            string text;
            try
            {
                text = File.ReadAllText(report);
            }
            catch (IOException)
            {
                return null;
            }
            Match exception = ExceptionAddress.Match(text);
            if (!exception.Success)
                return null;

            List<string> suspects = new List<string>();
            string faulting = ModuleAt(text, uint.Parse(exception.Groups[1].Value, NumberStyles.HexNumber));
            if (faulting != null)
                suspects.Add(faulting);
            suspects.AddRange(StackFrame.Matches(text).Cast<Match>().Take(StackFramesChecked).Select(m => m.Groups[1].Value));
            return suspects.Where(path => IsVulkanModule(folder, path)).Select(Path.GetFileName).FirstOrDefault();
        }

        static string ModuleAt(string text, uint address)
        {
            foreach (Match module in ModuleRange.Matches(text))
            {
                uint start = uint.Parse(module.Groups[1].Value, NumberStyles.HexNumber);
                uint end = uint.Parse(module.Groups[2].Value, NumberStyles.HexNumber);
                if (address >= start && address < end)
                    return module.Groups[3].Value;
            }
            return null;
        }

        static bool IsVulkanModule(GameFolder folder, string path)
        {
            string name = Path.GetFileName(path).ToLowerInvariant();
            if (name == DllName)
                return string.Equals(Path.GetFullPath(Path.GetDirectoryName(path) ?? "").TrimEnd('\\'),
                    Path.GetFullPath(folder.Root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            return name == "vulkan-1.dll" || name == "nvoglv32.dll" || name.Contains("vk32");
        }

        static string StatePath(GameFolder folder) => Path.Combine(folder.LauncherData, StateFileName);

        static State Load(GameFolder folder)
        {
            State state = new State();
            string path = StatePath(folder);
            if (!File.Exists(path))
                return state;
            foreach (string[] pair in File.ReadAllLines(path).Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2))
            {
                string key = pair[0].Trim(), value = pair[1].Trim();
                if (key == "Allowed" && bool.TryParse(value, out bool allowed))
                    state.Allowed = allowed;
                else if (key == "Loaded" && bool.TryParse(value, out bool loaded))
                    state.Loaded = loaded;
                else if (key == "CheckedUtc" && long.TryParse(value, out long ticks))
                    state.CheckedUtc = new DateTime(ticks, DateTimeKind.Utc);
                else if (key == "Reason")
                    state.Reason = value;
            }
            return state;
        }

        static void Save(GameFolder folder, State state)
        {
            Directory.CreateDirectory(folder.LauncherData);
            File.WriteAllLines(StatePath(folder), new[]
            {
                "Allowed=" + state.Allowed,
                "Loaded=" + state.Loaded,
                "CheckedUtc=" + state.CheckedUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                "Reason=" + state.Reason,
            });
        }
    }
}
