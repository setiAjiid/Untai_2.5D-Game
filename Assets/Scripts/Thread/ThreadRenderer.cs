using UnityEngine;

namespace Untai.Thread
{
    /// <summary>
    /// Menggambar benang wol dengan LineRenderer mengikuti jejak ThreadController:
    /// titik tambat (checkpoint) -> titik-titik jejak -> pemain, lalu ke jangkar/peti saat terkait.
    /// Titik kontrol disampel (dibatasi maxControlPoints) lalu dihaluskan dengan Catmull-Rom.
    /// Titik jejak yang dicatat saat pemain di tanah digambar sedikit di atas permukaan.
    /// Benang menipis dan memerah saat sisa benang di bawah 20%. Semua buffer dialokasikan sekali.
    /// </summary>
    [RequireComponent(typeof(ThreadController))]
    public class ThreadRenderer : MonoBehaviour
    {
        const float LineZ = -0.6f; // sedikit di depan level supaya terlihat
        const int Subdivisions = 3; // titik Catmull-Rom per segmen kontrol

        public float width = 0.12f;
        [Tooltip("Material garis benang (kosong = Sprites/Default). Warna diatur lewat warna wol.")]
        public Material lineMaterial;
        [Tooltip("Warna wol di dekat titik tambat dan di dekat pemain (gradasi sederhana).")]
        public Color woolColorTether = new Color(0.85f, 0.55f, 0.15f);
        public Color woolColor = new Color(1f, 0.78f, 0.3f);
        public Color tautColor = new Color(1f, 0.12f, 0.12f);
        [Tooltip("Batas jumlah titik kontrol jejak sebelum dihaluskan.")]
        public int maxControlPoints = 96;
        [Tooltip("Jarak pusat pemain ke titik benang di tanah (pusat kapsul 1 unit di atas kaki).")]
        public float groundDrop = 0.88f;
        [Tooltip("Jarak pusat pemain ke titik benang saat di udara (setinggi pinggang).")]
        public float airDrop = 0.45f;

        ThreadController _thread;
        LineRenderer _line;
        Vector3[] _control;
        Vector3[] _smooth;

        void Awake()
        {
            _thread = GetComponent<ThreadController>();
            _control = new Vector3[Mathf.Max(8, maxControlPoints) + 16];
            _smooth = new Vector3[_control.Length * Subdivisions + 1];
            var go = new GameObject("ThreadLine");
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = width;
            _line.numCapVertices = 4;
            _line.numCornerVertices = 3;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (lineMaterial != null) _line.sharedMaterial = lineMaterial;
            else
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _line.sharedMaterial = new Material(shader);
            }
        }

        void OnDestroy()
        {
            if (_line != null) Destroy(_line.gameObject);
        }

        void LateUpdate()
        {
            int count = BuildControlPoints();
            int n = Smooth(count);
            _line.positionCount = n;
            for (int i = 0; i < n; i++) _line.SetPosition(i, _smooth[i]);

            // Benang menipis dan memerah saat sisa di bawah 20%.
            float low = Mathf.Clamp01(_thread.RemainingRatio / 0.2f);
            _line.widthMultiplier = width * Mathf.Lerp(0.45f, 1f, low);
            _line.startColor = Color.Lerp(tautColor, woolColorTether, low);
            _line.endColor = Color.Lerp(tautColor, woolColor, low);
        }

        /// <summary>Isi _control: jejak tersampel (menempel permukaan bila di tanah), pemain, lalu target kait.</summary>
        int BuildControlPoints()
        {
            int budget = _control.Length - 16; // sisakan slot untuk pemain dan garis kait
            int trail = _thread.TrailCount;
            int count = 0;
            int step = trail > budget ? Mathf.CeilToInt(trail / (float)(budget - 1)) : 1;
            for (int i = 0; i < trail; i += step) _control[count++] = TrailPoint(i);
            if ((trail - 1) % step != 0) _control[count++] = TrailPoint(trail - 1);

            _control[count++] = AtLineDepth(transform.position + Vector3.down * airDrop);
            Transform target = _thread.SwingAnchor;
            if (target != null) _control[count++] = AtLineDepth(target.position);
            return count;
        }

        Vector3 TrailPoint(int i)
        {
            Vector3 p = _thread.GetTrailPoint(i);
            if (i > 0) p.y -= _thread.IsTrailPointGrounded(i) ? groundDrop : airDrop;
            return AtLineDepth(p);
        }

        /// <summary>Haluskan titik kontrol dengan Catmull-Rom (titik ujung digandakan). Mengembalikan jumlah titik.</summary>
        int Smooth(int count)
        {
            if (count < 3)
            {
                for (int i = 0; i < count; i++) _smooth[i] = _control[i];
                return count;
            }
            int n = 0;
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 p0 = _control[i > 0 ? i - 1 : 0];
                Vector3 p1 = _control[i];
                Vector3 p2 = _control[i + 1];
                Vector3 p3 = _control[i + 2 < count ? i + 2 : count - 1];
                for (int s = 0; s < Subdivisions; s++)
                    _smooth[n++] = CatmullRom(p0, p1, p2, p3, s / (float)Subdivisions);
            }
            _smooth[n++] = _control[count - 1];
            return n;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                           (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        static Vector3 AtLineDepth(Vector3 p)
        {
            p.z = LineZ;
            return p;
        }
    }
}
