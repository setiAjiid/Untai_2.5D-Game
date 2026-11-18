using UnityEngine;
using Untai.Core;
using Untai.Data;
using Untai.Player;

namespace Untai.Level
{
    /// <summary>
    /// Kill zone: pemain yang jatuh di bawah killHeight mati (penyebab: jatuh) lalu respawn
    /// di checkpoint terakhir. Memakai cek ketinggian agar berlaku sepanjang level hasil PCG.
    /// Komponen ini juga bisa dipasang pada collider trigger (misal lubang jebakan).
    /// </summary>
    public class KillZone : MonoBehaviour
    {
        public float killHeight = -8f;
        public PlayerHealth player;
        public bool useHeightCheck = true;

        /// <summary>Ambil KillZone di scene, atau buat dari prefab / objek kosong.</summary>
        public static KillZone FindOrCreate()
        {
            var existing = FindAnyObjectByType<KillZone>();
            if (existing != null) return existing;
            var prefab = PrefabLibrary.Get(l => l.killZone);
            if (prefab != null)
            {
                var k = Prims.Spawn(prefab, Vector3.zero, null).GetComponent<KillZone>();
                if (k != null) return k;
            }
            return new GameObject("KillZone").AddComponent<KillZone>();
        }

        void Update()
        {
            if (useHeightCheck && player != null && player.transform.position.y < killHeight)
                player.Die(DeathCause.Fall);
        }

        void OnTriggerEnter(Collider other)
        {
            var hp = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<PlayerHealth>() : null;
            if (hp != null) hp.Die(DeathCause.Fall);
        }
    }
}
