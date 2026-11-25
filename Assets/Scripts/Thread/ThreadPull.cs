using UnityEngine;
using Untai.Core;
using Untai.Level;
using Untai.Player;

namespace Untai.Thread
{
    /// <summary>
    /// Mengait dan menarik peti (PullCrate) dengan benang:
    /// - Tekan Shift/J dekat peti (bila tidak ada jangkar yang bisa dikait) untuk mengaitkan benang.
    /// - Selama terkait, tahan S (gulung) untuk menarik peti mendekat di sepanjang tanah.
    ///   Kecepatan tarik dibatasi sisa benang: makin sedikit benang, makin lemah tarikannya.
    /// - Lepas Shift/J untuk melepas peti.
    /// </summary>
    [DefaultExecutionOrder(45)] // setelah ThreadSwing (40) agar kandidat jangkar sudah diperbarui
    [RequireComponent(typeof(PlayerMotor), typeof(ThreadController), typeof(ThreadSwing))]
    public class ThreadPull : MonoBehaviour
    {
        public float pullRange = 7f;
        [Tooltip("Kecepatan tarik saat benang penuh dan hampir habis.")]
        public float maxPullSpeed = 3.5f;
        public float minPullSpeed = 1.2f;
        [Tooltip("Peti berhenti pada jarak horizontal ini dari pemain.")]
        public float stopDistance = 1.6f;
        [Tooltip("Selisih tinggi maksimum pemain dan peti untuk bisa dikait.")]
        public float maxHeightDiff = 3f;

        public PullCrate Candidate { get; private set; }
        public PullCrate Hooked { get; private set; }
        public bool IsPulling { get; private set; }

        PlayerMotor _motor;
        ThreadController _thread;
        ThreadSwing _swing;
        bool _prevHeld;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _thread = GetComponent<ThreadController>();
            _swing = GetComponent<ThreadSwing>();
        }

        void Start()
        {
            var hp = GetComponent<PlayerHealth>();
            if (hp != null) hp.BeforeRespawn += Unhook;
        }

        void Update()
        {
            if (Hooked == null) UpdateCandidate();

            bool held = _motor.InputEnabled && InputReader.ThreadHeld;
            // Jangkar diprioritaskan: peti hanya dikait bila tidak sedang/akan mengayun.
            if (held && !_prevHeld && Hooked == null && !_swing.IsSwinging && _swing.Candidate == null && Candidate != null)
                Hook(Candidate);
            else if (Hooked != null && (!held || _swing.IsSwinging)) Unhook();
            _prevHeld = held;
        }

        void UpdateCandidate()
        {
            PullCrate best = null;
            float bestSqr = float.MaxValue;
            float reach = Mathf.Min(pullRange, _thread.Remaining);
            float reachSqr = reach * reach;
            Vector3 p = transform.position;
            var all = PullCrate.All;
            for (int i = 0; i < all.Count; i++)
            {
                Vector3 d = all[i].transform.position - p;
                d.z = 0f;
                if (Mathf.Abs(d.y) > maxHeightDiff) continue;
                float sqr = d.sqrMagnitude;
                if (sqr <= reachSqr && sqr < bestSqr) { bestSqr = sqr; best = all[i]; }
            }
            if (Candidate != best)
            {
                if (Candidate != null) Candidate.SetHighlight(false);
                if (best != null) best.SetHighlight(true);
                Candidate = best;
            }
        }

        void Hook(PullCrate crate)
        {
            Hooked = crate;
            _thread.SwingAnchor = crate.transform; // renderer menggambar benang ke peti
        }

        public void Unhook()
        {
            if (Hooked == null) return;
            if (_thread.SwingAnchor == Hooked.transform) _thread.SwingAnchor = null;
            Hooked = null;
            IsPulling = false;
        }

        void FixedUpdate()
        {
            IsPulling = false;
            if (Hooked == null) return;
            Vector3 d = transform.position - Hooked.transform.position;
            // Pemain terlalu jauh dari peti (melebihi jangkauan): benang terlepas.
            if (new Vector2(d.x, d.y).magnitude > pullRange + 1.5f) { Unhook(); return; }

            // Tahan S untuk menggulung: peti bergerak ke arah pemain sepanjang tanah.
            if (InputReader.Vertical < -0.5f && Mathf.Abs(d.x) > stopDistance)
            {
                float speed = Mathf.Lerp(minPullSpeed, maxPullSpeed, _thread.RemainingRatio);
                Hooked.SetPull(Mathf.Sign(d.x) * speed);
                IsPulling = true;
            }
        }
    }
}
