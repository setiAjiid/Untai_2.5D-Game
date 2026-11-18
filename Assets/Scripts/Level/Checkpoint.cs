using UnityEngine;
using Untai.Core;
using Untai.Data;
using Untai.Thread;

namespace Untai.Level
{
    /// <summary>
    /// Checkpoint berupa tiang gulungan benang. Saat disentuh, titik tambat benang
    /// pindah ke sini dan benang pulih sepanjang threadLength (diisi oleh PCG).
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        public float threadLength = 20f;
        [Tooltip("Tinggi titik tambat benang di atas dasar checkpoint.")]
        public float tetherHeight = 1f;
        [Tooltip("Renderer gulungan yang berganti warna saat aktif.")]
        public Renderer spool;
        public Material idleMaterial;
        public Material activeMaterial;

        public bool IsActive { get; private set; }
        public static Checkpoint Active { get; private set; }

        public static readonly Color DefaultIdleColor = new Color(0.55f, 0.55f, 0.6f);
        public static readonly Color DefaultActiveColor = new Color(0.3f, 0.95f, 0.45f);
        public static readonly Color PoleColor = new Color(0.4f, 0.3f, 0.2f);

        public Vector3 TetherPoint => transform.position + Vector3.up * tetherHeight;

        /// <summary>Buat checkpoint dari prefab PrefabLibrary, atau primitif bila prefab kosong.</summary>
        public static Checkpoint Create(Vector3 groundPos, float threadLength, Transform parent)
        {
            Checkpoint cp = null;
            var prefab = PrefabLibrary.Get(l => l.checkpoint);
            if (prefab != null) cp = Prims.Spawn(prefab, groundPos, parent).GetComponent<Checkpoint>();
            if (cp == null) cp = CreatePrimitive(groundPos, parent);
            cp.threadLength = threadLength;
            return cp;
        }

        /// <summary>Versi primitif dari kode (fallback, juga dipakai generator prefab).</summary>
        public static Checkpoint CreatePrimitive(Vector3 groundPos, Transform parent)
        {
            var root = new GameObject("Checkpoint");
            root.transform.SetParent(parent, false);
            root.transform.position = groundPos;
            Prims.Create(PrimitiveType.Cylinder, "Pole", groundPos + Vector3.up * 0.75f,
                new Vector3(0.15f, 0.75f, 0.15f), PoleColor, root.transform, false);
            var spoolGo = Prims.Create(PrimitiveType.Cylinder, "Spool", groundPos + Vector3.up * 1.6f,
                new Vector3(0.5f, 0.2f, 0.5f), DefaultIdleColor, root.transform, false);
            spoolGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 1f;
            trigger.size = new Vector3(1.2f, 2.5f, 3f);

            var cp = root.AddComponent<Checkpoint>();
            cp.spool = spoolGo.GetComponent<Renderer>();
            return cp;
        }

        void OnTriggerEnter(Collider other)
        {
            var thread = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<ThreadController>() : null;
            if (thread != null) Activate(thread);
        }

        public void Activate(ThreadController thread, bool silent = false)
        {
            if (IsActive) return;
            float ratioBefore = thread.RemainingRatio;
            if (Active != null && Active != this) Active.SetActiveVisual(false);
            Active = this;
            SetActiveVisual(true);
            thread.SetTether(TetherPoint, threadLength);
            if (!silent) GameEvents.RaiseCheckpointReached(transform.position, ratioBefore);
        }

        void SetActiveVisual(bool on)
        {
            IsActive = on;
            if (spool != null)
                spool.sharedMaterial = on ? Prims.MatOr(activeMaterial, DefaultActiveColor)
                                          : Prims.MatOr(idleMaterial, DefaultIdleColor);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
