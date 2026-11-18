using UnityEngine;
using Untai.Core;
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
        public bool IsActive { get; private set; }
        public static Checkpoint Active { get; private set; }

        static readonly Color IdleColor = new Color(0.55f, 0.55f, 0.6f);
        static readonly Color ActiveColor = new Color(0.3f, 0.95f, 0.45f);
        Renderer _spool;

        public Vector3 TetherPoint => transform.position + Vector3.up * 1.0f;

        public static Checkpoint Create(Vector3 groundPos, float threadLength, Transform parent)
        {
            var root = new GameObject("Checkpoint");
            root.transform.SetParent(parent, false);
            root.transform.position = groundPos;
            Prims.Create(PrimitiveType.Cylinder, "Pole", groundPos + Vector3.up * 0.75f,
                new Vector3(0.15f, 0.75f, 0.15f), new Color(0.4f, 0.3f, 0.2f), root.transform, false);
            var spool = Prims.Create(PrimitiveType.Cylinder, "Spool", groundPos + Vector3.up * 1.6f,
                new Vector3(0.5f, 0.2f, 0.5f), IdleColor, root.transform, false);
            spool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = Vector3.up * 1f;
            trigger.size = new Vector3(1.2f, 2.5f, 3f);

            var cp = root.AddComponent<Checkpoint>();
            cp.threadLength = threadLength;
            cp._spool = spool.GetComponent<Renderer>();
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
            if (_spool != null) _spool.sharedMaterial = Prims.Mat(on ? ActiveColor : IdleColor);
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
