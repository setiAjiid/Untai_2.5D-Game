using UnityEngine;
using Untai.Core;
using Untai.Data;
using Untai.Player;

namespace Untai.Thread
{
    /// <summary>
    /// Untai yang mengikat dua karakter Tandem. Ini mekanik utama game:
    /// - Batas jarak: kedua karakter tidak bisa berjauhan melebihi panjang untai.
    /// - Tumpuan: karakter yang berdiri di tanah menjadi tumpuan; karakter yang di udara
    ///   tertahan untai dan BERAYUN di sekitar tumpuan (turun dari tepi jurang, menyeberang).
    /// - Panjat: saat karakter aktif menggantung, W/S memendekkan/memanjangkan untai.
    /// - Tarik: tahan F untuk menggulung untai sehingga pasangan tertarik mendekat/naik.
    /// - Jangkar: tahan Shift/J dekat jangkar untuk mengaitkan untai; karakter aktif berayun dari
    ///   jangkar dan bisa memanjat (W) untuk naik ke dinding tinggi. Space melepas dengan dorongan.
    /// Panjang untai dibaca dari GameplayParameters.threadLength (diatur DDA).
    /// </summary>
    [DefaultExecutionOrder(60)] // setelah PlayerMotor kedua karakter
    public class TandemRope : MonoBehaviour
    {
        public static TandemRope Instance { get; private set; }

        public Tandem tandem;

        [Header("Untai")]
        public float minLength = 1.2f;
        public float climbSpeed = 3.5f;
        public float reelSpeed = 4f;
        public float payOutSpeed = 6f;
        [Tooltip("Dorongan tangensial A/D saat berayun.")]
        public float swingForce = 16f;
        public float releaseBoost = 1.15f;
        public float releaseUpBoost = 3.5f;
        [Tooltip("Redaman ayunan pasangan yang menggantung (tidak dikendalikan).")]
        public float partnerDamping = 0.6f;

        [Header("Tampilan")]
        public LineRenderer line;
        public Material lineMaterial;
        public float lineWidth = 0.08f;
        public Color slackColor = new Color(0.9f, 0.85f, 1f);
        public Color tautColor = new Color(1f, 0.35f, 0.25f);

        public float MaxLength => GameplayParameters.Active.threadLength;
        public float CurrentLength { get; private set; }
        public float Distance { get; private set; }
        public bool IsTaut => Distance >= CurrentLength - 0.05f;
        public Anchor Hooked { get; private set; }
        public Anchor Candidate { get; private set; }
        public bool Reeling { get; private set; }
        /// <summary>Karakter yang sedang menggantung/berayun pada untai (null bila tidak ada).</summary>
        public PlayerMotor Hanging { get; private set; }

        // ---- Permintaan dari PartnerBrain (AI), diproses lewat jalur fisika yang sama dengan pemain ----
        /// <summary>Gulung untai agar partner tertarik ke karakter aktif (misal tertahan di bawah tebing).</summary>
        public bool PartnerReelRequest { get; set; }
        /// <summary>0..1: kecepatan partner memanjat untai saat menggantung.</summary>
        public float PartnerClimbRequest { get; set; }
        /// <summary>Partner yang menggantung melompat dari untai ke arah karakter aktif.</summary>
        public bool PartnerHangJumpRequest { get; set; }

        float _hookRope;
        bool _prevHookHeld;
        bool _jumpQueued;
        bool _hangJumpQueued;
        [Tooltip("Kecepatan lompat dari untai saat menggantung (dikali jumpVelocity).")]
        public float hangJumpFactor = 0.85f;
        const int Segments = 16;
        readonly Vector3[] _points = new Vector3[Segments + 2];

        void Awake()
        {
            Instance = this;
            if (line == null) line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = lineWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (lineMaterial != null) line.sharedMaterial = lineMaterial;
            else if (line.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) line.sharedMaterial = new Material(shader);
            }
        }

        public void Bind(Tandem t)
        {
            tandem = t;
            CurrentLength = MaxLength;
            tandem.Switched += _ => Unhook(false);
            tandem.Respawned += () => { Unhook(false); CurrentLength = MaxLength; };
        }

        bool Ready => tandem != null && tandem.characterA != null && tandem.characterB != null;

        void Update()
        {
            if (!Ready) return;
            var active = tandem.Active;
            if (Hooked == null) UpdateCandidate(active);

            bool held = active.InputEnabled && InputReader.ThreadHeld;
            if (held && !_prevHookHeld && Hooked == null && Candidate != null) Hook(Candidate, active);
            else if (!held && Hooked != null) Unhook(false);
            if (Hooked != null && InputReader.JumpPressed) _jumpQueued = true;
            else if (Hanging == active && active.InputEnabled && InputReader.JumpPressed) _hangJumpQueued = true;
            _prevHookHeld = held;

            Reeling = active.InputEnabled && InputReader.ReelHeld;
        }

        /// <summary>Jangkar terdekat di atas karakter aktif yang terjangkau (jangkauan jangkar & sisa untai).</summary>
        void UpdateCandidate(PlayerMotor active)
        {
            Anchor best = null;
            float bestDist = float.MaxValue;
            Vector3 p = active.transform.position;
            float range = GameplayParameters.Active.anchorRange;
            var all = Anchor.All;
            for (int i = 0; i < all.Count; i++)
            {
                Vector3 a = all[i].transform.position;
                if (a.y < p.y + 0.3f) continue;
                float d = Vector2.Distance(a, p);
                if (d <= range && d < bestDist) { bestDist = d; best = all[i]; }
            }
            if (Candidate != best)
            {
                if (Candidate != null) Candidate.SetHighlight(false);
                if (best != null) best.SetHighlight(true);
                Candidate = best;
            }
        }

        void Hook(Anchor anchor, PlayerMotor active)
        {
            Hooked = anchor;
            _hookRope = Mathf.Max(minLength, Vector2.Distance(anchor.transform.position, active.transform.position));
            active.ExternalControl = true;
            if (Candidate != null) Candidate.SetHighlight(false);
            Candidate = null;
        }

        public void Unhook(bool withBoost)
        {
            if (Hooked == null) return;
            Hooked = null;
            if (!Ready) return;
            var active = tandem.Active;
            if (withBoost)
            {
                Vector3 v = active.Body.linearVelocity * releaseBoost;
                v.y = Mathf.Max(v.y, 0f) + releaseUpBoost;
                active.Body.linearVelocity = v;
            }
        }

        void FixedUpdate()
        {
            if (!Ready) return;
            float dt = Time.fixedDeltaTime;
            var active = tandem.Active;
            var partner = tandem.Partner;
            float max = MaxLength;

            if (_jumpQueued) { _jumpQueued = false; Unhook(true); }

            Vector3 a = active.Body.position;
            Vector3 b = partner.Body.position;
            Distance = Vector2.Distance(a, b);
            Hanging = null;

            if (Hooked != null)
            {
                // Untai lewat jangkar: pasangan -> jangkar -> karakter aktif.
                Vector3 anc = Hooked.transform.position; anc.z = 0f;
                float partnerSeg = Vector2.Distance(b, anc);
                float available = Mathf.Max(minLength, max - partnerSeg);
                _hookRope = Mathf.Clamp(_hookRope - InputReader.Vertical * climbSpeed * dt, minLength,
                    Mathf.Min(available, GameplayParameters.Active.anchorRange + 1f));
                CurrentLength = partnerSeg + _hookRope;
                Distance = CurrentLength;
                active.ExternalControl = true;
                Swing(active, anc, _hookRope, true, dt);
                Hanging = active;
                partner.ExternalControl = false;
                return;
            }

            bool aGround = active.IsGrounded;
            bool bGround = partner.IsGrounded;

            // Panjang untai: digulung (F), dipanjat (W/S saat menggantung), atau diulur kembali ke maksimum.
            bool activeHangs = !aGround && bGround && Distance >= CurrentLength - 0.3f;
            bool reel = Reeling || PartnerReelRequest;
            if (reel)
                CurrentLength = Mathf.Max(minLength, Mathf.Min(CurrentLength, Distance) - reelSpeed * dt);
            else if (activeHangs)
                CurrentLength = Mathf.Clamp(CurrentLength - InputReader.Vertical * climbSpeed * dt, minLength, max);
            else if (aGround && bGround)
                // Keduanya berdiri: untai diulur kembali. Pasangan yang digantung tidak diturunkan lagi.
                CurrentLength = Mathf.MoveTowards(CurrentLength, max, payOutSpeed * dt);
            CurrentLength = Mathf.Min(CurrentLength, max);

            // Tentukan siapa yang bergerak mengikuti batasan untai dan siapa yang menjadi tumpuan.
            PlayerMotor mover = null;
            if (reel) mover = partner;                          // menarik pasangan (pemain F atau permintaan AI)
            else if (bGround && !aGround) mover = active;       // aktif menggantung dari pasangan
            else if (aGround && !bGround) mover = partner;      // pasangan menggantung dari aktif
            else if (aGround && bGround) mover = active;        // keduanya di tanah: aktif tertahan

            active.ExternalControl = false;
            partner.ExternalControl = false;

            if (mover == null)
            {
                // Keduanya di udara: koreksi setengah-setengah.
                if (Distance > CurrentLength) SplitCorrect(active, partner);
                return;
            }

            var pivotBody = mover == active ? partner : active;
            Vector3 pivot = pivotBody.Body.position;
            bool swinging = !mover.IsGrounded && Vector2.Distance(mover.Body.position, pivot) >= CurrentLength - 0.3f;
            if (swinging)
            {
                Hanging = mover;
                mover.ExternalControl = true; // agar motor tidak meredam ayunan
            }
            // Partner AI yang menggantung memanjat untai sendiri, lalu melompat ke tepi platform.
            if (swinging && mover == partner && !reel)
            {
                if (PartnerClimbRequest > 0f)
                    CurrentLength = Mathf.Max(minLength, CurrentLength - climbSpeed * PartnerClimbRequest * dt);
                if (PartnerHangJumpRequest)
                {
                    PartnerHangJumpRequest = false;
                    var gp = GameplayParameters.Active;
                    Vector3 pv = partner.Body.linearVelocity;
                    pv.y = gp.jumpVelocity * hangJumpFactor;
                    pv.x = Mathf.Sign(active.Body.position.x - partner.Body.position.x) * gp.moveSpeed * 0.8f;
                    partner.Body.linearVelocity = pv;
                    partner.ExternalControl = false;
                    return;
                }
            }

            Swing(mover, pivot, CurrentLength, mover == active, dt);

            // Lompat dari untai: karakter aktif yang menggantung bisa melompat naik ke tepi platform.
            if (_hangJumpQueued)
            {
                _hangJumpQueued = false;
                if (swinging && mover == active)
                {
                    var p = GameplayParameters.Active;
                    Vector3 v = active.Body.linearVelocity;
                    v.y = p.jumpVelocity * hangJumpFactor;
                    float h = InputReader.Horizontal;
                    if (Mathf.Abs(h) > 0.1f) v.x = h * p.moveSpeed;
                    active.Body.linearVelocity = v;
                    active.ExternalControl = false;
                }
            }
        }

        /// <summary>
        /// Batasan pendulum: jika lebih jauh dari panjang tali, kembalikan ke lingkaran dan buang
        /// kecepatan radial yang menjauh. Karakter aktif bisa memompa ayunan dengan A/D.
        /// </summary>
        void Swing(PlayerMotor m, Vector3 pivot, float length, bool controllable, float dt)
        {
            var body = m.Body;
            Vector3 p = body.position;
            Vector3 v = body.linearVelocity;
            Vector3 d = p - pivot; d.z = 0f;
            float dist = d.magnitude;
            if (dist < 0.001f) return;
            Vector3 n = d / dist;

            if (!m.IsGrounded && dist >= length - 0.3f)
            {
                if (controllable) v += new Vector3(-n.y, n.x, 0f) * (InputReader.Horizontal * swingForce * dt);
                else v *= 1f - partnerDamping * dt;
            }

            if (dist > length)
            {
                Vector3 target = pivot + n * length;
                target.z = 0f;
                body.position = target;
                float radial = Vector3.Dot(v, n);
                if (radial > 0f) v -= n * radial;
            }
            v.z = 0f;
            body.linearVelocity = v;
        }

        void SplitCorrect(PlayerMotor x, PlayerMotor y)
        {
            Vector3 px = x.Body.position, py = y.Body.position;
            Vector3 d = py - px; d.z = 0f;
            float dist = d.magnitude;
            if (dist < 0.001f) return;
            Vector3 n = d / dist;
            float excess = (dist - CurrentLength) * 0.5f;
            x.Body.position = px + n * excess;
            y.Body.position = py - n * excess;
            Vector3 vx = x.Body.linearVelocity, vy = y.Body.linearVelocity;
            float rel = Vector3.Dot(vy - vx, n);
            if (rel > 0f)
            {
                x.Body.linearVelocity = vx + n * (rel * 0.5f);
                y.Body.linearVelocity = vy - n * (rel * 0.5f);
            }
        }

        // ---------------------------------------------------------------- render

        void LateUpdate()
        {
            if (!Ready || line == null) return;
            Vector3 b = tandem.Partner.transform.position;
            Vector3 a = tandem.Active.transform.position;
            int count;
            if (Hooked != null)
            {
                count = FillSag(b, Hooked.transform.position, Segments, 0, Mathf.Max(0f, CurrentLength - _hookRope - Vector2.Distance(b, Hooked.transform.position)));
                _points[count++] = Flat(a);
            }
            else
            {
                count = FillSag(b, a, Segments + 1, 0, Mathf.Max(0f, CurrentLength - Distance));
            }
            line.positionCount = count;
            for (int i = 0; i < count; i++) line.SetPosition(i, _points[i]);
            float tension = CurrentLength > 0f ? Mathf.Clamp01(Distance / CurrentLength) : 1f;
            Color c = Color.Lerp(slackColor, tautColor, Mathf.InverseLerp(0.85f, 1f, tension));
            line.startColor = c;
            line.endColor = c;
        }

        static Vector3 Flat(Vector3 p) { p.z = -0.6f; return p; }

        /// <summary>Titik parabola dari a ke b; lengkungan sebanding dengan kekenduran untai.</summary>
        int FillSag(Vector3 a, Vector3 b, int n, int start, float slack)
        {
            float sag = Mathf.Clamp(slack * 0.5f, 0f, 2.5f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                _points[start + i] = Flat(p);
            }
            return start + n;
        }

        /// <summary>Buat objek untai dari prefab PrefabLibrary, atau objek baru bila kosong.</summary>
        public static TandemRope FindOrCreate()
        {
            var existing = FindAnyObjectByType<TandemRope>();
            if (existing != null) return existing;
            var prefab = PrefabLibrary.Get(l => l.tandem);
            if (prefab != null)
            {
                var r = Prims.Spawn(prefab, Vector3.zero, null).GetComponent<TandemRope>();
                if (r != null) return r;
            }
            var go = new GameObject("Tandem");
            go.AddComponent<Tandem>();
            return go.AddComponent<TandemRope>();
        }
    }
}
