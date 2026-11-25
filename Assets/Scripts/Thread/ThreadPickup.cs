using UnityEngine;
using Untai.Core;
using Untai.Data;

namespace Untai.Thread
{
    /// <summary>
    /// Pickup gulungan benang: bola kecil (trigger) yang menambah panjang benang maksimum
    /// sebesar GameplayParameters.pickupThreadBonus, lalu hilang. Dipasang ChunkBuilder (ber-seed).
    /// </summary>
    public class ThreadPickup : MonoBehaviour
    {
        public static readonly Color DefaultColor = new Color(1f, 0.75f, 0.95f);

        [Tooltip("Kecepatan putar (derajat/detik) dan amplitudo naik-turun untuk penanda visual.")]
        public float spinSpeed = 90f;
        public float bobHeight = 0.15f;

        Vector3 _basePos;
        bool _taken;

        /// <summary>Buat pickup dari prefab PrefabLibrary, atau primitif bila prefab kosong.</summary>
        public static ThreadPickup Create(Vector3 position, Transform parent)
        {
            var prefab = PrefabLibrary.Get(l => l.threadPickup);
            if (prefab != null)
            {
                var p = Prims.Spawn(prefab, position, parent).GetComponent<ThreadPickup>();
                if (p != null) return p;
            }
            return CreatePrimitive(position, parent);
        }

        /// <summary>Versi primitif dari kode (fallback, juga dipakai generator prefab).</summary>
        public static ThreadPickup CreatePrimitive(Vector3 position, Transform parent)
        {
            var go = Prims.Create(PrimitiveType.Sphere, "ThreadPickup", position, Vector3.one * 0.45f, DefaultColor, parent);
            go.GetComponent<Collider>().isTrigger = true;
            // Cincin tipis sebagai tanda "gulungan".
            var ring = Prims.Create(PrimitiveType.Cylinder, "Ring", position, new Vector3(0.7f, 0.03f, 0.7f),
                Color.white, go.transform, false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go.AddComponent<ThreadPickup>();
        }

        void Start() => _basePos = transform.position;

        void Update()
        {
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            transform.position = _basePos + Vector3.up * (Mathf.Sin(Time.time * 3f) * bobHeight);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            var thread = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<ThreadController>() : null;
            if (thread == null) return;
            _taken = true;
            float bonus = GameplayParameters.Active.pickupThreadBonus;
            thread.AddLength(bonus);
            if (GameDirector.Instance != null) GameDirector.Instance.Flash("+" + bonus.ToString("0") + " benang", 1.2f);
            Destroy(gameObject);
        }
    }
}
