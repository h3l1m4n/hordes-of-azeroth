using System;
using System.Collections.Generic;
using System.IO;

namespace BonkTelegraphs
{
    static class Kit
    {
        public const string PatchName = "patch-3.MPQ";
        const uint FirstDisplayId = 7000;
        const string Folder = "World\\Generic\\BonkTelegraph\\";
        public static readonly uint[] FillMs = { 1000, 1500, 2000, 2500, 3000 };

        const float ConeHalfAngle = 45f;
        const float LinePieceSize = 3.0f;
        const float LinePieceRim = 0.18f;
        const float LinePieceMargin = 0.25f;
        public static readonly uint[] LinePieceFillMs = { 60, 80, 100, 125, 150, 200, 250, 300 };
        public const int LineMaxPieces = 12;

        static readonly string[] Shapes = { "Circle", "Cone", "Line" };

        public static void Build(string root)
        {
            string dataDir = Path.Combine(root, "Data");
            Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            byte[] playableLookup;
            Dbc displays, creatureDisplays, creatureModels, spells, spellVisuals, spellVisualKits, spellEffectNames, areaPois;
            List<string> projectiles, enemyMissiles, castVisuals;
            byte[] ticketShard, chainEffects;
            int standIns, landmarks;
            List<string> addonTextures;
            using (ClientData client = new ClientData(dataDir, new[] { PatchName }))
            {
                addonTextures = AddonTextures.Write(client, root);
                creatureDisplays = new Dbc(client.Read("DBFilesClient\\CreatureDisplayInfo.dbc"));
                creatureModels = new Dbc(client.Read("DBFilesClient\\CreatureModelData.dbc"));
                Wisps.AddWisps(client, files, creatureDisplays, creatureModels);
                Pickups.AddJewels(client, files, creatureDisplays, creatureModels);
                Pickups.AddTurkey(client, creatureDisplays, creatureModels);
                Traps.AddTraps(client, files, creatureDisplays, creatureModels);
                ticketShard = Pickups.ReadTicket(client);
                projectiles = Projectiles.AddProjectiles(client, files, creatureDisplays, creatureModels);
                enemyMissiles = Projectiles.AddEnemyMissiles(client, files, creatureDisplays, creatureModels);
                standIns = StandIns.AddStandIns(creatureDisplays, creatureModels);

                spells = new Dbc(client.Read("DBFilesClient\\Spell.dbc"));
                spellVisuals = new Dbc(client.Read("DBFilesClient\\SpellVisual.dbc"));
                spellVisualKits = new Dbc(client.Read("DBFilesClient\\SpellVisualKit.dbc"));
                castVisuals = CastVisuals.AddCastVisuals(spells, spellVisuals, spellVisualKits);
                castVisuals.AddRange(AreaVisuals.AddAreaVisuals(spells, spellVisuals, spellVisualKits));
                castVisuals.AddRange(GroundlessVisuals.AddGroundlessVisuals(spells, spellVisuals));
                castVisuals.AddRange(BoltVisuals.AddBoltVisuals(spells, spellVisuals));
                castVisuals.AddRange(ExploderVisuals.AddExploderVisuals(spells, spellVisuals, spellVisualKits));
                castVisuals.AddRange(MutedSounds.MuteMeleeSwings(spellVisualKits));
                castVisuals.AddRange(QuietKits.AddQuietCopies(spellVisualKits));
                spellEffectNames = new Dbc(client.Read("DBFilesClient\\SpellVisualEffectName.dbc"));
                castVisuals.AddRange(BossLooks.AddLooks(spellVisualKits, spellEffectNames));
                castVisuals.AddRange(AuraLooks.AddAuraLooks(spells, spellVisuals, spellVisualKits, spellEffectNames, new Dbc(client.Read("DBFilesClient\\SpellIcon.dbc"))));
                castVisuals.AddRange(AuraRings.AddSpells(spells, spellVisuals, spellVisualKits, spellEffectNames));
                castVisuals.AddRange(PenanceVisuals.AddPenanceVisuals(spells, spellVisuals, spellEffectNames));
                castVisuals.AddRange(LootBeams.AddLootBeams(client, files, spells, spellVisuals, spellVisualKits, spellEffectNames));

                areaPois = new Dbc(client.Read("DBFilesClient\\AreaPOI.dbc"));
                landmarks = Landmarks.HideFromMinimap(areaPois);
                chainEffects = ChainEffects.Quicken(client.Read("DBFilesClient\\SpellChainEffects.dbc"));

                byte[] reference = client.Read("SPELLS\\Consecration_Impact_Base.m2");
                if (reference == null)
                    throw new FileNotFoundException("Consecration_Impact_Base.m2 is not in the client's archives.");
                uint count = BitConverter.ToUInt32(reference, 0x2C), offset = BitConverter.ToUInt32(reference, 0x30);
                playableLookup = new byte[count * 4];
                Buffer.BlockCopy(reference, (int)offset, playableLookup, 0, playableLookup.Length);

                byte[] dbc = client.Read("DBFilesClient\\GameObjectDisplayInfo.dbc");
                if (dbc == null)
                    throw new FileNotFoundException("GameObjectDisplayInfo.dbc is not in the client's archives.");
                displays = new Dbc(dbc);
            }
            if (displays.MaxId() >= FirstDisplayId)
                throw new InvalidOperationException("GameObjectDisplayInfo.dbc already has ids from " + FirstDisplayId + ".");

            uint displayId = FirstDisplayId;
            List<string> manifest = new List<string>();
            foreach (string shape in Shapes)
            {
                Quad layout = GetLayout(shape);

                string outlineName = shape + "Outline";
                AddPiece(files, displays, playableLookup, displayId++, outlineName, layout, 0, 0, Paint(shape, layout, false), null);
                manifest.Add(outlineName);
                bool sized = shape != "Line";
                if (sized)
                    AddSizedPieces(files, playableLookup, outlineName, layout, 0, Folder + outlineName + ".blp");

                string fillTexture = Folder + shape + "Fill.blp";
                files[fillTexture] = Blp.WriteDxt3(Paint(shape, layout, true), true);
                foreach (uint ms in FillMs)
                {
                    string fillName = shape + "Fill" + ms;
                    AddPiece(files, displays, playableLookup, displayId++, fillName, layout, ms, 0, null, fillTexture);
                    manifest.Add(fillName);
                    if (sized)
                        AddSizedPieces(files, playableLookup, fillName, layout, ms, fillTexture);
                }
            }

            foreach (string end in new[] { "Start", "End" })
            {
                string name = "Line" + end + "Outline";
                Quad layout = GetLayout("Line");
                AddPiece(files, displays, playableLookup, displayId++, name, layout, 0, 0, PaintLinePiece(layout, false, end == "Start", end == "End"), null);
                manifest.Add(name);
            }

            float pieceHalf = LinePieceSize / 2 + LinePieceMargin;
            Quad sweep = new Quad { MinX = -LinePieceMargin, MaxX = LinePieceSize + LinePieceMargin, MinY = -pieceHalf, MaxY = pieceHalf,
                GrowAlongX = true };
            foreach (uint ms in LinePieceFillMs)
                for (int piece = 0; piece < LineMaxPieces; piece++)
                {
                    string name = "LineFill" + ms + "_" + piece;
                    AddPiece(files, displays, playableLookup, displayId++, name, sweep, ms, (uint)piece * ms, null, Folder + "LineFill.blp");
                    manifest.Add(name);
                }

            AuraRings.AddModels(files, playableLookup);
            Pickups.AddTicket(ticketShard, files, displays);

            Dictionary<string, byte[]> tables = new Dictionary<string, byte[]>
            {
                { "GameObjectDisplayInfo.dbc", displays.ToBytes() },
                { "CreatureDisplayInfo.dbc", creatureDisplays.ToBytes() },
                { "CreatureModelData.dbc", creatureModels.ToBytes() },
            };
            foreach (KeyValuePair<string, byte[]> table in tables)
                files["DBFilesClient\\" + table.Key] = table.Value;
            files["DBFilesClient\\Spell.dbc"] = spells.ToBytes();
            files["DBFilesClient\\SpellVisual.dbc"] = spellVisuals.ToBytes();
            files["DBFilesClient\\SpellVisualKit.dbc"] = spellVisualKits.ToBytes();
            files["DBFilesClient\\SpellVisualEffectName.dbc"] = spellEffectNames.ToBytes();
            files["DBFilesClient\\AreaPOI.dbc"] = areaPois.ToBytes();
            files["DBFilesClient\\SpellChainEffects.dbc"] = chainEffects;

            string patch = Path.Combine(dataDir, PatchName);
            MpqWriter.Write(patch, files);
            Console.WriteLine("Wrote {0} ({1} files).", patch, files.Count + 1);
            foreach (string texture in addonTextures)
                Console.WriteLine("Wrote {0} from the client's art.", texture);
            Console.WriteLine("  AreaPOI: {0} landmarks no longer point an arrow from the minimap's rim", landmarks);
            for (int i = 0; i < manifest.Count; i++)
                Console.WriteLine("  display {0}: {1}", FirstDisplayId + i, manifest[i]);
            Console.WriteLine("  creature display {0}: the spell orbs' light blue wisp", Wisps.BlueOrbDisplayId);
            Console.WriteLine("  creature displays {0}-{1}: XP orb jewels, blue, green, red, purple", Pickups.FirstJewelDisplayId, Pickups.FirstJewelDisplayId + 3);
            Console.WriteLine("  display {0}: the Darkmoon Ticket's crystal", Pickups.TicketDisplayId);
            Console.WriteLine("  creature display {0}: the Turkey Leg heal drop", Pickups.TurkeyDisplayId);
            Console.WriteLine("  creature displays {0}-{1}: thrown trap looks, Explosive and Frost Trap, set and sprung", Traps.FirstDisplayId, Traps.FirstDisplayId + 3);
            for (int i = 0; i < projectiles.Count; i++)
                Console.WriteLine("  creature display {0}: {1}", Projectiles.FirstDisplayId + i, projectiles[i]);
            for (int i = 0; i < enemyMissiles.Count; i++)
                Console.WriteLine("  creature display {0}: enemy bolt {1}", Projectiles.FirstEnemyDisplayId + i, enemyMissiles[i]);
            Console.WriteLine("  creature displays {0}-{1}: telegraph stand-ins", StandIns.FirstDisplayId, StandIns.FirstDisplayId + standIns - 1);
            foreach (string line in castVisuals)
                Console.WriteLine("  " + line);

            string serverDbc = Path.Combine(root, "server", "data", "5875", "dbc");
            if (Directory.Exists(serverDbc))
            {
                foreach (KeyValuePair<string, byte[]> table in tables)
                    File.WriteAllBytes(Path.Combine(serverDbc, table.Key), table.Value);
                Console.WriteLine("Wrote the same tables to {0}; restart the server to load them.", serverDbc);
            }
            else
                Console.WriteLine("No server data at {0}: extract it, then rerun this.", serverDbc);
        }

        static Quad GetLayout(string shape)
        {
            switch (shape)
            {
                case "Cone":
                    float halfWidth = (float)Math.Sin(ConeHalfAngle * Math.PI / 180);
                    return new Quad { MinX = 0, MaxX = 1, MinY = -halfWidth, MaxY = halfWidth };
                case "Line":
                    float half = LinePieceSize / 2 + LinePieceMargin;
                    return new Quad { MinX = -half, MaxX = half, MinY = -half, MaxY = half };
                default:
                    return new Quad { MinX = -1, MaxX = 1, MinY = -1, MaxY = 1 };
            }
        }

        static void AddPiece(Dictionary<string, byte[]> files, Dbc displays, byte[] playableLookup, uint displayId, string name, Quad layout, uint growMs,
            uint waitMs, Image texture, string texturePath)
        {
            string model = Folder + name;
            if (texturePath == null)
            {
                texturePath = model + ".blp";
                files[texturePath] = Blp.WriteDxt3(texture, true);
            }
            Quad quad = new Quad { MinX = layout.MinX, MaxX = layout.MaxX, MinY = layout.MinY, MaxY = layout.MaxY, Z = layout.Z,
                GrowAlongX = layout.GrowAlongX, GrowMs = growMs, WaitMs = waitMs, Texture = texturePath };
            files[model + ".m2"] = M2.Write("BonkTelegraph" + name, quad, playableLookup);
            uint[] row = new uint[displays.FieldCount];
            row[0] = displayId;
            row[1] = displays.AddString(model + ".mdx");
            displays.Records.Add(row);
        }

        static void AddSizedPieces(Dictionary<string, byte[]> files, byte[] playableLookup, string name, Quad layout, uint growMs, string texturePath)
        {
            foreach (float size in StandIns.Sizes)
            {
                string sizedName = StandIns.SizedName(name, size);
                Quad quad = new Quad { MinX = layout.MinX * size, MaxX = layout.MaxX * size, MinY = layout.MinY * size, MaxY = layout.MaxY * size,
                    Z = layout.Z, GrowAlongX = layout.GrowAlongX, GrowMs = growMs, Texture = texturePath, CastsNoShadow = true };
                files[Folder + sizedName + ".m2"] = M2.Write("BonkTelegraph" + sizedName, quad, playableLookup);
            }
        }

        static readonly float[] Rim = { 1.0f, 0.30f, 0.18f };
        static readonly float[] Wash = { 0.85f, 0.04f, 0.02f };
        const float OutlineRimAlpha = 0.95f, OutlineWashAlpha = 0.15f;
        const float FillRimAlpha = 0.75f, FillWashAlpha = 0.42f;
        const float RimWidth = 0.05f;

        static Image Paint(string shape, Quad quad, bool fill)
        {
            if (shape == "Line")
                return PaintLinePiece(quad, fill, false, false);
            int width = 256, height = 256;
            float spanX = quad.MaxX - quad.MinX, spanY = quad.MaxY - quad.MinY;
            float pixel = Math.Max(spanX / width, spanY / height);
            Image image = new Image(width, height);
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                {
                    float x = quad.MinX + (px + 0.5f) * spanX / width, y = quad.MinY + (py + 0.5f) * spanY / height;
                    float edge, lead;
                    Distances(shape, x, y, out edge, out lead);

                    float coverage = Clamp01(0.5f - edge / pixel);
                    if (coverage <= 0)
                    {
                        image.Set(px, py, Wash[0], Wash[1], Wash[2], 0);
                        continue;
                    }
                    float depth = fill ? lead : -edge;
                    float rim = Clamp01((RimWidth - depth) / pixel + 0.5f);
                    float alpha = fill ? Lerp(FillWashAlpha, FillRimAlpha, rim) : Lerp(OutlineWashAlpha, OutlineRimAlpha, rim);
                    image.Set(px, py, Lerp(Wash[0], Rim[0], rim), Lerp(Wash[1], Rim[1], rim), Lerp(Wash[2], Rim[2], rim), alpha * coverage);
                }
            return image;
        }

        static void Distances(string shape, float x, float y, out float edge, out float lead)
        {
            float r = (float)Math.Sqrt(x * x + y * y);
            switch (shape)
            {
                case "Cone":
                {
                    float s = (float)Math.Sin(ConeHalfAngle * Math.PI / 180), c = (float)Math.Cos(ConeHalfAngle * Math.PI / 180);
                    edge = Math.Max(r - 1, Math.Max(y * c - x * s, -y * c - x * s));
                    lead = 1 - r;
                    return;
                }
                default:
                    edge = r - 1;
                    lead = 1 - r;
                    return;
            }
        }

        static Image PaintLinePiece(Quad quad, bool fill, bool startEnd, bool endEnd)
        {
            int size = 128;
            float half = LinePieceSize / 2;
            float pixel = (quad.MaxX - quad.MinX) / size;
            Image image = new Image(size, size);
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    float x = quad.MinX + (px + 0.5f) * pixel, y = quad.MinY + (py + 0.5f) * pixel;
                    float edge = Math.Abs(y) - half;
                    if (startEnd)
                        edge = Math.Max(edge, -half - x);
                    if (endEnd)
                        edge = Math.Max(edge, x - half);
                    float coverage = Clamp01(0.5f - edge / pixel);
                    if (Math.Abs(x) > half)
                        coverage = 0;
                    float depth = fill ? half - Math.Abs(y) : -edge;
                    float rim = Clamp01((LinePieceRim - depth) / pixel + 0.5f);
                    float alpha = fill ? Lerp(FillWashAlpha, FillRimAlpha, rim) : Lerp(OutlineWashAlpha, OutlineRimAlpha, rim);
                    image.Set(px, py, Lerp(Wash[0], Rim[0], rim), Lerp(Wash[1], Rim[1], rim), Lerp(Wash[2], Rim[2], rim), alpha * coverage);
                }
            return image;
        }

        static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    }
}
