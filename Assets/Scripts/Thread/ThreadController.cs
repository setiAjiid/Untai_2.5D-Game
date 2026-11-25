using UnityEngine;
using Untai.Player;

namespace Untai.Thread
{
    /// <summary>
    /// Sumber daya benang berbasis jalur. Benang terikat pada checkpoint aktif (titik tambat)
    /// dan terurai mengikuti jalur yang benar-benar dilalui pemain (jejak/trail), termasuk
    /// lompatan, panjatan, dan ayunan. Benang terpakai = panjang jejak + segmen ke pemain.
    /// Jika benang habis (tegang), pemain tidak bisa maju lagi tetapi tetap boleh mundur.
    /// Mencapai checkpoint baru atau respawn mengosongkan jejak.
    /// </summary>
    [DefaultExecutionOrder(50)] // setelah PlayerMotor dan ThreadSwing agar batas benang menang
    [RequireComponent(typeof(PlayerMotor))]
    public class ThreadController : MonoBehaviour
    {
        /// <summary>Kapasitas buffer jejak (dialokasikan sekali).</summary>
        public const int TrailCapacity = 1024;

        public float maxLength = 36f;
        [Tooltip("Jarak minimum dari titik jejak terakhir sebelum titik baru dicatat.")]
        public float pointSpacing = 0.25f;
        [Tooltip("Pemain dianggap kembali ke titik jejak lama bila sedekat ini (gulung balik).")]
        public float retractRadius = 0.5f;
        [Tooltip("Jumlah titik jejak terakhir yang diperiksa untuk gulung balik.")]
        public int retractWindow = 48;

        public Vector3 TetherPoint { get; private set; }
        /// <summary>Panjang benang terpakai (panjang jalur sejak titik tambat), maksimal maxLength.</summary>
        public float Used => Mathf.Min(RawUsed, maxLength);
        public float Remaining => Mathf.Max(0f, maxLength - RawUsed);
        public float RemainingRatio => maxLength > 0f ? Remaining / maxLength : 0f;
        public bool IsTaut => RawUsed >= maxLength - 0.05f;
        /// <summary>Jangkar yang sedang dipakai mengayun (null jika tidak mengayun).</summary>
        public Transform SwingAnchor { get; set; }

        /// <summary>Jumlah titik jejak yang tercatat (titik 0 = titik tambat).</summary>
        public int TrailCount => _count;
        public Vector3 GetTrailPoint(int i) => _trail[i];
        /// <summary>Apakah titik jejak i dicatat saat pemain berdiri di tanah (untuk render menempel permukaan).</summary>
        public bool IsTrailPointGrounded(int i) => _grounded[i];
        /// <summary>Panjang jejak tercatat tanpa segmen terakhir ke pemain.</summary>
        public float TrailLength => _count > 0 ? _cum[_count - 1] : 0f;

        float RawUsed => TrailLength + (_count > 0 ? Vector3.Distance(_trail[_count - 1], Flat(_lastPos)) : 0f);

        PlayerMotor _motor;
        readonly Vector3[] _trail = new Vector3[TrailCapacity];
        // Panjang kumulatif sampai titik i (angka berjalan, sehingga pemotongan jejak O(1)).
        readonly float[] _cum = new float[TrailCapacity];
        readonly bool[] _grounded = new bool[TrailCapacity];
        int _count;
        Vector3 _lastPos;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            TetherPoint = transform.position;
            _lastPos = transform.position;
            ResetTrail();
        }

        static Vector3 Flat(Vector3 v) { v.z = 0f; return v; }

        /// <summary>Pindahkan titik tambat (checkpoint), atur panjang benang, dan kosongkan jejak.</summary>
        public void SetTether(Vector3 point, float length)
        {
            TetherPoint = point;
            maxLength = Mathf.Max(1f, length);
            ResetTrail();
        }

        /// <summary>Tambah panjang benang maksimum (misal dari pickup gulungan).</summary>
        public void AddLength(float amount)
        {
            maxLength = Mathf.Max(1f, maxLength + amount);
        }

        /// <summary>Mulai jejak baru dari titik tambat (dipakai saat checkpoint baru dan respawn).</summary>
        public void ResetTrail()
        {
            _trail[0] = Flat(TetherPoint);
            _cum[0] = 0f;
            _grounded[0] = false; // titik tambat berada di gulungan checkpoint
            _count = 1;
            if (_motor != null && _motor.Body != null) _lastPos = _motor.Body.position;
        }

        void FixedUpdate()
        {
            var body = _motor.Body;
            Vector3 pos = Flat(body.position);

            Retract(pos);
            Vector3 last = _trail[_count - 1];
            float tail = Vector3.Distance(last, pos);
            float used = _cum[_count - 1] + tail;

            if (used > maxLength)
            {
                // Benang tegang: tahan gerak maju (arah jejak), gerak mundur dan jatuh tetap boleh.
                float fwd = ForwardSign(pos);
                if (fwd != 0f)
                {
                    Vector3 bp = body.position;
                    if ((bp.x - _lastPos.x) * fwd > 0f)
                    {
                        bp.x = _lastPos.x;
                        body.position = bp;
                        pos.x = bp.x;
                    }
                    Vector3 v = body.linearVelocity;
                    if (v.x * fwd > 0f)
                    {
                        v.x = 0f;
                        body.linearVelocity = v;
                    }
                }
            }
            else if (tail >= pointSpacing)
            {
                AppendPoint(pos, tail);
            }

            _lastPos = body.position;
        }

        /// <summary>
        /// Gulung balik: cari titik jejak terdekat di belakang ujung jejak. Bila pemain sudah
        /// kembali ke dekat titik itu (lebih dekat daripada ke ujung jejak), titik sesudahnya
        /// dibuang sehingga benang terpakai berkurang bertahap saat pemain berjalan mundur.
        /// </summary>
        void Retract(Vector3 pos)
        {
            if (_count < 2) return;
            float tailSqr = (pos - _trail[_count - 1]).sqrMagnitude;
            float bestSqr = retractRadius * retractRadius;
            int best = -1;
            int stop = Mathf.Max(0, _count - 1 - retractWindow);
            for (int i = _count - 2; i >= stop; i--)
            {
                float d = (pos - _trail[i]).sqrMagnitude;
                if (d < bestSqr && d < tailSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            if (best >= 0) _count = best + 1; // panjang kumulatif ikut terpotong otomatis
        }

        void AppendPoint(Vector3 pos, float segment)
        {
            if (_count >= TrailCapacity)
            {
                // Buffer penuh: geser titik terakhir saja (jarang terjadi, jejak tetap kontinu).
                int k = _count - 1;
                _trail[k] = pos;
                _cum[k] = _cum[k - 1] + Vector3.Distance(_trail[k - 1], pos);
                _grounded[k] = _motor.IsGrounded;
                return;
            }
            _trail[_count] = pos;
            _cum[_count] = _cum[_count - 1] + segment;
            _grounded[_count] = _motor.IsGrounded;
            _count++;
        }

        /// <summary>
        /// Arah "maju" horizontal: dari titik jejak yang cukup jauh di belakang menuju pemain.
        /// Dipakai agar benang tegang hanya menahan gerak yang menambah panjang jalur.
        /// </summary>
        float ForwardSign(Vector3 pos)
        {
            int stop = Mathf.Max(0, _count - 48);
            for (int i = _count - 1; i >= stop; i--)
            {
                float dx = pos.x - _trail[i].x;
                if (Mathf.Abs(dx) > 0.3f) return Mathf.Sign(dx);
            }
            float t = pos.x - TetherPoint.x;
            return Mathf.Abs(t) > 0.01f ? Mathf.Sign(t) : 0f;
        }
    }
}
