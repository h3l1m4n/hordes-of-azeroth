using System;
using System.IO;
using System.Windows.Media;
using BonkTelegraphs;

namespace HordesOfAzeroth.Launcher
{
    static class ClientFonts
    {
        public static FontFamily Title { get; private set; } = new FontFamily("Georgia");
        public static FontFamily Body { get; private set; } = new FontFamily("Georgia");

        public static void Load(GameFolder folder)
        {
            string archive = Path.Combine(folder.Data, "fonts.MPQ");
            if (!File.Exists(archive))
                return;
            string cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HordesOfAzeroth", "fonts");
            try
            {
                Directory.CreateDirectory(cache);
                using (MpqReader reader = new MpqReader(archive))
                {
                    Extract(reader, "Fonts\\MORPHEUS.TTF", Path.Combine(cache, "MORPHEUS.TTF"));
                    Extract(reader, "Fonts\\FRIZQT__.TTF", Path.Combine(cache, "FRIZQT__.TTF"));
                }
                Uri folderUri = new Uri(cache.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
                Title = new FontFamily(folderUri, "./#Morpheus, Georgia");
                Body = new FontFamily(folderUri, "./#Friz Quadrata TT, Georgia");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
            {
            }
        }

        static void Extract(MpqReader reader, string name, string target)
        {
            if (File.Exists(target))
                return;
            byte[] data = reader.Read(name);
            if (data != null)
                File.WriteAllBytes(target, data);
        }
    }
}
