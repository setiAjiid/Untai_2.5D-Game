using System;
using System.Collections.Generic;
using UnityEngine;

namespace Untai.PCG
{
    public enum ChunkTier { Easy = 0, Medium = 1, Hard = 2 }

    /// <summary>Platform dalam koordinat lokal chunk: x = tepi kiri, top = tinggi permukaan atas.</summary>
    [Serializable]
    public struct PlatformSpec
    {
        public float x;
        public float top;
        public float width;

        public PlatformSpec(float x, float top, float width) { this.x = x; this.top = top; this.width = width; }
        public float Right => x + width;
    }

    /// <summary>Titik spawn enemy: indeks platform tempat enemy berpatroli.</summary>
    [Serializable]
    public struct EnemySpawnSpec
    {
        public int platform;
        public EnemySpawnSpec(int platform) { this.platform = platform; }
    }

    /// <summary>
    /// Data satu chunk level. Chunk tidak memakai prefab: LevelBuilder membangunnya menjadi
    /// primitif saat runtime. Platform pertama mulai di x=0 dan platform terakhir berakhir di
    /// x=width sehingga chunk bersambung tanpa celah; kecocokan sambungan ditentukan oleh
    /// entryHeight dan exitHeight.
    /// </summary>
    [CreateAssetMenu(menuName = "Untai/Chunk Definition", fileName = "Chunk")]
    public class ChunkDefinition : ScriptableObject
    {
        public string id = "X0";
        public string displayName = "Chunk";
        public ChunkTier tier = ChunkTier.Easy;
        public float width = 12f;
        [Tooltip("Tinggi permukaan di sisi masuk (kiri).")]
        public float entryHeight;
        [Tooltip("Tinggi permukaan di sisi keluar (kanan).")]
        public float exitHeight;

        public List<PlatformSpec> platforms = new List<PlatformSpec>();
        public List<Vector2> anchors = new List<Vector2>();
        [Tooltip("Posisi kandidat jebakan (x lokal, y = permukaan platform). Muncul sesuai trapDensity.")]
        public List<Vector2> traps = new List<Vector2>();
        public List<EnemySpawnSpec> enemySpawns = new List<EnemySpawnSpec>();
        [Tooltip("Chunk ayun wajib: celah lebih lebar dari lompatan, hanya bisa diseberangi dengan ayunan.")]
        public bool swingOnly;
        [Tooltip("Posisi peti tarik (x lokal, y = permukaan platform).")]
        public List<Vector2> crateSpawns = new List<Vector2>();

        /// <summary>Tinggi peti tarik; validator menambahkannya ke tinggi lompat pada chunk berpeti.</summary>
        public const float CrateHeight = 1.2f;

        /// <summary>Lebar celah terbesar antar platform berurutan.</summary>
        public float MaxGap
        {
            get
            {
                float max = 0f;
                for (int i = 1; i < platforms.Count; i++)
                    max = Mathf.Max(max, platforms[i].x - platforms[i - 1].Right);
                return max;
            }
        }

        public string TierLetter => tier == ChunkTier.Easy ? "E" : tier == ChunkTier.Medium ? "M" : "H";

        /// <summary>Isi data chunk lewat kode (dipakai pustaka bawaan dan generator Editor).</summary>
        public ChunkDefinition Setup(string chunkId, string label, ChunkTier chunkTier, float chunkWidth,
            PlatformSpec[] plats, Vector2[] anchorPoints = null, Vector2[] trapPoints = null, int[] enemyPlatforms = null)
        {
            id = chunkId;
            name = chunkId;
            displayName = label;
            tier = chunkTier;
            width = chunkWidth;
            platforms = new List<PlatformSpec>(plats);
            anchors = anchorPoints != null ? new List<Vector2>(anchorPoints) : new List<Vector2>();
            traps = trapPoints != null ? new List<Vector2>(trapPoints) : new List<Vector2>();
            enemySpawns = new List<EnemySpawnSpec>();
            swingOnly = false;
            crateSpawns = new List<Vector2>();
            if (enemyPlatforms != null)
                foreach (int p in enemyPlatforms) enemySpawns.Add(new EnemySpawnSpec(p));
            entryHeight = plats[0].top;
            exitHeight = plats[plats.Length - 1].top;
            return this;
        }

        /// <summary>Tambahkan peti tarik (dipakai pustaka bawaan).</summary>
        public ChunkDefinition WithCrates(params Vector2[] crates)
        {
            crateSpawns = new List<Vector2>(crates);
            return this;
        }

        /// <summary>Tandai sebagai chunk ayun wajib (dipakai pustaka bawaan).</summary>
        public ChunkDefinition MarkSwingOnly()
        {
            swingOnly = true;
            return this;
        }
    }
}
