using UnityEngine;
using Untai.Data;
using Untai.Player;
using Untai.Thread;

namespace Untai.AI
{
    public enum PartnerAction { Follow, Wait, Assist }

    /// <summary>Perintah manual pemain lewat tombol E.</summary>
    public enum PartnerCommand { None, WaitHere, FollowMe }

    /// <summary>
    /// AI untuk karakter Tandem yang tidak dikendalikan pemain. Menggerakkan PlayerMotor yang sama
    /// lewat IMotorInput (tanpa teleport), sehingga partner tunduk pada fisika dan untai yang sama.
    /// Aksi: Follow (mengikuti sampai jarak nyaman), Wait (diam sebagai tumpuan untai), dan
    /// Assist (ke tepi jurang di depan karakter aktif lalu menunggu sebagai tumpuan ayunan).
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
        public readonly float[] Scores = new float[3];

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
            if (Core.InputReader.KeyDown(UnityEngine.InputSystem.Key.E)) ToggleCommand();
            ApplyAssistLevel();
            ScoreActions();
            Act();
        }

        /// <summary>Nilai assist dari DDA (GameplayParameters) menentukan kecekatan partner.</summary>
        public float AssistLevel { get; private set; }

        void ApplyAssistLevel()
        {
            var p = GameplayParameters.Active;
            float a = Mathf.Clamp01(p.partnerAssistLevel);
            AssistLevel = a;
            comfortDistance = Mathf.Max(1.2f, p.partnerFollowDistance);
            speedFactor = Mathf.Lerp(0.85f, 1.15f, a);   // makin cekatan makin cepat menyusul
            climbFactor = Mathf.Lerp(0.4f, 1f, a);       // memanjat untai lebih cepat
            stuckTimeout = Mathf.Lerp(10f, 5f, a);       // lebih cepat pulih bila terjebak
            minHoldTime = Mathf.Lerp(2f, 1f, a);         // bereaksi lebih cepat
        }

        // ------------------------------------------------------------ keputusan

        /// <summary>Kondisi dasar: karakter aktif sedang bergantung pada untai (butuh tumpuan).</summary>
        protected bool LeaderOnRope => rope.Hooked != null || rope.Hanging == Leader;

        [Header("Utility AI")]
        [Tooltip("Waktu minimal sebelum boleh berganti aksi (histeresis waktu).")]
        public float minHoldTime = 1.5f;
        [Tooltip("Skor aksi baru harus melebihi aksi sekarang sebesar ini (histeresis nilai).")]
        public float switchMargin = 0.1f;

        float _lastSwitch = -10f;
        float _nextSense;
        bool _gapAhead;
        bool _cliffAhead;
        float _assistX;

        public bool GapAhead => _gapAhead;
        public bool CliffAhead => _cliffAhead;

        /// <summary>
        /// Skor kegunaan 0..1 tiap aksi dari faktor: jarak ke karakter aktif, kelonggaran untai
        /// (jarak / panjang untai), jurang atau tebing di depan karakter aktif, dan apakah karakter
        /// aktif sedang menggantung/mengait jangkar. Aksi berganti hanya bila lolos histeresis.
        /// </summary>
        protected virtual void ScoreActions()
        {
            if (Time.time >= _nextSense) { _nextSense = Time.time + 0.1f; SenseLeader(); }

            Vector3 me = Me.transform.position;
            float dist = Vector2.Distance(me, Leader.transform.position);
            float slack = rope.MaxLength > 0f ? Mathf.Clamp01(dist / rope.MaxLength) : 0f;
            bool onRope = LeaderOnRope;

            float follow = Mathf.Clamp01((dist - comfortDistance) / 4f) * 0.7f + 0.3f * slack;
            float wait = 0.15f + (dist <= comfortDistance ? 0.25f : 0f) + (_cliffAhead ? 0.2f : 0f);
            float assist = _gapAhead ? 0.55f + 0.35f * Mathf.Clamp01(1f - Mathf.Abs(me.x - _assistX) / 6f) : 0f;
            if (onRope)
            {
                // Karakter aktif bergantung pada untai: partner harus menjadi tumpuan.
                follow *= 0.1f;
                assist *= 0.3f;
                wait += 0.8f;
            }

            Scores[(int)PartnerAction.Follow] = Mathf.Clamp01(follow);
            Scores[(int)PartnerAction.Wait] = Mathf.Clamp01(wait);
            Scores[(int)PartnerAction.Assist] = Mathf.Clamp01(assist);
            ApplyModifiers();
            Choose(onRope);
        }

        [Header("Perintah manual (E)")]
        [Tooltip("Tambahan skor untuk aksi yang diperintahkan (besar, tetapi tidak mutlak).")]
        public float commandBonus = 0.5f;
        [Tooltip("Pengali skor aksi lain saat ada perintah.")]
        public float commandDamp = 0.7f;

        public PartnerCommand Command { get; private set; } = PartnerCommand.None;

        /// <summary>E bergantian: "Tunggu di sini" lalu "Ikuti saya".</summary>
        public void ToggleCommand()
        {
            Command = Command == PartnerCommand.WaitHere ? PartnerCommand.FollowMe : PartnerCommand.WaitHere;
            _lastSwitch = -10f; // perintah boleh langsung mengubah aksi
        }

        /// <summary>
        /// Perintah manual menaikkan skor aksi terkait dan meredam aksi lain. Tidak mutlak:
        /// misalnya saat "Tunggu" tetapi untai sudah sangat tegang, Follow masih bisa menang.
        /// </summary>
        void ApplyModifiers()
        {
            int target = Command == PartnerCommand.WaitHere ? (int)PartnerAction.Wait
                : Command == PartnerCommand.FollowMe ? (int)PartnerAction.Follow : -1;
            if (target < 0) return;
            for (int i = 0; i < Scores.Length; i++)
                Scores[i] = i == target ? Mathf.Min(1f, Scores[i] + commandBonus) : Scores[i] * commandDamp;
        }

        /// <summary>Aksi yang dipaksa tombol demo F10 (null = Utility AI bebas memilih).</summary>
        public PartnerAction? Forced { get; private set; }

        /// <summary>F10: bebas -> Follow -> Wait -> Assist -> bebas.</summary>
        public void CycleForced()
        {
            if (!Forced.HasValue) Forced = PartnerAction.Follow;
            else if (Forced.Value == PartnerAction.Assist) Forced = null;
            else Forced = Forced.Value + 1;
            _lastSwitch = Time.time;
        }

        void Choose(bool onRope)
        {
            if (Forced.HasValue) { Current = Forced.Value; return; }
            int best = 0;
            for (int i = 1; i < Scores.Length; i++) if (Scores[i] > Scores[best]) best = i;
            var next = (PartnerAction)best;
            if (next == Current) return;

            // Keselamatan: saat karakter aktif bergantung pada untai, langsung menjadi tumpuan.
            bool safety = onRope && next == PartnerAction.Wait;
            bool held = Time.time - _lastSwitch < minHoldTime;
            bool margin = Scores[best] > Scores[(int)Current] + switchMargin;
            if (safety || (!held && margin))
            {
                Current = next;
                _lastSwitch = Time.time;
            }
        }

        /// <summary>Periksa jurang atau tebing di depan karakter aktif (arah hadapnya).</summary>
        void SenseLeader()
        {
            _gapAhead = false;
            _cliffAhead = false;
            if (!Leader.IsGrounded) return;
            Vector3 l = Leader.transform.position;
            float dir = Leader.FacingSign;
            var p = GameplayParameters.Active;
            float limit = 5f;
            if (StaticRay(l, new Vector3(dir, 0f, 0f), limit, out var wall))
            {
                limit = wall.distance;
                _cliffAhead = StaticRay(l + Vector3.up * (p.JumpHeight * 0.85f), new Vector3(dir, 0f, 0f), limit + 0.3f, out _);
            }
            for (float d = 0.5f; d <= limit; d += 0.5f)
            {
                if (GroundBelow(l + new Vector3(dir * d, 0f, 0f), 3f)) continue;
                _gapAhead = true;
                // Tumpuan sedikit di belakang tepi jurang.
                _assistX = l.x + dir * (d - 0.5f) - dir * 0.8f;
                break;
            }
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

            switch (Current)
            {
                case PartnerAction.Wait:
                    _h = 0f;
                    TrackProgress(false);
                    break;
                case PartnerAction.Assist:
                    // Ke tepi jurang di depan karakter aktif, lalu diam sebagai tumpuan ayunan.
                    MoveToward(_assistX, 0.3f, false);
                    break;
                default:
                    MoveToward(Leader.transform.position.x, comfortDistance, true);
                    break;
            }
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
            Core.GameEvents.RaisePartnerStuck();
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
