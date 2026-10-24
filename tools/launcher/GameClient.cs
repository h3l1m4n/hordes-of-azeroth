using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HordesOfAzeroth.Launcher
{
    sealed class GameClient
    {
        const string Account = "player";
        const string Password = "player";

        readonly GameFolder m_folder;

        public GameClient(GameFolder folder)
        {
            m_folder = folder;
        }

        string RealmName => new UpdateCheck(m_folder).LoadCached()?.RealmName ?? "Hordes of Azeroth";

        public string ProcessName => Path.GetFileNameWithoutExtension(m_folder.GameExeName);

        public bool IsRunning => Process.GetProcessesByName(ProcessName).Length > 0;

        public DateTime LaunchedAt { get; private set; }

        public void Prepare(bool widescreen)
        {
            if (!File.Exists(m_folder.GameExe))
                throw new FileNotFoundException($"{m_folder.GameExeName} is missing. {LocalServer.RepairAdvice}");

            Directory.CreateDirectory(m_folder.GlueXml);
            Directory.CreateDirectory(Path.Combine(m_folder.Root, "Logs"));
            File.WriteAllLines(Path.Combine(m_folder.GlueXml, "BonkAccount.lua"), new[]
            {
                $"BONK_ACCOUNT = {LuaString(Account)};",
                $"BONK_PASSWORD = {LuaString(Password)};",
            }, new UTF8Encoding(false));
            string stats = Path.Combine(m_folder.GlueXml, "BonkStats.lua");
            if (!File.Exists(stats))
                File.WriteAllLines(stats, new[] { "BONK_STATS = nil;" }, new UTF8Encoding(false));

            if (Directory.Exists(m_folder.Cache))
                foreach (string cached in Directory.GetFiles(m_folder.Cache, "*.wdb"))
                    File.Delete(cached);

            ForeignAddons.Disable(m_folder, Account);
            GameConfig.EnsureDefaults(m_folder, RealmName);
            ClientPatcher.SetFieldOfView(m_folder, widescreen);
            TurboMod.Apply(m_folder);
            DxvkMod.Apply(m_folder);
        }

        public async Task<Process> StartAsync(CancellationToken cancel)
        {
            string vanillaFixes = Path.Combine(m_folder.Root, "VanillaFixes.exe");
            ProcessStartInfo info = File.Exists(vanillaFixes)
                ? new ProcessStartInfo(vanillaFixes, Quote(m_folder.GameExeName))
                : new ProcessStartInfo(m_folder.GameExe);
            info.WorkingDirectory = m_folder.Root;
            info.UseShellExecute = false;
            info.EnvironmentVariables[TurboMod.SkipVariable] = TurboMod.SkippedHooks;
            LaunchedAt = DateTime.UtcNow;
            Process.Start(info);

            Stopwatch clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(30))
            {
                Process game = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                if (game != null)
                    return game;
                await Task.Delay(250, cancel);
            }
            throw new TimeoutException("The game did not start. Check that VanillaFixes and the game files are complete.");
        }

        public async Task WaitForAllToCloseAsync(CancellationToken cancel)
        {
            while (IsRunning)
                await Task.Delay(1000, cancel);
        }

        static string LuaString(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        static string Quote(string text) => "\"" + text + "\"";
    }
}
