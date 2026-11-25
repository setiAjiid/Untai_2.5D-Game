using System.Collections.Generic;
using UnityEngine;
using Untai.Core;
using Untai.Data;
using Untai.PCG;
using Untai.Thread;

namespace Untai.Level
{
    /// <summary>Data spawn enemy hasil pembangunan chunk (posisi dan batas patroli platform).</summary>
    public struct EnemySpawnInfo
    {
        public Vector3 position;
        public float minX;
        public float maxX;
    }

    /// <summary>
    /// Membangun ChunkDefinition menjadi objek di dunia: platform, jangkar, jebakan, dan titik enemy
    /// diinstansiasi dari prefab PrefabLibrary (fallback primitif bila kosong). Skala dan posisi
    /// tetap ditentukan data chunk. Jebakan, enemy, dan pickup benang dipilih acak ber-seed sesuai parameter.
    /// </summary>
    public static class ChunkBuilder
    {
        // Ukuran dasar dibaca dari PrefabLibrary (bisa diubah di Inspector).
        public static float PillarBottom => PrefabLibrary.PillarBottom;
        public static float Depth => PrefabLibrary.PlatformDepth;

        static readonly List<int> s_Indices = new List<int>(8);

        public static Transform Build(ChunkDefinition def, float offsetX, Transform parent, float trapDensity,
            int enemyCount, float pickupChance, int seed, List<EnemySpawnInfo> enemyOut, out int trapCount,
            out bool pickupPlaced)
        {
            Random.InitState(seed);
            var root = new GameObject("Chunk " + def.id).transform;
            root.SetParent(parent, false);
            var platformPrefab = PrefabLibrary.Get(l => l.platform);
            float bottom = PillarBottom, depth = Depth;

            // Platform berupa pilar dari dasar sampai permukaan atas.
            for (int i = 0; i < def.platforms.Count; i++)
            {
                var p = def.platforms[i];
                float h = p.top - bottom;
                var center = new Vector3(offsetX + p.x + p.width * 0.5f, p.top - h * 0.5f, 0f);
                var size = new Vector3(p.width, h, depth);
                GameObject go = platformPrefab != null
                    ? Prims.Spawn(platformPrefab, center, root)
                    : Prims.Box("Platform", center, size, PlatformVisual.DefaultTierColors[(int)def.tier], root);
                go.name = "Platform " + i;
                go.transform.localScale = size;
                var visual = go.GetComponent<PlatformVisual>();
                if (visual != null) visual.SetTier((int)def.tier);
            }

            for (int i = 0; i < def.anchors.Count; i++)
                Anchor.Create(new Vector3(offsetX + def.anchors[i].x, def.anchors[i].y, 0f), root);

            // Peti tarik: posisi tetap dari data chunk (tanpa acak, jadi urutan seed tidak berubah).
            for (int i = 0; i < def.crateSpawns.Count; i++)
                PullCrate.Create(new Vector3(offsetX + def.crateSpawns[i].x, def.crateSpawns[i].y, 0f), root);

            // Jebakan: setiap kandidat muncul dengan peluang trapDensity.
            trapCount = 0;
            for (int i = 0; i < def.traps.Count; i++)
            {
                if (Random.value >= trapDensity) continue;
                Trap.Create(new Vector3(offsetX + def.traps[i].x, def.traps[i].y, 0f), root);
                trapCount++;
            }

            // Enemy: acak urutan titik spawn (Fisher-Yates ber-seed) lalu ambil sebanyak enemyCount.
            s_Indices.Clear();
            for (int i = 0; i < def.enemySpawns.Count; i++) s_Indices.Add(i);
            for (int i = s_Indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (s_Indices[i], s_Indices[j]) = (s_Indices[j], s_Indices[i]);
            }
            int count = Mathf.Min(enemyCount, s_Indices.Count);
            for (int k = 0; k < count; k++)
            {
                var plat = def.platforms[def.enemySpawns[s_Indices[k]].platform];
                float left = offsetX + plat.x, right = offsetX + plat.Right;
                enemyOut.Add(new EnemySpawnInfo
                {
                    position = new Vector3(Mathf.Lerp(left, right, 0.6f), plat.top + 1f, 0f),
                    minX = left + 0.5f,
                    maxX = right - 0.5f
                });
            }

            // Pickup benang: 0 atau 1 per chunk. Diundi paling akhir agar urutan acak jebakan/enemy
            // tidak berubah (seed yang sama tetap menghasilkan level yang sama).
            pickupPlaced = false;
            if (pickupChance > 0f && Random.value < pickupChance && TryRiskySpot(def, out Vector2 spot))
            {
                ThreadPickup.Create(new Vector3(offsetX + spot.x, spot.y, 0f), root);
                pickupPlaced = true;
            }
            return root;
        }

        /// <summary>
        /// Titik pickup di rute berisiko (koordinat lokal chunk), bukan di jalur aman:
        /// - Chunk berjangkar: dekat titik terendah busur ayunan di bawah jangkar.
        /// - Chunk bercelah: di atas celah terlebar, setinggi puncak lompatan.
        /// Chunk tanpa jangkar dan celah tidak mendapat pickup.
        /// </summary>
        static bool TryRiskySpot(ChunkDefinition def, out Vector2 spot)
        {
            spot = Vector2.zero;
            if (def.anchors.Count > 0)
            {
                Vector2 a = def.anchors[Random.Range(0, def.anchors.Count)];
                // Perkirakan panjang tali dari tepi platform terdekat di kiri jangkar.
                float rope = 4.5f;
                for (int i = def.platforms.Count - 1; i >= 0; i--)
                {
                    var pl = def.platforms[i];
                    if (pl.Right > a.x + 0.5f) continue;
                    rope = Vector2.Distance(a, new Vector2(pl.Right, pl.top + 1f));
                    break;
                }
                spot = new Vector2(a.x, a.y - rope + 0.3f);
                return true;
            }

            int best = -1;
            float bestGap = 0.9f; // celah sangat kecil masih termasuk jalur aman
            for (int i = 1; i < def.platforms.Count; i++)
            {
                float gap = def.platforms[i].x - def.platforms[i - 1].Right;
                if (gap > bestGap) { bestGap = gap; best = i; }
            }
            if (best < 0) return false;
            var left = def.platforms[best - 1];
            var right = def.platforms[best];
            spot = new Vector2((left.Right + right.x) * 0.5f, Mathf.Max(left.top, right.top) + 3f);
            return true;
        }
    }
}
