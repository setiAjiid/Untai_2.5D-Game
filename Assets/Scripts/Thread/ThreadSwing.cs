using UnityEngine;
using Untai.Core;
using Untai.Data;
using Untai.Player;

namespace Untai.Thread
{
    /// <summary>
    /// Ayunan jangkar versi sederhana (pendulum dengan batasan jarak):
    /// - Tahan Shift/J di dekat jangkar untuk mengaitkan benang.
    /// - Selama terkait, jarak ke jangkar dibatasi panjang tali; gravitasi membuat pemain berayun.
    /// - A/D menambah dorongan tangensial, W/S menggulung/mengulur tali.
    /// - Lepas tombol atau tekan Space untuk melepas (Space memberi dorongan tambahan).
    /// Panjang tali tidak boleh melebihi jangkauan jangkar maupun sisa benang.
    /// </summary>
    [DefaultExecutionOrder(40)] // setelah PlayerMotor, sebelum ThreadController
    [RequireComponent(typeof(PlayerMotor), typeof(ThreadController))]
    public class ThreadSwing : MonoBehaviour
    {
        /// <summary>Jangkauan jangkar dibaca dari GameplayParameters.Active.</summary>
        public float AnchorRange => GameplayParameters.Active.anchorRange;
        public float swingForce = 16f;
        public float reelSpeed = 4f;
        public float minRope = 1.5f;
        public float releaseBoost = 1.15f;
        public float releaseUpBoost = 3.5f;

        public bool IsSwinging => _current != null;
        public Anchor Current => _current;
        public Anchor Candidate { get; private set; }

        PlayerMotor _motor;
        ThreadController _thread;
        Anchor _current;
        float _rope;
        bool _prevHeld;
        bool _jumpReleaseQueued;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _thread = GetComponent<ThreadController>();
        }

        void Start()
        {
            // Di Start agar urutan komponen pada prefab tidak berpengaruh.
            var hp = GetComponent<PlayerHealth>();
            if (hp != null) hp.BeforeRespawn += () => Release(false);
        }

        void Update()
        {
            if (!IsSwinging) UpdateCandidate();

            bool held = _motor.InputEnabled && InputReader.ThreadHeld;
            if (held && !_prevHeld && !IsSwinging && Candidate != null) Attach(Candidate);
            else if (!held && IsSwinging) Release(false);
            if (IsSwinging && InputReader.JumpPressed) _jumpReleaseQueued = true;
            _prevHeld = held;
        }

        /// <summary>Cari jangkar terdekat di atas pemain yang terjangkau dan cukup benangnya.</summary>
        void UpdateCandidate()
        {
            Anchor best = null;
            float bestDist = float.MaxValue;
            Vector3 p = transform.position;
            float maxReach = Mathf.Min(AnchorRange, _thread.Remaining);
            var all = Anchor.All;
            for (int i = 0; i < all.Count; i++)
            {
                Vector3 a = all[i].transform.position;
                if (a.y < p.y + 0.3f) continue;
                float d = Vector2.Distance(a, p);
                if (d <= maxReach && d < bestDist)
                {
                    bestDist = d;
                    best = all[i];
                }
            }
            if (Candidate != best)
            {
                if (Candidate != null) Candidate.SetHighlight(false);
                if (best != null) best.SetHighlight(true);
                Candidate = best;
            }
        }

        void Attach(Anchor anchor)
        {
            _current = anchor;
            _rope = Mathf.Max(minRope, Vector2.Distance(anchor.transform.position, transform.position));
            _thread.SwingAnchor = anchor.transform;
            _motor.ExternalControl = true;
        }

        public void Release(bool withBoost)
        {
            if (_current == null) return;
            _current = null;
            _thread.SwingAnchor = null;
            _motor.ExternalControl = false;
            if (withBoost)
            {
                Vector3 v = _motor.Body.linearVelocity * releaseBoost;
                v.y = Mathf.Max(v.y, 0f) + releaseUpBoost;
                _motor.Body.linearVelocity = v;
            }
        }

        void FixedUpdate()
        {
            if (_current == null) return;
            if (_jumpReleaseQueued)
            {
                _jumpReleaseQueued = false;
                Release(true);
                return;
            }

            float dt = Time.fixedDeltaTime;
            var body = _motor.Body;
            Vector3 a = _current.transform.position; a.z = 0f;
            Vector3 p = body.position;
            Vector3 v = body.linearVelocity;

            // Gulung/ulur tali, dibatasi jangkauan jangkar.
            _rope = Mathf.Clamp(_rope - InputReader.Vertical * reelSpeed * dt, minRope, AnchorRange);

            Vector3 d = p - a;
            float dist = d.magnitude;
            if (dist < 0.001f) return;
            Vector3 n = d / dist; // arah radial (jangkar -> pemain)

            // Dorongan tangensial: tegak lurus tali, searah input kiri/kanan.
            Vector3 tangent = new Vector3(-n.y, n.x, 0f);
            v += tangent * (InputReader.Horizontal * swingForce * dt);

            // Batasan tali: jika lebih jauh dari panjang tali, tarik kembali ke lingkaran
            // dan buang komponen kecepatan yang menjauhi jangkar (inilah yang membuat ayunan).
            if (dist > _rope)
            {
                body.position = a + n * _rope;
                float radial = Vector3.Dot(v, n);
                if (radial > 0f) v -= n * radial;
            }
            v.z = 0f;
            body.linearVelocity = v;
        }
    }
}
