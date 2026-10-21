using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HordesOfAzeroth.Launcher
{
    sealed class InstallProgress
    {
        public string Step;
        public string Detail;
        public double Fraction = -1;
    }

    sealed class Installer
    {
        public const string StockClientMd5 = "CCF83146DBB3D10EF826AA4DE178A5BE";

        public static readonly string[] Steps =
        {
            "Download the game files",
            "Patch the game",
            "Read the maps",
            "Build the spell effects",
            "Set up the database",
        };

        readonly GameFolder m_folder;
        readonly LocalServer m_server;

        public Installer(GameFolder folder, LocalServer server)
        {
            m_folder = folder;
            m_server = server;
        }

        string StateFile => Path.Combine(m_folder.LauncherData, "install-steps.txt");
        string PackagesFile => Path.Combine(m_folder.LauncherData, "packages.txt");
        string Downloads => Path.Combine(m_folder.LauncherData, "downloads");
        string DataDir => Path.Combine(m_folder.Server, "data");
        string PendingVersion => Path.Combine(m_folder.LauncherData, "VERSION.pending");

        void FinishVersion()
        {
            if (!File.Exists(PendingVersion))
                return;
            File.Copy(PendingVersion, m_folder.VersionFile, true);
            File.Delete(PendingVersion);
        }

        public HashSet<string> CompletedSteps =>
            new HashSet<string>(File.Exists(StateFile) ? File.ReadAllLines(StateFile) : new string[0]);

        public bool IsComplete => Steps.All(CompletedSteps.Contains);

        public static bool IsStockClient(GameFolder folder)
        {
            string exe = Path.Combine(folder.Root, "WoW.exe");
            if (!File.Exists(exe))
                return false;
            using (MD5 md5 = MD5.Create())
            using (FileStream stream = File.OpenRead(exe))
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "") == StockClientMd5;
        }

        public static readonly string[] UpdateSteps =
        {
            "Back up your characters",
            "Download the update",
            "Patch the game",
            "Rebuild the spell effects",
            "Update the database",
        };

        const int BackupsKept = 3;

        string Backups => Path.Combine(m_folder.LauncherData, "backups");

        public HashSet<string> ChangedPackages(Manifest manifest)
        {
            Dictionary<string, string> installed = InstalledPackages();
            return new HashSet<string>(manifest.Packages
                .Where(p => !installed.TryGetValue(p.Name, out string hash) || hash != p.ContentHash)
                .Select(p => p.Name));
        }

        public async Task<bool> UpdateAsync(Manifest manifest, IProgress<InstallProgress> progress, CancellationToken cancel)
        {
            HashSet<string> changed = ChangedPackages(manifest);
            GameVersion from = m_folder.InstalledVersion;
            try
            {
                progress.Report(new InstallProgress { Step = UpdateSteps[0] });
                await BackUpAsync(from, cancel);

                progress.Report(new InstallProgress { Step = UpdateSteps[1] });
                await DownloadAsync(manifest, progress, UpdateSteps[1], cancel);

                progress.Report(new InstallProgress { Step = UpdateSteps[2] });
                Patch(manifest);

                progress.Report(new InstallProgress { Step = UpdateSteps[3] });
                if (changed.Contains("telegraphkit"))
                    await BuildSpellEffectsAsync(cancel);

                progress.Report(new InstallProgress { Step = UpdateSteps[4] });
                await UpdateDatabaseAsync(manifest, changed.Contains("world-db"), progress, UpdateSteps[4], cancel);
                FinishVersion();
            }
            finally
            {
                await m_server.StopAsync();
            }
            return changed.Contains("launcher");
        }

        async Task BackUpAsync(GameVersion version, CancellationToken cancel)
        {
            await m_server.StartDatabaseOnlyAsync(cancel);
            string folder = Path.Combine(Backups, $"{DateTime.Now:yyyy-MM-dd HHmmss} Patch {version?.Display ?? "unknown"}");
            Directory.CreateDirectory(folder);
            await m_server.BackUpAsync(Path.Combine(folder, "characters and accounts.sql"), "characters", "realmd");
            foreach (DirectoryInfo old in new DirectoryInfo(Backups).GetDirectories().OrderByDescending(d => d.Name).Skip(BackupsKept))
                old.Delete(true);
        }

        async Task UpdateDatabaseAsync(Manifest manifest, bool replaceWorld, IProgress<InstallProgress> progress, string step, CancellationToken cancel)
        {
            string sql = Path.Combine(m_folder.Server, "sql");
            await m_server.StartDatabaseOnlyAsync(cancel);
            if (replaceWorld)
            {
                progress.Report(new InstallProgress { Step = step, Detail = "loading the new world (about a minute)" });
                await m_server.QueryAsync("mysql", "DROP DATABASE IF EXISTS mangos");
                await m_server.RunSqlFileAsync(null, Path.Combine(m_folder.Config, "create-databases.sql"));
                await m_server.RunSqlFileAsync("mangos", Path.Combine(sql, "world.sql"));
            }
            progress.Report(new InstallProgress { Step = step, Detail = "applying updates to your characters" });
            await ApplyMigrationsAsync(Path.Combine(sql, "migrations"));
            string realm = manifest.RealmName.Replace("'", "''");
            await m_server.QueryAsync("realmd", $"UPDATE realmlist SET name = '{realm}' WHERE id = 1");
        }

        public async Task InstallAsync(Manifest manifest, IProgress<InstallProgress> progress, CancellationToken cancel)
        {
            Directory.CreateDirectory(m_folder.LauncherData);
            HashSet<string> done = CompletedSteps;
            foreach (string step in Steps)
            {
                if (done.Contains(step))
                    continue;
                progress.Report(new InstallProgress { Step = step });
                switch (Array.IndexOf(Steps, step))
                {
                    case 0: await DownloadAsync(manifest, progress, step, cancel); break;
                    case 1: Patch(manifest); break;
                    case 2: await ReadMapsAsync(progress, step, cancel); break;
                    case 3: await BuildSpellEffectsAsync(cancel); break;
                    case 4: await SetUpDatabaseAsync(manifest, progress, step, cancel); break;
                }
                File.AppendAllLines(StateFile, new[] { step });
            }
            FinishVersion();
        }

        public async Task<bool> RefreshLauncherBeforeResumingAsync(Manifest manifest, CancellationToken cancel)
        {
            if (!CompletedSteps.Contains(Steps[0]) || !LauncherOutdated(manifest))
                return false;
            await RefreshLauncherAsync(manifest, cancel);
            return true;
        }

        public bool LauncherOutdated(Manifest manifest)
        {
            PackageEntry launcher = manifest.Packages.FirstOrDefault(p => p.Name == "launcher");
            return launcher != null && !(InstalledPackages().TryGetValue(launcher.Name, out string hash) && hash == launcher.ContentHash);
        }

        public async Task RefreshLauncherAsync(Manifest manifest, CancellationToken cancel)
        {
            PackageEntry launcher = manifest.Packages.First(p => p.Name == "launcher");
            Dictionary<string, string> installed = InstalledPackages();
            string file = await FetchAsync(launcher.Url, launcher.Sha256, _ => { }, cancel);
            Unzip(file, m_folder.Root, false, PendingVersion);
            installed[launcher.Name] = launcher.ContentHash;
            File.WriteAllLines(PackagesFile, installed.Select(p => p.Key + "=" + p.Value));
            File.Delete(file);
        }

        async Task DownloadAsync(Manifest manifest, IProgress<InstallProgress> progress, string step, CancellationToken cancel)
        {
            Dictionary<string, string> installed = InstalledPackages();
            List<PackageEntry> packages = manifest.Packages.Where(p => !installed.TryGetValue(p.Name, out string hash) || hash != p.ContentHash).ToList();
            List<ThirdPartyEntry> thirdParty = manifest.ThirdParty.Where(t => !ThirdPartyInstalled(t)).ToList();
            long total = packages.Sum(p => p.Size) + thirdParty.Sum(t => t.Size);
            long finished = 0;
            Action<long> report = bytes => progress.Report(new InstallProgress
            {
                Step = step,
                Detail = $"{(finished + bytes) / 1048576.0:0.0} of {total / 1048576.0:0.0} MB",
                Fraction = total > 0 ? (double)(finished + bytes) / total : 0,
            });

            foreach (ThirdPartyEntry item in thirdParty)
            {
                string file = await FetchAsync(item.Url, item.Sha256, report, cancel);
                if (item.CopyTo != null)
                    File.Copy(file, Path.Combine(m_folder.Root, item.CopyTo), true);
                else
                    Unzip(file, Path.Combine(m_folder.Root, item.UnzipTo ?? "."), item.StripTopFolder, null);
                finished += item.Size;
            }
            foreach (PackageEntry package in packages)
            {
                string file = await FetchAsync(package.Url, package.Sha256, report, cancel);
                Unzip(file, m_folder.Root, false, PendingVersion);
                installed[package.Name] = package.ContentHash;
                File.WriteAllLines(PackagesFile, installed.Select(p => p.Key + "=" + p.Value));
                finished += package.Size;
                File.Delete(file);
            }
        }

        public Dictionary<string, string> InstalledPackages()
        {
            return File.Exists(PackagesFile)
                ? File.ReadAllLines(PackagesFile).Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1])
                : new Dictionary<string, string>();
        }

        bool ThirdPartyInstalled(ThirdPartyEntry item)
        {
            if (item.CopyTo != null)
                return File.Exists(Path.Combine(m_folder.Root, item.CopyTo));
            if (item.Name == "MariaDB")
                return File.Exists(Path.Combine(m_folder.MariaDb, "bin", "mariadbd.exe"));
            if (item.Name == "VanillaFixes")
                return File.Exists(Path.Combine(m_folder.Root, "VanillaFixes.exe"));
            return false;
        }

        public async Task<string> FetchAsync(string url, string sha256, Action<long> report, CancellationToken cancel)
        {
            Directory.CreateDirectory(Downloads);
            string name = Path.GetFileName(new Uri(url).AbsolutePath);
            string target = Path.Combine(Downloads, name);
            if (File.Exists(target) && Sha256(target) == sha256)
                return target;

            string local = UpdateCheck.LocalSource == null ? null : Path.Combine(UpdateCheck.LocalSource, name);
            if (local != null && File.Exists(local))
            {
                File.Copy(local, target, true);
            }
            else
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (WebClient client = new WebClient())
                using (cancel.Register(client.CancelAsync))
                {
                    client.Headers[HttpRequestHeader.UserAgent] = "HordesOfAzerothLauncher";
                    client.DownloadProgressChanged += (s, e) => report(e.BytesReceived);
                    try
                    {
                        await client.DownloadFileTaskAsync(new Uri(url), target);
                    }
                    catch (WebException e) when (!cancel.IsCancellationRequested)
                    {
                        if (File.Exists(target))
                            File.Delete(target);
                        throw new InvalidOperationException($"Couldn't download {name} ({e.Message.TrimEnd('.')}). Check your connection and try again.");
                    }
                }
            }
            if (Sha256(target) != sha256)
            {
                File.Delete(target);
                throw new InvalidDataException($"{name} didn't download correctly. Try again.");
            }
            return target;
        }

        static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static readonly string RunningExe = Path.GetFullPath(Process.GetCurrentProcess().MainModule.FileName);

        public const string LauncherFileName = "HoALauncher.exe";
        static readonly string[] RetiredLauncherNames = { "HordesOfAzerothLauncher.exe" };

        public static void RemoveOldLauncher()
        {
            string folder = Path.GetDirectoryName(RunningExe);
            List<string> leftovers = new List<string> { RunningExe + ".old" };
            if (string.Equals(Path.GetFileName(RunningExe), LauncherFileName, StringComparison.OrdinalIgnoreCase))
                foreach (string retired in RetiredLauncherNames)
                {
                    leftovers.Add(Path.Combine(folder, retired));
                    leftovers.Add(Path.Combine(folder, retired + ".old"));
                }
            foreach (string leftover in leftovers)
                try
                {
                    if (File.Exists(leftover))
                        File.Delete(leftover);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                }
        }

        static void Unzip(string zip, string targetDir, bool stripTopFolder, string versionTarget)
        {
            string root = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (ZipArchive archive = ZipFile.OpenRead(zip))
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    if (stripTopFolder)
                    {
                        int slash = relative.IndexOf(Path.DirectorySeparatorChar);
                        if (slash < 0)
                            continue;
                        relative = relative.Substring(slash + 1);
                    }
                    if (relative.Length == 0)
                        continue;
                    string target = versionTarget != null && relative == "VERSION" ? versionTarget : Path.GetFullPath(Path.Combine(root, relative));
                    if (target != versionTarget && !target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"{Path.GetFileName(zip)} contains a path outside the game folder.");
                    if (relative.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    if (string.Equals(target, RunningExe, StringComparison.OrdinalIgnoreCase) && File.Exists(target))
                    {
                        string old = target + ".old";
                        if (File.Exists(old))
                            File.Delete(old);
                        File.Move(target, old);
                    }
                    entry.ExtractToFile(target, true);
                }
        }

        void Patch(Manifest manifest)
        {
            ClientPatcher.Apply(m_folder);
            File.WriteAllText(Path.Combine(m_folder.Root, "realmlist.wtf"), "set realmlist 127.0.0.1\r\n");
            string dllsTxt = Path.Combine(m_folder.Root, "dlls.txt");
            List<string> lines = File.Exists(dllsTxt) ? File.ReadAllLines(dllsTxt).ToList() : new List<string>();
            foreach (string dll in manifest.ClientDlls)
                if (!lines.Contains(dll))
                    lines.Add(dll);
            File.WriteAllLines(dllsTxt, lines);
            GameConfig.EnsureDefaults(m_folder, manifest.RealmName);
        }

        async Task ReadMapsAsync(IProgress<InstallProgress> progress, string step, CancellationToken cancel)
        {
            string extractors = Path.Combine(m_folder.ServerRun, "Extractors");
            Directory.CreateDirectory(Path.Combine(DataDir, "vmaps"));

            progress.Report(new InstallProgress { Step = step, Detail = "terrain and areas", Fraction = 0.05 });
            await RunToolAsync(Path.Combine(extractors, "MapExtractor.exe"), DataDir, cancel, "-i", Quote(m_folder.Root), "--silent");
            string dbcTarget = Path.Combine(DataDir, "5875", "dbc");
            Directory.CreateDirectory(Path.GetDirectoryName(dbcTarget));
            await Folders.MoveAsync(Path.Combine(DataDir, "dbc"), dbcTarget, cancel);

            progress.Report(new InstallProgress { Step = step, Detail = "buildings and trees", Fraction = 0.4 });
            await RunToolAsync(Path.Combine(extractors, "VMapExtractor.exe"), DataDir, cancel, "-d", Quote(m_folder.Data), "--silent");
            progress.Report(new InstallProgress { Step = step, Detail = "line of sight", Fraction = 0.8 });
            await RunToolAsync(Path.Combine(extractors, "VMapAssembler.exe"), DataDir, cancel, "Buildings", "vmaps", "--silent");
            await Folders.DeleteAsync(Path.Combine(DataDir, "Buildings"), cancel);
        }

        async Task BuildSpellEffectsAsync(CancellationToken cancel)
        {
            await RunToolAsync(Path.Combine(m_folder.Root, "tools", "TelegraphKit.exe"), m_folder.Root, cancel, "build", Quote(m_folder.Root));
        }

        async Task SetUpDatabaseAsync(Manifest manifest, IProgress<InstallProgress> progress, string step, CancellationToken cancel)
        {
            string sql = Path.Combine(m_folder.Server, "sql");
            if (!m_server.DatabaseExists)
            {
                progress.Report(new InstallProgress { Step = step, Detail = "creating the database", Fraction = 0.05 });
                await m_server.CreateDatabaseFilesAsync();
            }
            await m_server.StartDatabaseOnlyAsync(cancel);
            await m_server.RunSqlFileAsync(null, Path.Combine(m_folder.Config, "create-databases.sql"));

            string[,] imports =
            {
                { "realmd", "logon.sql", "account" },
                { "logs", "logs.sql", "logs_characters" },
                { "characters", "characters.sql", "characters" },
                { "mangos", "world.sql", "bonk_map" },
            };
            for (int i = 0; i < imports.GetLength(0); i++)
            {
                string database = imports[i, 0];
                if (await TableExistsAsync(database, imports[i, 2]))
                    continue;
                progress.Report(new InstallProgress { Step = step, Detail = database == "mangos" ? "loading the world (about a minute)" : "creating " + database, Fraction = 0.1 + 0.15 * i });
                await m_server.RunSqlFileAsync(database, Path.Combine(sql, imports[i, 1]));
            }

            progress.Report(new InstallProgress { Step = step, Detail = "applying updates", Fraction = 0.75 });
            await ApplyMigrationsAsync(Path.Combine(sql, "migrations"));

            string realm = manifest.RealmName.Replace("'", "''");
            await m_server.QueryAsync("realmd", $"INSERT INTO realmlist (id, name, address, port) VALUES (1, '{realm}', '127.0.0.1', 8085) ON DUPLICATE KEY UPDATE name = '{realm}', address = '127.0.0.1', port = 8085");

            string accounts = await m_server.QueryAsync("realmd", "SELECT COUNT(*) FROM account WHERE username = 'PLAYER'");
            if (accounts.Trim() == "0")
            {
                progress.Report(new InstallProgress { Step = step, Detail = "creating your account", Fraction = 0.85 });
                await m_server.StartAsync(new Progress<string>(_ => { }), cancel);
                await m_server.RunConsoleAsync("account create player player");
                bool created = (await m_server.QueryAsync("realmd", "SELECT COUNT(*) FROM account WHERE username = 'PLAYER'")).Trim() != "0";
                await m_server.StopAsync();
                if (!created)
                    throw new InvalidOperationException("The account couldn't be created. See server/logs/Server.log.");
            }
            else
            {
                await m_server.StopAsync();
            }
        }

        async Task<bool> TableExistsAsync(string database, string table)
        {
            string count = await m_server.QueryAsync("information_schema", $"SELECT COUNT(*) FROM TABLES WHERE TABLE_SCHEMA = '{database}' AND TABLE_NAME = '{table}'");
            return count.Trim() != "0";
        }

        async Task ApplyMigrationsAsync(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            Dictionary<string, string> databases = new Dictionary<string, string> { ["characters"] = "characters", ["logon"] = "realmd", ["logs"] = "logs" };
            foreach (string file in Directory.GetFiles(folder, "*.sql").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
            {
                string[] parts = Path.GetFileNameWithoutExtension(file).Split('_');
                if (parts.Length != 2 || !databases.TryGetValue(parts[1], out string database))
                    continue;
                string known = await m_server.QueryAsync(database, $"SELECT COUNT(*) FROM migrations WHERE id = '{parts[0]}'");
                if (known.Trim() != "0")
                    continue;
                await m_server.RunSqlFileAsync(database, file);
            }
        }

        static async Task RunToolAsync(string exe, string workingDirectory, CancellationToken cancel, params string[] arguments)
        {
            if (!File.Exists(exe))
                throw new FileNotFoundException($"{Path.GetFileName(exe)} is missing from the download.");
            ProcessStartInfo info = new ProcessStartInfo(exe, string.Join(" ", arguments))
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            Queue<string> lastLines = new Queue<string>();
            DataReceivedEventHandler remember = (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data) || e.Data.StartsWith("Processing"))
                    return;
                lock (lastLines)
                {
                    lastLines.Enqueue(e.Data.Trim());
                    while (lastLines.Count > 3)
                        lastLines.Dequeue();
                }
            };
            using (Process process = Process.Start(info))
            {
                process.OutputDataReceived += remember;
                process.ErrorDataReceived += remember;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.StandardInput.Close();
                using (cancel.Register(() => { try { process.Kill(); } catch (InvalidOperationException) { } }))
                    while (!process.HasExited)
                        await Task.Delay(200);
                process.WaitForExit();
                cancel.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                {
                    string said;
                    lock (lastLines)
                        said = string.Join(" / ", lastLines);
                    throw new InvalidOperationException($"{Path.GetFileName(exe)} stopped with code {process.ExitCode}: {said}");
                }
            }
        }

        static string Quote(string text) => "\"" + text + "\"";
    }
}
