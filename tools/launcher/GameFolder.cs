using System;
using System.IO;
using System.Web.Script.Serialization;

namespace HordesOfAzeroth.Launcher
{
    sealed class GameFolder
    {
        public string Root { get; }
        public string Data => Path.Combine(Root, "Data");
        public string Config => Path.Combine(Root, "config");
        public string Server => Path.Combine(Root, "server");
        public string ServerRun => Path.Combine(Server, "run");
        public string ServerLogs => Path.Combine(Server, "logs");
        public string MariaDb => Path.Combine(Server, "mariadb");
        public string MariaDbData => Path.Combine(Server, "mariadb-data");
        public string LauncherData => Path.Combine(Root, "HordesOfAzeroth");
        public string VersionFile => Path.Combine(Root, "VERSION");
        public string LatestVersionFile => Path.Combine(ServerRun, "latest-version.txt");
        public string ConfigWtf => Path.Combine(Root, "WTF", "Config.wtf");
        public string GlueXml => Path.Combine(Root, "Interface", "GlueXML");
        public string Cache => Path.Combine(Root, "WDB");

        GameFolder(string root)
        {
            Root = root;
        }

        public static GameFolder Find()
        {
            string start = AppDomain.CurrentDomain.BaseDirectory;
            for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "WoW.exe")))
                    return new GameFolder(dir.FullName.TrimEnd(Path.DirectorySeparatorChar));
            return new GameFolder(start.TrimEnd(Path.DirectorySeparatorChar));
        }

        public bool IsDevelopmentTree => Directory.Exists(Path.Combine(Root, ".git"));
        public bool HasClient => File.Exists(Path.Combine(Root, "WoW.exe")) && Directory.Exists(Data);

        public string GameExeName
        {
            get
            {
                string spec = Path.Combine(Config, "client-patches.json");
                if (File.Exists(spec))
                {
                    var parsed = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(spec)) as System.Collections.Generic.Dictionary<string, object>;
                    if (parsed != null && parsed.TryGetValue("target", out object target) && target is string name)
                        return name;
                }
                return "HordesOfAzeroth.exe";
            }
        }

        public string GameExe => Path.Combine(Root, GameExeName);

        public GameVersion InstalledVersion => GameVersion.ReadFile(VersionFile);
    }
}
