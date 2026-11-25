using UnityEngine;

namespace Untai.Thread
{
    /// <summary>
    /// Menggambar benang dengan LineRenderer mengikuti jejak yang dicatat ThreadController:
    /// titik tambat (checkpoint) -> titik-titik jejak -> pemain. Jika sedang mengayun, benang
    /// diteruskan dari pemain ke jangkar. Warna memudar ke merah saat sisa benang kecil.
    /// Jumlah titik yang dikirim ke LineRenderer dibatasi maxRenderPoints (jejak disampel).
    /// </summary>
    [RequireComponent(typeof(ThreadController))]
    public class ThreadRenderer : MonoBehaviour
    {
        const float LineZ = -0.6f; // sedikit di depan level supaya terlihat

        public float width = 0.08f;
        [Tooltip("Material garis benang (kosong = Sprites/Default). Warna diatur lewat slack/taut color.")]
        public Material lineMaterial;
        public Color slackColor = new Color(0.9f, 0.85f, 1f);
        public Color tautColor = new Color(1f, 0.15f, 0.15f);
        [Tooltip("Batas jumlah titik yang dikirim ke LineRenderer.")]
        public int maxRenderPoints = 128;

        ThreadController _thread;
        LineRenderer _line;
        Vector3[] _points;

        void Awake()
        {
            _thread = GetComponent<ThreadController>();
            _points = new Vector3[Mathf.Max(8, maxRenderPoints) + 2];
            var go = new GameObject("ThreadLine");
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = width;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
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
            int budget = _points.Length - 2; // sisakan slot untuk pemain dan jangkar
            int trail = _thread.TrailCount;
            int count = 0;

            // Sampel jejak dengan langkah tetap bila titiknya melebihi batas; titik terakhir selalu ikut.
            int step = trail > budget ? Mathf.CeilToInt(trail / (float)(budget - 1)) : 1;
            for (int i = 0; i < trail; i += step) _points[count++] = AtLineDepth(_thread.GetTrailPoint(i));
            if ((trail - 1) % step != 0) _points[count++] = AtLineDepth(_thread.GetTrailPoint(trail - 1));

            _points[count++] = AtLineDepth(transform.position);
            Transform anchor = _thread.SwingAnchor;
            if (anchor != null) _points[count++] = AtLineDepth(anchor.position);

            _line.positionCount = count;
            for (int i = 0; i < count; i++) _line.SetPosition(i, _points[i]);
            Color c = Color.Lerp(tautColor, slackColor, Mathf.Clamp01(_thread.RemainingRatio * 2.5f));
            _line.startColor = c;
            _line.endColor = c;
        }

        static Vector3 AtLineDepth(Vector3 p)
        {
            p.z = LineZ;
            return p;
        }
    }
}
