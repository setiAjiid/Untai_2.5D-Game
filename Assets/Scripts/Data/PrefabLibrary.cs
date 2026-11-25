using UnityEngine;

namespace Untai.Data
{
    /// <summary>
    /// Daftar prefab yang dipakai builder runtime (LevelBuilder, ChunkBuilder, EnemySpawner, GameDirector).
    /// Ganti model dummy cukup dengan mengedit prefab yang dirujuk di sini.
    /// Jika aset ini atau salah satu slot kosong, builder membuat primitif dari kode (fallback)
    /// sehingga game tetap berjalan.
    /// </summary>
    [CreateAssetMenu(menuName = "Untai/Prefab Library", fileName = "PrefabLibrary")]
    public class PrefabLibrary : ScriptableObject
    {
        public const string ResourcePath = "Untai/PrefabLibrary";

        [Header("Karakter")]
        public GameObject player;
        public GameObject enemy;

        [Header("Level")]
        [Tooltip("Kubus 1x1x1; skala dan posisi ditentukan data chunk.")]
        public GameObject platform;
        public GameObject checkpoint;
        public GameObject goal;
        public GameObject killZone;
        public GameObject trap;
        [Tooltip("Peti yang bisa ditarik benang dan dijadikan pijakan.")]
        public GameObject pullCrate;

        [Header("Benang")]
        public GameObject anchor;
        [Tooltip("Pickup gulungan benang (menambah panjang benang maksimum).")]
        public GameObject threadPickup;

        [Header("Ukuran dasar platform")]
        [Tooltip("Ketebalan platform pada sumbu Z.")]
        public float platformDepth = 3f;
        [Tooltip("Dasar pilar platform (y bawah).")]
        public float pillarBottom = -5f;

        static PrefabLibrary s_Active;
        static bool s_Searched;

        /// <summary>Pustaka aktif: yang diset GameDirector, atau dimuat dari Resources. Bisa null.</summary>
        public static PrefabLibrary Active
        {
            get
            {
                if (s_Active == null && !s_Searched)
                {
                    s_Searched = true;
                    s_Active = Resources.Load<PrefabLibrary>(ResourcePath);
                }
                return s_Active;
            }
        }

        public static void SetActive(PrefabLibrary library)
        {
            if (library != null) s_Active = library;
        }

        /// <summary>Ambil prefab lewat selector; null bila pustaka atau slot kosong.</summary>
        public static GameObject Get(System.Func<PrefabLibrary, GameObject> selector)
        {
            var lib = Active;
            return lib != null ? selector(lib) : null;
        }

        public static float PlatformDepth => Active != null ? Active.platformDepth : 3f;
        public static float PillarBottom => Active != null ? Active.pillarBottom : -5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Active = null;
            s_Searched = false;
        }
    }
}
