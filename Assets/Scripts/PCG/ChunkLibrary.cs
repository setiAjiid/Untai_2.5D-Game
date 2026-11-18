using System.Collections.Generic;
using UnityEngine;

namespace Untai.PCG
{
    /// <summary>
    /// Pustaka chunk. Runtime memuat aset dari Resources/Untai/TandemChunks; jika belum ada,
    /// memakai definisi bawaan dari kode (sama persis dengan yang dibuat generator Editor).
    /// Daftar selalu diurutkan berdasarkan id agar hasil acak ber-seed deterministik.
    /// </summary>
    public static class ChunkLibrary
    {
        // Folder baru untuk chunk Tandem agar aset chunk versi lama tidak ikut termuat.
        public const string ResourceFolder = "Untai/TandemChunks";

        public static List<ChunkDefinition> Load()
        {
            var list = new List<ChunkDefinition>(Resources.LoadAll<ChunkDefinition>(ResourceFolder));
            if (list.Count == 0) list = CreateDefaults();
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list;
        }

        static ChunkDefinition New() => ScriptableObject.CreateInstance<ChunkDefinition>();
        static PlatformSpec P(float x, float top, float w) => new PlatformSpec(x, top, w);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>
        /// 14 chunk contoh untuk mekanik Tandem: 5 mudah, 4 sedang, 5 sulit.
        /// Chunk bertanda [untai] tidak bisa diselesaikan hanya dengan lompat: perlu ayun tandem
        /// (satu karakter jadi tumpuan di tepi) atau mengait jangkar lalu memanjat.
        /// </summary>
        public static List<ChunkDefinition> CreateDefaults()
        {
            return new List<ChunkDefinition>
            {
                // ---------- Mudah ----------
                New().Setup("E1", "Flat Walk", ChunkTier.Easy, 12f,
                    new[] { P(0, 0, 12) }, null, new[] { V(6, 0) }, new[] { 0 }),
                New().Setup("E2", "Small Gaps", ChunkTier.Easy, 13f,
                    new[] { P(0, 0, 4), P(6, 0, 3), P(11, 0, 2) }, null, new[] { V(2, 0) }),
                New().Setup("E3", "Step Up", ChunkTier.Easy, 12f,
                    new[] { P(0, 0, 4), P(4, 1.5f, 8) }, null, new[] { V(9, 1.5f) }, new[] { 1 }),
                // [untai] dinding 3 m (lebih tinggi dari lompatan): kait jangkar lalu panjat.
                New().Setup("E4", "First Anchor", ChunkTier.Easy, 12f,
                    new[] { P(0, 0, 5), P(5, 3f, 7) }, new[] { V(5.5f, 6f) }, null, new[] { 1 }),
                New().Setup("E5", "Drop Down", ChunkTier.Easy, 12f,
                    new[] { P(0, 3f, 5), P(5, 0, 7) }, null, new[] { V(8.5f, 0) }, new[] { 1 }),

                // ---------- Sedang ----------
                // [untai] jurang 6 m tanpa jangkar: satu karakter jadi tumpuan, yang lain berayun.
                New().Setup("M1", "Partner Gap", ChunkTier.Medium, 15f,
                    new[] { P(0, 0, 4), P(10, 0, 5) }, null, new[] { V(12.5f, 0) }, new[] { 1 }),
                // [untai] tebing 3,5 m: jangkar di atas tepi tebing.
                New().Setup("M2", "High Ledge", ChunkTier.Medium, 13f,
                    new[] { P(0, 0, 4), P(4, 3.5f, 9) }, new[] { V(4.5f, 6.5f) }, new[] { V(10, 3.5f) }, new[] { 1 }),
                New().Setup("M3", "Guarded Bridge", ChunkTier.Medium, 16f,
                    new[] { P(0, 0, 3), P(6, 0, 10) }, null, new[] { V(9, 0), V(13, 0) }, new[] { 1, 1 }),
                // [untai] jurang 6 m: jangkar atau ayun tandem.
                New().Setup("M4", "Rope Bridge", ChunkTier.Medium, 14f,
                    new[] { P(0, 1.5f, 4), P(10, 1.5f, 4) }, new[] { V(7f, 6.5f) }, new[] { V(12, 1.5f) }),

                // ---------- Sulit ----------
                // [untai] jurang 7 m: jangkar (untai pendek) atau ayun tandem (untai panjang).
                New().Setup("H1", "Long Swing", ChunkTier.Hard, 16f,
                    new[] { P(0, 0, 3), P(10, 0, 6) }, new[] { V(6.5f, 5.5f) }, new[] { V(13, 0) }, new[] { 1 }),
                // [untai] tebing lalu lompatan turun yang presisi.
                New().Setup("H2", "Wall and Chasm", ChunkTier.Hard, 18f,
                    new[] { P(0, 0, 4), P(4, 3.5f, 3), P(13, 1.5f, 5) }, new[] { V(4.5f, 6.5f) }, new[] { V(16, 1.5f) }, new[] { 2 }),
                New().Setup("H3", "Gauntlet", ChunkTier.Hard, 16f,
                    new[] { P(0, 0, 16) }, null, new[] { V(3.5f, 0), V(6.5f, 0), V(9.5f, 0), V(12.5f, 0) }, new[] { 0, 0, 0 }),
                // [untai] jurang 6 m tanpa jangkar, dijaga enemy di seberang.
                New().Setup("H4", "Hanging Bridge", ChunkTier.Hard, 14f,
                    new[] { P(0, 1.5f, 3), P(9, 1.5f, 5) }, null, new[] { V(11, 1.5f) }, new[] { 1 }),
                // [untai] dua jurang berturut-turut dengan pijakan sempit di tengah.
                New().Setup("H5", "Twin Anchors", ChunkTier.Hard, 20f,
                    new[] { P(0, 0, 3), P(9, 0, 2), P(17, 0, 3) }, new[] { V(6f, 5.5f), V(14f, 5.5f) }),
            };
        }

        /// <summary>Chunk awal: lantai datar aman untuk checkpoint pertama.</summary>
        public static ChunkDefinition CreateStart()
        {
            return New().Setup("S0", "Start", ChunkTier.Easy, 10f, new[] { P(0, 0, 10) });
        }

        /// <summary>Chunk akhir berisi goal, setinggi sambungan chunk sebelumnya.</summary>
        public static ChunkDefinition CreateGoal(float height)
        {
            return New().Setup("G0", "Goal", ChunkTier.Easy, 10f, new[] { P(0, height, 10) });
        }
    }
}
