using UnityEngine;
using Untai.Data;
using Untai.Player;
using Untai.Thread;

namespace Untai.AI
{
    public enum PartnerAction { Follow, Wait }

    /// <summary>
    /// AI untuk karakter Tandem yang tidak dikendalikan pemain. Menggerakkan PlayerMotor yang sama
    /// lewat IMotorInput (tanpa teleport), sehingga partner tunduk pada fisika dan untai yang sama.
    /// Aksi: Follow (mengikuti sampai jarak nyaman) dan Wait (diam sebagai tumpuan untai).
    /// Saat menggantung, partner memanjat untai sendiri lalu melompat ke tepi platform.
    /// </summary>
    [DefaultExecutionOrder(-10)] // isi input sebelum PlayerMotor membacanya
    public class PartnerBrain : MonoBehaviour, IMotorInput
    {
        public static PartnerBrain Instance { get; private set; }

        public Tandem tandem;
        public TandemRope rope;

        [Header("Gerak")]
        public float comfortDistance = 2.5f;
        public float speedFactor = 1f;
        [Tooltip("Kecepatan memanjat untai saat menggantung (0..1 dari kecepatan panjat pemain).")]
        public float climbFactor = 0.6f;
        [Tooltip("Detik tanpa kemajuan sebelum partner dianggap terjebak.")]
        public float stuckTimeout = 8f;

        public PartnerAction Current { get; private set; } = PartnerAction.Follow;
        public readonly float[] Scores = new float[2];

        // ---- IMotorInput ----
        float _h;
        bool _jump;
        float _jumpHeldUntil;
        public float Horizontal => _h;
        public bool JumpPressed { get { bool j = _jump; _jump = false; return j; } }
        public bool JumpHeld => Time.time < _jumpHeldUntil;

        protected PlayerMotor Me => tandem.Partner;
        protected PlayerMotor Leader => tandem.Active;
        protected bool Ready => tandem != null && rope != null && tandem.characterA != null && tandem.characterB != null;

        float _progressX;
        float _progressTime;
        public int StuckCount { get; private set; }
        public bool IsBlocked { get; private set; }

        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        void Awake() => Instance = this;

        public void Bind(Tandem t, TandemRope r)
        {
            tandem = t;
            rope = r;
        }

        void Update()
        {
            if (!Ready) { _h = 0f; return; }
            ScoreActions();
            Act();
        }

        // ------------------------------------------------------------ keputusan

        /// <summary>Kondisi dasar: karakter aktif sedang bergantung pada untai (butuh tumpuan).</summary>
        protected bool LeaderOnRope => rope.Hooked != null || rope.Hanging == Leader;

        protected virtual void ScoreActions()
        {
            float dist = Vector2.Distance(Me.transform.position, Leader.transform.position);
            float slack = rope.MaxLength > 0f ? Mathf.Clamp01(dist / rope.MaxLength) : 0f;
            float follow = Mathf.Clamp01((dist - comfortDistance) / 4f) * 0.8f + 0.2f * slack;
            if (LeaderOnRope) follow *= 0.1f;
            float wait = LeaderOnRope ? 0.9f : dist <= comfortDistance ? 0.5f : 0.15f;
            Scores[0] = follow;
            Scores[1] = wait;
            Current = follow >= wait ? PartnerAction.Follow : PartnerAction.Wait;
        }

        // ------------------------------------------------------------ eksekusi

        void Act()
        {
            rope.PartnerReelRequest = false;
            rope.PartnerClimbRequest = 0f;

            // Menggantung: panjat untai, lalu lompat ke tepi saat sudah dekat karakter aktif.
            if (rope.Hanging == Me)
            {
                _h = Mathf.Sign(Leader.transform.position.x - Me.transform.position.x);
                rope.PartnerClimbRequest = climbFactor;
                if (Me.transform.position.y > Leader.transform.position.y - 1.4f) rope.PartnerHangJumpRequest = true;
                TrackProgress(true);
                return;
            }

            if (Current == PartnerAction.Wait) { _h = 0f; TrackProgress(false); return; }
            MoveToward(Leader.transform.position.x, comfortDistance, true);
        }

        /// <summary>Berjalan ke targetX sampai jarak tolerance; tangani dinding, tepi jurang, dan tebing.</summary>
        protected void MoveToward(float targetX, float tolerance, bool crossGaps)
        {
            Vector3 me = Me.transform.position;
            Vector3 leader = Leader.transform.position;
            float dx = targetX - me.x;
            if (Mathf.Abs(dx) <= tolerance) { _h = 0f; TrackProgress(false); return; }

            float dir = Mathf.Sign(dx);
            _h = dir * speedFactor;
            var p = GameplayParameters.Active;

            if (Me.IsGrounded)
            {
                // Dinding di depan: lompati bila cukup rendah, jika terlalu tinggi minta ditarik.
                if (StaticRay(me, new Vector3(dir, 0f, 0f), 0.9f, out _))
                {
                    bool lowWall = !StaticRay(me + Vector3.up * (p.JumpHeight * 0.85f), new Vector3(dir, 0f, 0f), 1.2f, out _);
                    if (lowWall) Jump();
                    else if (leader.y > me.y + 1.2f) rope.PartnerReelRequest = true;
                }

                // Tepi jurang di depan.
                if (!GroundBelow(me + new Vector3(dir * 0.7f, 0f, 0f), 3f))
                {
                    bool leaderAcross = crossGaps && Mathf.Sign(leader.x - me.x) == dir && Leader.IsGrounded;
                    if (!leaderAcross) _h = 0f;                         // jangan jatuh sendirian
                    else if (FindLanding(me, dir, p.JumpDistance * 0.8f)) Jump(); // celah kecil: lompat
                    // selain itu: jalan terus, menggantung pada karakter aktif lalu memanjat
                }

                // Karakter aktif lebih tinggi dan dekat: lompat naik.
                if (leader.y > me.y + 0.8f && Mathf.Abs(leader.x - me.x) < 3f) Jump();
            }

            // Tertahan di bawah tebing yang tidak bisa dilompati: tarik dengan untai.
            if (IsBlocked && leader.y > me.y + 1.2f) rope.PartnerReelRequest = true;
            TrackProgress(true);
        }

        void Jump()
        {
            if (!Me.IsGrounded) return;
            _jump = true;
            _jumpHeldUntil = Time.time + 0.3f;
        }

        /// <summary>Deteksi tidak ada kemajuan; terlalu lama = terjebak (respawn bersama).</summary>
        void TrackProgress(bool wantsToMove)
        {
            float x = Me.transform.position.x;
            if (!wantsToMove || Mathf.Abs(x - _progressX) > 0.3f)
            {
                _progressX = x;
                _progressTime = Time.time;
                IsBlocked = false;
                return;
            }
            float t = Time.time - _progressTime;
            IsBlocked = t > 1.5f;
            if (t > stuckTimeout)
            {
                StuckCount++;
                OnStuck();
                _progressX = x;
                _progressTime = Time.time;
            }
        }

        /// <summary>Partner terjebak: kedua karakter respawn bersama di dekat karakter aktif (bila berdiri).</summary>
        protected virtual void OnStuck()
        {
            if (Leader.IsGrounded) tandem.RespawnAll(Leader.transform.position + Vector3.up * 0.2f);
            else Leader.GetComponent<PlayerHealth>().Respawn();
        }

        // ------------------------------------------------------------ sensor

        /// <summary>Raycast yang hanya mengenai level statis (tanpa Rigidbody, tanpa trigger).</summary>
        protected static bool StaticRay(Vector3 origin, Vector3 dir, float dist, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, s_Hits, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            hit = default;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (s_Hits[i].rigidbody != null) continue;
                if (s_Hits[i].distance < best) { best = s_Hits[i].distance; hit = s_Hits[i]; found = true; }
            }
            return found;
        }

        protected static bool GroundBelow(Vector3 from, float depth) => StaticRay(from, Vector3.down, depth, out _);

        /// <summary>Cari pijakan di seberang celah dalam jangkauan lompat.</summary>
        protected static bool FindLanding(Vector3 me, float dir, float reach)
        {
            for (float d = 1f; d <= reach; d += 0.5f)
                if (GroundBelow(me + new Vector3(dir * d, 1f, 0f), 4f)) return true;
            return false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
