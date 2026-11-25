using UnityEngine;

namespace Untai.Core
{
    /// <summary>
    /// Pesan antar-scene dari Title Screen ke Untai_Main: seed yang diminta pemain.
    /// Tidak ada permintaan berarti GameDirector memakai pengaturannya sendiri
    /// (randomStartSeed atau startSeed).
    /// </summary>
    public static class GameLaunch
    {
        static bool s_HasSeed;
        static int s_Seed;

        /// <summary>Minta level pertama dibangun dengan seed tertentu.</summary>
        public static void RequestSeed(int seed)
        {
            s_HasSeed = true;
            s_Seed = seed;
        }

        /// <summary>Hapus permintaan sehingga GameDirector memakai pengaturannya sendiri.</summary>
        public static void Clear() => s_HasSeed = false;

        /// <summary>Ambil seed yang diminta (sekali pakai).</summary>
        public static bool TryTakeSeed(out int seed)
        {
            seed = s_Seed;
            if (!s_HasSeed) return false;
            s_HasSeed = false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_HasSeed = false;
    }
}
