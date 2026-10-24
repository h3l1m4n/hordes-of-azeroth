using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace HordesOfAzeroth.Launcher
{
    static class SmartAppControl
    {
        const string PolicyKey = @"SYSTEM\CurrentControlSet\Control\CI\Policy";
        const string StateValue = "VerifiedAndReputablePolicyState";
        const int Enforcing = 1;
        const string ClientFixLog = "HordesOfAzerothClientFix.log";

        public const string BlockedText = "Windows Smart App Control stopped the game from loading HordesOfAzerothClientFix.dll, so VanillaFixes closed the game.\n\n" +
            "Smart App Control blocks new programs that aren't signed yet, and Hordes of Azeroth isn't signed. Adding an exception in Windows Defender doesn't change this.\n\n" +
            "To play, open Windows Security, go to App & browser control, then Smart App Control settings, and choose Off. Then press Play again.";

        public static bool IsOn
        {
            get
            {
                try
                {
                    using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (RegistryKey policy = machine.OpenSubKey(PolicyKey))
                        return policy?.GetValue(StateValue) is int state && state == Enforcing;
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException || e is IOException)
                {
                    return false;
                }
            }
        }

        public static bool BlockedClientFix(GameFolder folder, DateTime launchedAt) =>
            IsOn && !ClientFixRanSince(folder, launchedAt);

        static bool ClientFixRanSince(GameFolder folder, DateTime launchedAt)
        {
            FileInfo log = new FileInfo(Path.Combine(folder.Root, "Logs", ClientFixLog));
            return log.Exists && log.LastWriteTimeUtc >= launchedAt;
        }
    }
}
