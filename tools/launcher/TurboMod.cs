using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace HordesOfAzeroth.Launcher
{
    enum TurboBuild
    {
        Off = 0,
        Sse = 1,
        Avx = 2,
    }

    static class TurboMod
    {
        const uint PF_SSE4_2_INSTRUCTIONS_AVAILABLE = 38;
        const uint PF_AVX2_INSTRUCTIONS_AVAILABLE = 40;
        const string DllName = "wow_turbo.dll";
        const string LineMarker = "wow_turbo";
        const string StateFileName = "wow_turbo.txt";
        public const string SkipVariable = "WOW_TURBO_SKIP";
        public const string SkippedHooks = "FloatingTextPlaceRect__509520,FloatingFrame_AnchorAtScreenPos__509ec0,CreateWorldText__6c73f0,WorldTextOwner__Destroy__6c6cd0,WorldTextOwner__Draw__6c6e00,WorldTextOwner__Update__6c6d40,WorldText__Release__6c86a0,WorldText__Update__6c7cc0";

        static readonly Regex ExceptionAddress = new Regex(@"^Exception:\s+0x[0-9A-F]+\s+\(\w+\)\s+at\s+[0-9A-F]+:([0-9A-F]{8})",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        static readonly Regex ModuleRange = new Regex(@"^0x([0-9A-F]{8})\s+-\s+0x([0-9A-F]{8})\s+(.+?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        [DllImport("kernel32.dll")]
        static extern bool IsProcessorFeaturePresent(uint feature);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr module, string name);

        sealed class State
        {
            public TurboBuild Allowed = TurboBuild.Avx;
            public DateTime CheckedUtc = DateTime.MinValue;
            public TurboBuild Loaded = TurboBuild.Off;
            public string Reason = "";
        }

        public static TurboBuild Apply(GameFolder folder)
        {
            State state = Load(folder);
            foreach (string report in CrashReportsSince(folder, state.CheckedUtc))
            {
                TurboBuild crashed = FindCrashedBuild(report);
                if (crashed == TurboBuild.Off || crashed > state.Allowed)
                    continue;
                state.Allowed = crashed - 1;
                state.Reason = $"stepped down after a crash inside the {Name(crashed)} build ({Path.GetFileName(report)})";
            }
            state.CheckedUtc = DateTime.UtcNow;

            TurboBuild supported = SupportedBuild(out string hardware);
            TurboBuild chosen = (TurboBuild)Math.Min((int)supported, (int)state.Allowed);
            if (chosen != TurboBuild.Off && !File.Exists(DllPath(folder, chosen)))
                chosen = TurboBuild.Off;
            state.Loaded = chosen;
            if (state.Allowed == TurboBuild.Avx)
                state.Reason = hardware;

            WriteDllsTxt(folder, chosen);
            Save(folder, state);
            return chosen;
        }

        public static string Describe(GameFolder folder)
        {
            State state = Load(folder);
            return $"{Name(state.Loaded)} ({state.Reason})";
        }

        static TurboBuild SupportedBuild(out string hardware)
        {
            bool avx2 = IsProcessorFeaturePresent(PF_AVX2_INSTRUCTIONS_AVAILABLE);
            bool sse42 = IsProcessorFeaturePresent(PF_SSE4_2_INSTRUCTIONS_AVAILABLE);
            bool wine = IsWine();
            hardware = $"AVX2 {(avx2 ? "yes" : "no")}, SSE4.2 {(sse42 ? "yes" : "no")}{(wine ? ", Wine" : "")}";
            if (avx2 && !wine)
                return TurboBuild.Avx;
            if (sse42 || avx2)
                return TurboBuild.Sse;
            return TurboBuild.Off;
        }

        static bool IsWine()
        {
            IntPtr ntdll = GetModuleHandle("ntdll.dll");
            return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
        }

        static string Name(TurboBuild build) => build.ToString().ToLowerInvariant();

        static string RelativeDllPath(TurboBuild build) => Path.Combine("mods", "wow_turbo", Name(build), DllName);

        static string DllPath(GameFolder folder, TurboBuild build) => Path.Combine(folder.Root, RelativeDllPath(build));

        static void WriteDllsTxt(GameFolder folder, TurboBuild build)
        {
            string dllsTxt = Path.Combine(folder.Root, "dlls.txt");
            List<string> old = File.Exists(dllsTxt) ? File.ReadAllLines(dllsTxt).ToList() : new List<string>();
            List<string> lines = old.Where(l => l.IndexOf(LineMarker, StringComparison.OrdinalIgnoreCase) < 0 || l.TrimStart().StartsWith("#")).ToList();
            if (build != TurboBuild.Off)
                lines.Add(RelativeDllPath(build));
            if (!lines.SequenceEqual(old))
                File.WriteAllLines(dllsTxt, lines);
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

        static TurboBuild FindCrashedBuild(string report)
        {
            string text;
            try
            {
                text = File.ReadAllText(report);
            }
            catch (IOException)
            {
                return TurboBuild.Off;
            }
            Match exception = ExceptionAddress.Match(text);
            if (!exception.Success)
                return TurboBuild.Off;
            uint address = uint.Parse(exception.Groups[1].Value, NumberStyles.HexNumber);
            foreach (Match module in ModuleRange.Matches(text))
            {
                uint start = uint.Parse(module.Groups[1].Value, NumberStyles.HexNumber);
                uint end = uint.Parse(module.Groups[2].Value, NumberStyles.HexNumber);
                if (address < start || address >= end)
                    continue;
                string path = module.Groups[3].Value;
                if (!string.Equals(Path.GetFileName(path), DllName, StringComparison.OrdinalIgnoreCase))
                    return TurboBuild.Off;
                string build = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
                return string.Equals(build, Name(TurboBuild.Sse), StringComparison.OrdinalIgnoreCase) ? TurboBuild.Sse : TurboBuild.Avx;
            }
            return TurboBuild.Off;
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
                if (key == "Allowed" && Enum.TryParse(value, true, out TurboBuild allowed))
                    state.Allowed = allowed;
                else if (key == "Loaded" && Enum.TryParse(value, true, out TurboBuild loaded))
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
                "Allowed=" + Name(state.Allowed),
                "Loaded=" + Name(state.Loaded),
                "CheckedUtc=" + state.CheckedUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                "Reason=" + state.Reason,
            });
        }
    }
}
