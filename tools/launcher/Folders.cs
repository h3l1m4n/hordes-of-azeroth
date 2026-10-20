using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HordesOfAzeroth.Launcher
{
    static class Folders
    {
        const int Attempts = 10;
        static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

        public static async Task MoveAsync(string from, string to, CancellationToken cancel)
        {
            await DeleteAsync(to, cancel);
            for (int attempt = 0; attempt < Attempts && !Directory.Exists(to); attempt++)
            {
                try
                {
                    Directory.Move(from, to);
                    return;
                }
                catch (Exception e) when (IsLocked(e))
                {
                    await Task.Delay(RetryDelay, cancel);
                }
            }
            CopyTree(from, to);
            await DeleteAsync(from, cancel);
        }

        public static async Task<bool> DeleteAsync(string path, CancellationToken cancel)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                if (!Directory.Exists(path))
                    return true;
                try
                {
                    Directory.Delete(path, true);
                    return true;
                }
                catch (Exception e) when (IsLocked(e))
                {
                    await Task.Delay(RetryDelay, cancel);
                }
            }
            return !Directory.Exists(path);
        }

        static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            foreach (string folder in Directory.GetDirectories(from))
                CopyTree(folder, Path.Combine(to, Path.GetFileName(folder)));
        }

        static bool IsLocked(Exception e) => e is IOException || e is UnauthorizedAccessException;
    }
}
