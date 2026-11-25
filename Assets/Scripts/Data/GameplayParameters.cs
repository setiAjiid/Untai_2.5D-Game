using System;
using UnityEngine;

namespace Untai.Data
{
    /// <summary>
    /// Sumber tunggal parameter gameplay. Semua skrip (pemain, benang, PCG, AI) membaca dari
    /// GameplayParameters.Active, yaitu salinan runtime dari aset di Resources/Untai.
    /// DDA mengubah salinan runtime ini sehingga aset asli tidak ikut berubah.
    /// </summary>
    [CreateAssetMenu(menuName = "Untai/Gameplay Parameters", fileName = "GameplayParameters")]
    public class GameplayParameters : ScriptableObject
    {
        public const string ResourcePath = "Untai/GameplayParameters";

        [Header("Pemain")]
        public float moveSpeed = 6f;
        public float jumpVelocity = 11f;
        public float gravityMultiplier = 2.5f;

        [Header("Benang")]
        [Tooltip("Panjang benang dari checkpoint (satuan dunia, diukur sepanjang jalur pemain). " +
                 "Satu chunk rata-rata ~14 unit x PathFactor 1.6 = ~22 unit jalur, jadi 36 berarti ~60% terpakai.")]
        public float threadLength = 36f;
        [Tooltip("Jangkauan maksimum untuk mengait jangkar.")]
        public float anchorRange = 7f;
        [Tooltip("Tambahan panjang benang maksimum dari satu pickup gulungan.")]
        public float pickupThreadBonus = 4f;
        [Tooltip("Peluang muncul pickup per chunk: mudah, sedang, sulit (maksimal satu per chunk).")]
        [Range(0f, 1f)] public float pickupChanceEasy = 0.15f;
        [Range(0f, 1f)] public float pickupChanceMedium = 0.35f;
        [Range(0f, 1f)] public float pickupChanceHard = 0.6f;

        [Header("PCG")]
        [Tooltip("Lebar celah maksimum yang boleh muncul pada chunk terpilih.")]
        public float maxGapWidth = 4.5f;
        [Range(0f, 1f)] public float trapDensity = 0.3f;
        [Tooltip("Jumlah enemy maksimum per chunk (minimal 1 setelah chunk pengantar).")]
        [Range(1, 3)] public int enemyCount = 1;
        [Tooltip("Checkpoint dipasang setiap N chunk (dipaksa lebih rapat jika benang tidak cukup).")]
        [Range(1, 4)] public int checkpointInterval = 2;
        [Tooltip("Bobot pemilihan chunk: mudah, sedang, sulit.")]
        public float easyWeight = 1f;
        public float mediumWeight = 0.6f;
        public float hardWeight = 0.2f;

        [Header("Game AI")]
        [Range(0f, 1f)] public float aggression = 0.4f;

        // ---------- Turunan (dipakai validator dan AI) ----------

        /// <summary>
        /// Perkiraan rasio panjang jalur pemain (lompat, panjat, ayun) terhadap jarak horizontal.
        /// PCG memakai faktor ini agar jarak antar checkpoint muat dalam benang berbasis jalur.
        /// </summary>
        public const float PathFactor = 1.6f;

        public float Gravity => Mathf.Abs(Physics.gravity.y) * gravityMultiplier;
        /// <summary>Tinggi lompat maksimum (v^2 / 2g).</summary>
        public float JumpHeight => jumpVelocity * jumpVelocity / (2f * Gravity);
        /// <summary>Jarak lompat horizontal di tanah datar (kecepatan x waktu di udara).</summary>
        public float JumpDistance => moveSpeed * (2f * jumpVelocity / Gravity);

        /// <summary>Peluang pickup benang untuk tingkat chunk (0 mudah, 1 sedang, 2 sulit).</summary>
        public float PickupChance(int tier)
        {
            switch (tier)
            {
                case 0: return pickupChanceEasy;
                case 1: return pickupChanceMedium;
                default: return pickupChanceHard;
            }
        }

        public float TierWeight(int tier)
        {
            switch (tier)
            {
                case 0: return easyWeight;
                case 1: return mediumWeight;
                default: return hardWeight;
            }
        }

        public void CopyFrom(GameplayParameters o)
        {
            moveSpeed = o.moveSpeed; jumpVelocity = o.jumpVelocity; gravityMultiplier = o.gravityMultiplier;
            threadLength = o.threadLength; anchorRange = o.anchorRange;
            pickupThreadBonus = o.pickupThreadBonus; pickupChanceEasy = o.pickupChanceEasy;
            pickupChanceMedium = o.pickupChanceMedium; pickupChanceHard = o.pickupChanceHard;
            maxGapWidth = o.maxGapWidth; trapDensity = o.trapDensity; enemyCount = o.enemyCount;
            checkpointInterval = o.checkpointInterval;
            easyWeight = o.easyWeight; mediumWeight = o.mediumWeight; hardWeight = o.hardWeight;
            aggression = o.aggression;
        }

        // ---------- Instans runtime ----------

        static GameplayParameters s_Active;
        static GameplayParameters s_Baseline;

        /// <summary>Dipanggil setiap kali parameter aktif berubah (misal oleh DDA).</summary>
        public static event Action Changed;

        public static GameplayParameters Active
        {
            get
            {
                if (s_Active == null) s_Active = CreateRuntimeCopy();
                return s_Active;
            }
        }

        /// <summary>Nilai awal dari aset (tidak diubah DDA), dipakai untuk reset.</summary>
        public static GameplayParameters Baseline
        {
            get
            {
                if (s_Baseline == null) s_Baseline = CreateRuntimeCopy();
                return s_Baseline;
            }
        }

        static GameplayParameters s_Source;

        /// <summary>
        /// Pakai aset tertentu (misal yang diisi di Inspector GameDirector) sebagai sumber salinan runtime.
        /// Dipanggil sebelum ada skrip yang membaca Active.
        /// </summary>
        public static void UseSource(GameplayParameters asset)
        {
            if (asset == null) return;
            s_Source = asset;
            s_Active = null;
            s_Baseline = null;
        }

        static GameplayParameters CreateRuntimeCopy()
        {
            var asset = s_Source != null ? s_Source : Resources.Load<GameplayParameters>(ResourcePath);
            var copy = asset != null ? Instantiate(asset) : CreateInstance<GameplayParameters>();
            copy.name = "GameplayParameters (Runtime)";
            copy.hideFlags = HideFlags.DontSave;
            return copy;
        }

        public static void NotifyChanged() => Changed?.Invoke();

        public static void ResetToBaseline()
        {
            Active.CopyFrom(Baseline);
            NotifyChanged();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Active = null;
            s_Baseline = null;
            s_Source = null;
            Changed = null;
        }
    }
}
