using UnityEngine;

namespace Untai.Thread
{
    /// <summary>
    /// Menggambar benang wol dengan LineRenderer mengikuti jejak ThreadController:
    /// titik tambat (checkpoint) -> titik-titik jejak -> pemain, lalu ke jangkar/peti saat terkait.
    /// Titik kontrol disampel (dibatasi maxControlPoints) lalu dihaluskan dengan Catmull-Rom.
    /// Titik jejak yang dicatat saat pemain di tanah digambar sedikit di atas permukaan.
    /// Benang menipis dan memerah saat sisa benang di bawah 20%. Semua buffer dialokasikan sekali.
    /// Segmen ke pemain menggantung (sag) sesuai sisa benang, garis kait hampir lurus, ada simpul di
    /// titik tambat, dan partikel kecil muncul saat mengait atau melepas.
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
        [Tooltip("Lengkung menggantung maksimum segmen kendur ke pemain, dan lengkung garis kait.")]
        public float maxSag = 0.35f;
        public float hookSag = 0.12f;

        ThreadController _thread;
        Transform _knot;
        ParticleSystem _fx;
        Transform _prevTarget;
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

            // Simpul kecil di titik tambat (checkpoint).
            _knot = Core.Prims.Create(PrimitiveType.Sphere, "ThreadKnot", _thread.TetherPoint, Vector3.one * 0.3f,
                woolColorTether, null, false).transform;
            CreateHookFx();
        }

        /// <summary>Partikel serat wol untuk efek kait/lepas; dipancarkan manual lewat Emit (tanpa alokasi).</summary>
        void CreateHookFx()
        {
            var go = new GameObject("ThreadHookFx");
            _fx = go.AddComponent<ParticleSystem>();
            _fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _fx.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = woolColor;
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var emission = _fx.emission;
            emission.rateOverTime = 0f;
            var shape = _fx.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (r != null) r.sharedMaterial = _line.sharedMaterial;
        }

        void OnDestroy()
        {
            if (_line != null) Destroy(_line.gameObject);
            if (_knot != null) Destroy(_knot.gameObject);
            if (_fx != null) Destroy(_fx.gameObject);
        }

        void LateUpdate()
        {
            UpdateKnotAndFx();
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

            Vector3 player = AtLineDepth(transform.position + Vector3.down * airDrop);
            Transform target = _thread.SwingAnchor;
            // Segmen kendur terakhir ke pemain menggantung; makin banyak sisa benang, makin melengkung.
            if (target == null && count > 0)
            {
                Vector3 last = _control[count - 1];
                float sag = Mathf.Min(maxSag, Vector3.Distance(last, player) * 0.15f) * _thread.RemainingRatio;
                count = AddSag(count, last, player, 3, sag);
            }
            _control[count++] = player;
            if (target != null)
            {
                // Garis kait (jangkar atau peti): hampir lurus dengan sedikit lengkung.
                Vector3 t = AtLineDepth(target.position);
                count = AddSag(count, player, t, 4, Mathf.Min(hookSag, Vector3.Distance(player, t) * 0.03f));
                _control[count++] = t;
            }
            return count;
        }

        /// <summary>Tambah titik antara a dan b (tanpa ujung) yang turun membentuk parabola sedalam sag.</summary>
        int AddSag(int count, Vector3 a, Vector3 b, int points, float sag)
        {
            if (sag < 0.01f) return count;
            for (int k = 1; k <= points; k++)
            {
                float t = k / (float)(points + 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                _control[count++] = p;
            }
            return count;
        }

        void UpdateKnotAndFx()
        {
            _knot.position = AtLineDepth(_thread.TetherPoint);
            Transform target = _thread.SwingAnchor;
            if (target == _prevTarget) return;
            // Kait baru: semburan di target; lepas: semburan kecil di pemain.
            if (target != null) { _fx.transform.position = target.position; _fx.Emit(14); }
            else { _fx.transform.position = transform.position; _fx.Emit(8); }
            _prevTarget = target;
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
