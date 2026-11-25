using System.Collections.Generic;
using UnityEngine;

namespace Untai.PCG
{
    /// <summary>
    /// Pustaka chunk. Runtime memuat aset dari Resources/Untai/Chunks, lalu menambahkan definisi
    /// bawaan dari kode yang id-nya belum punya aset (misal chunk baru sebelum generator dijalankan).
    /// Daftar selalu diurutkan berdasarkan id agar hasil acak ber-seed deterministik.
    /// </summary>
    public static class ChunkLibrary
    {
        public const string ResourceFolder = "Untai/Chunks";

        public static List<ChunkDefinition> Load()
        {
            var list = new List<ChunkDefinition>(Resources.LoadAll<ChunkDefinition>(ResourceFolder));
            var defaults = CreateDefaults();
            for (int i = 0; i < defaults.Count; i++)
            {
                bool exists = false;
                for (int j = 0; j < list.Count && !exists; j++) exists = list[j].id == defaults[i].id;
                if (!exists) list.Add(defaults[i]);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return list;
        }

        static ChunkDefinition New() => ScriptableObject.CreateInstance<ChunkDefinition>();
        static PlatformSpec P(float x, float top, float w) => new PlatformSpec(x, top, w);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        // Dimensi chunk ayun wajib (lihat GameplayParameters): JumpDistance 5.38, jadi celah >= 7
        // (> 5.38 x 1.25 = 6.73). Jangkar diletakkan 3.5 ke kanan dan 3.5 di atas titik pegang di
        // tepi platform sehingga tali 4.95 masih terjangkau pada jangkauan jangkar tersulit (5.0).

        /// <summary>18 chunk: 4 mudah, 4 sedang, 4 sulit, 4 ayun wajib, 2 benang boros.</summary>
        public static List<ChunkDefinition> CreateDefaults()
        {
            return new List<ChunkDefinition>
            {
                // ---------- Mudah ----------
                New().Setup("E1", "Flat Walk", ChunkTier.Easy, 12f,
                    new[] { P(0, 0, 12) }, null, new[] { V(6, 0) }, new[] { 0 }),
                New().Setup("E2", "Small Gaps", ChunkTier.Easy, 13f,
                    new[] { P(0, 0, 4), P(6, 0, 3), P(11, 0, 2) }, null, new[] { V(2, 0) }, new[] { 1 }),
                New().Setup("E3", "Step Up", ChunkTier.Easy, 12f,
                    new[] { P(0, 0, 4), P(4, 1.5f, 8) }, null, new[] { V(9, 1.5f) }, new[] { 1 }),
                New().Setup("E4", "Step Down", ChunkTier.Easy, 12f,
                    new[] { P(0, 1.5f, 5), P(5, 0, 7) }, null, new[] { V(8.5f, 0) }, new[] { 1 }),

                // ---------- Sedang ----------
                New().Setup("M1", "Anchor Gap", ChunkTier.Medium, 14f,
                    new[] { P(0, 0, 4), P(9, 0, 5) }, new[] { V(6.5f, 5f) }, new[] { V(12, 0) }, new[] { 1 }),
                New().Setup("M2", "Pillars", ChunkTier.Medium, 15f,
                    new[] { P(0, 0, 3), P(5, 1.5f, 2), P(9, 3f, 2), P(13, 1.5f, 2) }),
                New().Setup("M3", "Guarded Bridge", ChunkTier.Medium, 16f,
                    new[] { P(0, 0, 3), P(6, 0, 10) }, null, new[] { V(9, 0), V(13, 0) }, new[] { 1, 1 }),
                New().Setup("M4", "High Gap", ChunkTier.Medium, 14f,
                    new[] { P(0, 1.5f, 4), P(8.5f, 1.5f, 5.5f) }, new[] { V(6.25f, 6f) }, new[] { V(11, 1.5f) }, new[] { 1 }),

                // ---------- Sulit ----------
                New().Setup("H1", "Long Swing", ChunkTier.Hard, 16f,
                    new[] { P(0, 0, 3), P(10, 0, 6) }, new[] { V(6.5f, 5.5f) }, new[] { V(13, 0) }, new[] { 1 }),
                New().Setup("H2", "Double Swing", ChunkTier.Hard, 18f,
                    new[] { P(0, 0, 3), P(8.5f, 1.5f, 1.5f), P(15, 0, 3) }, new[] { V(5.75f, 6f), V(13f, 6f) }),
                New().Setup("H3", "Gauntlet", ChunkTier.Hard, 16f,
                    new[] { P(0, 0, 16) }, null, new[] { V(3.5f, 0), V(6.5f, 0), V(9.5f, 0), V(12.5f, 0) }, new[] { 0, 0, 0 }),
                New().Setup("H4", "Tower Climb", ChunkTier.Hard, 14f,
                    new[] { P(0, 0, 3), P(5, 1.5f, 2), P(9.5f, 3f, 4.5f) }, null, new[] { V(12, 3f) }, new[] { 2 }),

                // ---------- Ayun wajib (celah >= 7, tidak bisa dilompati) ----------
                New().Setup("W1", "Swing Gap", ChunkTier.Medium, 16f,
                    new[] { P(0, 0, 4), P(11, 0, 5) }, new[] { V(7.5f, 4.5f) }, null, new[] { 1 }).MarkSwingOnly(),
                New().Setup("W2", "Swing Up", ChunkTier.Medium, 16f,
                    new[] { P(0, 0, 4), P(11.5f, 1.5f, 4.5f) }, new[] { V(7.5f, 4.5f) }).MarkSwingOnly(),
                New().Setup("W3", "Swing Chain", ChunkTier.Hard, 18.5f,
                    new[] { P(0, 0, 1.5f), P(8.5f, 0.5f, 1.5f), P(17, 0, 1.5f) },
                    new[] { V(5f, 4.5f), V(13.5f, 5f) }).MarkSwingOnly(),
                New().Setup("W4", "High Swing", ChunkTier.Hard, 16f,
                    new[] { P(0, 1.5f, 4), P(11, 1.5f, 5) }, new[] { V(7.5f, 6f) }, null, new[] { 1 }).MarkSwingOnly(),

                // ---------- Benang boros: jalur bawah panjang vs jalur atas pendek lewat ayunan ----------
                New().Setup("B1", "Valley Detour", ChunkTier.Medium, 16f,
                    new[] { P(0, 0, 3), P(4, -1.5f, 2), P(7, -3f, 3), P(11, -1.5f, 2), P(14, 0, 2) },
                    new[] { V(8.5f, 4.5f) }, new[] { V(8.5f, -3f) }, new[] { 2 }),
                New().Setup("B2", "Low Road", ChunkTier.Hard, 18f,
                    new[] { P(0, 1.5f, 3), P(4, 0, 2), P(7, -1.5f, 4), P(12, 0, 2), P(15, 1.5f, 3) },
                    new[] { V(9f, 6f) }, new[] { V(9f, -1.5f) }, new[] { 2 }),
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
