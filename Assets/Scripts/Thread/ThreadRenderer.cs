using UnityEngine;

namespace Untai.Thread
{
    /// <summary>
    /// Menggambar benang dengan LineRenderer dari titik tambat (checkpoint) ke pemain.
    /// Benang melengkung (sag) saat kendur dan memerah saat hampir habis.
    /// Jika sedang mengayun, benang melewati jangkar terlebih dahulu.
    /// </summary>
    [RequireComponent(typeof(ThreadController))]
    public class ThreadRenderer : MonoBehaviour
    {
        const int SegmentCount = 16;

        public float width = 0.08f;
        [Tooltip("Material garis benang (kosong = Sprites/Default). Warna diatur lewat slack/taut color.")]
        public Material lineMaterial;
        public Color slackColor = new Color(0.9f, 0.85f, 1f);
        public Color tautColor = new Color(1f, 0.15f, 0.15f);

        ThreadController _thread;
        LineRenderer _line;
        readonly Vector3[] _points = new Vector3[SegmentCount + 2];

        void Awake()
        {
            _thread = GetComponent<ThreadController>();
            var go = new GameObject("ThreadLine");
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.widthMultiplier = width;
            _line.numCapVertices = 2;
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
            Vector3 start = _thread.TetherPoint;
            Vector3 end = transform.position;
            int count;

            Transform anchor = _thread.SwingAnchor;
            if (anchor != null)
            {
                // Checkpoint -> jangkar (melengkung) -> pemain (lurus, tegang).
                count = FillSag(start, anchor.position, SegmentCount);
                end.z = -0.6f;
                _points[count++] = end;
            }
            else
            {
                count = FillSag(start, end, SegmentCount + 1);
            }

            _line.positionCount = count;
            for (int i = 0; i < count; i++) _line.SetPosition(i, _points[i]);
            Color c = Color.Lerp(tautColor, slackColor, Mathf.Clamp01(_thread.RemainingRatio * 2.5f));
            _line.startColor = c;
            _line.endColor = c;
        }

        /// <summary>Isi titik parabola dari a ke b; besar lengkungan sebanding dengan sisa benang.</summary>
        int FillSag(Vector3 a, Vector3 b, int n)
        {
            float sag = Mathf.Clamp(_thread.Remaining * 0.15f, 0f, 2.5f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                p.z = -0.6f; // sedikit di depan level supaya terlihat
                _points[i] = p;
            }
            return n;
        }
    }
}
