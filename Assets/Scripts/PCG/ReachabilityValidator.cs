using System.Collections.Generic;
using UnityEngine;
using Untai.Data;

namespace Untai.PCG
{
    /// <summary>
    /// Validasi jangkauan (logika murni). Platform dijadikan node graf; sisi i->j ada jika kedua
    /// karakter Tandem bisa berpindah dengan:
    /// - lompat (jarak dan tinggi lompat dari fisika),
    /// - ayun tandem (satu karakter jadi tumpuan di tepi, yang lain berayun; panjang untai),
    /// - jangkar (mengait, memanjat untai, berayun; jangkauan jangkar dan panjang untai).
    /// BFS mencari jalur dari platform masuk (platform terakhir chunk sebelumnya) ke platform
    /// terakhir chunk kandidat.
    /// </summary>
    public class ReachabilityValidator
    {
        public struct Node
        {
            public float left, right, top;
            public Node(float left, float right, float top) { this.left = left; this.right = right; this.top = top; }
        }

        /// <summary>Faktor aman: kemampuan pemain dianggap 85% dari batas fisika ideal.</summary>
        public float safety = 0.85f;

        public int LastNodeCount { get; private set; }
        public int LastPathLength { get; private set; }
        /// <summary>Jumlah sisi yang memakai untai (ayun tandem atau jangkar) pada BFS terakhir.</summary>
        public int LastSwingEdges { get; private set; }

        readonly List<Node> _nodes = new List<Node>(8);
        readonly List<Vector2> _anchors = new List<Vector2>(4);
        readonly int[] _dist = new int[16];
        readonly Queue<int> _queue = new Queue<int>(16);

        /// <summary>Jangkauan lompat horizontal untuk beda tinggi dh (positif = naik).</summary>
        public float JumpReach(float dh, GameplayParameters p)
        {
            float reach = p.JumpDistance * safety;
            if (dh > 0f) reach *= 1f - 0.5f * dh / p.JumpHeight;
            else reach += p.moveSpeed * Mathf.Sqrt(2f * -dh / p.Gravity) * safety; // turun: waktu di udara bertambah
            return reach;
        }

        public bool CanJump(Node a, Node b, GameplayParameters p)
        {
            float gap = Mathf.Max(0f, b.left - a.right);
            float dh = b.top - a.top;
            if (dh > p.JumpHeight * safety) return false;
            return gap <= JumpReach(dh, p);
        }

        /// <summary>
        /// Ayun tandem a->b: tumpuan di tepi a, karakter lain berayun; jangkauan horizontal
        /// sqrt(L^2 - dy^2) dengan L = panjang untai x faktor aman. Pasangan menyusul dengan
        /// menggantung lalu memanjat, sehingga target tidak boleh lebih tinggi dari asal.
        /// </summary>
        public bool CanTandemSwing(Node a, Node b, GameplayParameters p)
        {
            float gap = b.left - a.right;
            if (gap <= 0f) return false;
            if (b.top > a.top + 0.3f) return false;
            float L = p.threadLength * safety;
            float dy = a.top - b.top;
            if (dy >= L) return false;
            return Mathf.Sqrt(L * L - dy * dy) >= gap + 0.5f;
        }

        /// <summary>
        /// Jangkar a->b: jangkar di atas kedua platform yang terjangkau dari a dan dari b
        /// (jangkauan jangkar dan panjang untai). Berlaku untuk celah maupun dinding tinggi.
        /// </summary>
        public bool CanAnchor(Node a, Node b, List<Vector2> anchors, GameplayParameters p)
        {
            float maxRope = Mathf.Min(p.anchorRange, p.threadLength * 0.95f);
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 an = anchors[i];
                if (an.x < a.left || an.x > b.right) continue;
                if (an.y < Mathf.Max(a.top, b.top) + 1.5f) continue;
                var from = new Vector2(Mathf.Clamp(an.x, a.left, a.right), a.top + 1f);
                var to = new Vector2(Mathf.Clamp(an.x, b.left, b.right), b.top + 1f);
                if (Vector2.Distance(an, from) <= maxRope && Vector2.Distance(an, to) <= maxRope + 0.5f) return true;
            }
            return false;
        }

        /// <summary>
        /// Validasi chunk kandidat yang diletakkan pada offsetX setelah platform "entry".
        /// Mengembalikan null bila lolos, atau alasan penolakan.
        /// </summary>
        public string Validate(Node entry, ChunkDefinition c, float offsetX, GameplayParameters p)
        {
            _nodes.Clear();
            _nodes.Add(entry);
            for (int i = 0; i < c.platforms.Count && _nodes.Count < _dist.Length; i++)
            {
                var pl = c.platforms[i];
                _nodes.Add(new Node(offsetX + pl.x, offsetX + pl.Right, pl.top));
            }
            _anchors.Clear();
            for (int i = 0; i < c.anchors.Count; i++)
                _anchors.Add(new Vector2(offsetX + c.anchors[i].x, c.anchors[i].y));

            // BFS dari node 0 (platform masuk) ke node terakhir.
            int n = _nodes.Count;
            for (int i = 0; i < n; i++) _dist[i] = -1;
            _dist[0] = 0;
            _queue.Clear();
            _queue.Enqueue(0);
            int ropeEdges = 0;
            while (_queue.Count > 0)
            {
                int u = _queue.Dequeue();
                for (int v = 0; v < n; v++)
                {
                    if (_dist[v] >= 0 || _nodes[v].left < _nodes[u].left) continue;
                    bool jump = CanJump(_nodes[u], _nodes[v], p);
                    bool rope = !jump && (CanTandemSwing(_nodes[u], _nodes[v], p) ||
                                          CanAnchor(_nodes[u], _nodes[v], _anchors, p));
                    if (!jump && !rope) continue;
                    if (rope) ropeEdges++;
                    _dist[v] = _dist[u] + 1;
                    _queue.Enqueue(v);
                }
            }

            LastNodeCount = n;
            LastPathLength = _dist[n - 1];
            LastSwingEdges = ropeEdges;
            if (_dist[n - 1] >= 0) return null;

            float worst = 0f;
            for (int i = 1; i < n; i++) worst = Mathf.Max(worst, _nodes[i].left - _nodes[i - 1].right);
            return "BFS gagal: celah " + worst.ToString("0.0") + " > lompat " +
                   (p.JumpDistance * safety).ToString("0.0") + ", untai " + p.threadLength.ToString("0.0") +
                   ", jangkar " + p.anchorRange.ToString("0.0");
        }
    }
}
