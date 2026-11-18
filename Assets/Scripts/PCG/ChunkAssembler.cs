using System.Collections.Generic;
using UnityEngine;
using Untai.Data;

namespace Untai.PCG
{
    /// <summary>
    /// Perakit chunk berbasis seed (logika murni, tanpa MonoBehaviour).
    /// Setiap pemilihan memakai Random.InitState(seed turunan dari seed level, indeks chunk,
    /// dan nomor percobaan) sehingga seed dan parameter yang sama selalu menghasilkan level sama.
    /// </summary>
    public class ChunkAssembler
    {
        public struct PickResult
        {
            public ChunkDefinition def;
            public int attempts;
            public int seed;
            public bool fallback;
            public string note;
        }

        public int maxAttempts = 12;
        public float maxStepUp = 1.5f;
        public float maxStepDown = 3.5f;

        readonly List<ChunkDefinition> _library;

        public ChunkAssembler(List<ChunkDefinition> library)
        {
            _library = library;
        }

        public IReadOnlyList<ChunkDefinition> Library => _library;

        /// <summary>Seed turunan deterministik (hash sederhana).</summary>
        public static int DeriveSeed(int levelSeed, int chunkIndex, int attempt)
        {
            unchecked
            {
                int h = levelSeed * 73856093;
                h ^= (chunkIndex + 1) * 19349663;
                h ^= (attempt + 1) * 83492791;
                h ^= h >> 13;
                return h * 1274126177;
            }
        }

        /// <summary>Sambungan cocok jika beda tinggi masuk terhadap keluar sebelumnya masih bisa dilompati.</summary>
        public bool ConnectionOk(float prevExit, ChunkDefinition c)
        {
            float d = c.entryHeight - prevExit;
            return d <= maxStepUp + 0.01f && d >= -maxStepDown - 0.01f;
        }

        /// <summary>Bobot chunk = bobot tingkat dari parameter; 0 jika celahnya melebihi batas parameter.</summary>
        public float Weight(ChunkDefinition c, ChunkDefinition prev, GameplayParameters p)
        {
            if (c.MaxGap > p.maxGapWidth + 0.01f) return 0f;
            float w = Mathf.Max(0f, p.TierWeight((int)c.tier));
            if (c == prev) w *= 0.25f; // kurangi pengulangan chunk yang sama berturut-turut
            return w;
        }

        ChunkDefinition PickWeighted(ChunkDefinition prev, GameplayParameters p)
        {
            float total = 0f;
            for (int i = 0; i < _library.Count; i++) total += Weight(_library[i], prev, p);
            if (total <= 0f) return null;
            float r = Random.value * total;
            for (int i = 0; i < _library.Count; i++)
            {
                r -= Weight(_library[i], prev, p);
                if (r <= 0f) return _library[i];
            }
            return _library[_library.Count - 1];
        }

        /// <summary>
        /// Pilih chunk berikutnya. Kandidat ditolak jika sambungannya tidak cocok atau gagal
        /// validasi (bila validator diberikan); percobaan diulang dengan seed turunan
        /// sampai maxAttempts, lalu jatuh ke chunk paling aman.
        /// </summary>
        public PickResult Pick(int levelSeed, int index, ChunkDefinition prev, float prevExit,
            GameplayParameters p, System.Func<ChunkDefinition, string> validate = null)
        {
            string lastReason = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int seed = DeriveSeed(levelSeed, index, attempt);
                Random.InitState(seed);
                var c = PickWeighted(prev, p);
                if (c == null) { lastReason = "tak ada kandidat berbobot"; break; }
                if (!ConnectionOk(prevExit, c)) { lastReason = c.id + ": sambungan tidak cocok"; continue; }
                if (validate != null)
                {
                    string reason = validate(c);
                    if (reason != null) { lastReason = c.id + ": " + reason; continue; }
                }
                return new PickResult
                {
                    def = c, attempts = attempt + 1, seed = seed,
                    note = attempt == 0 ? "ok" : "ditolak " + attempt + "x, terakhir " + lastReason
                };
            }

            // Fallback: chunk tersambung dengan celah terkecil (paling aman).
            ChunkDefinition safest = null;
            for (int i = 0; i < _library.Count; i++)
            {
                var c = _library[i];
                if (!ConnectionOk(prevExit, c)) continue;
                if (validate != null && validate(c) != null) continue;
                if (safest == null || c.MaxGap < safest.MaxGap) safest = c;
            }
            if (safest == null) safest = _library[0];
            return new PickResult
            {
                def = safest, attempts = maxAttempts, seed = DeriveSeed(levelSeed, index, maxAttempts),
                fallback = true, note = "fallback (" + lastReason + ")"
            };
        }
    }
}
