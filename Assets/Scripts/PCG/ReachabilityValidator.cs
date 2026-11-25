using System.Collections.Generic;
using UnityEngine;
using Untai.Data;

namespace Untai.PCG
{
    /// <summary>
    /// Validasi jangkauan (logika murni). Platform dijadikan node graf; sisi i->j ada jika
    /// pemain bisa melompat (berdasarkan jarak dan tinggi lompat) atau mengayun lewat jangkar
    /// (berdasarkan jangkauan jangkar dan sisa benang). BFS mencari jalur dari platform masuk
    /// (platform terakhir chunk sebelumnya) ke platform terakhir chunk kandidat.
    /// Pada chunk ayun wajib (swingOnly), lompatan melewati celah dianggap tidak mungkin:
    /// hanya sambungan bersentuhan yang boleh dilalui tanpa ayunan, dan jalur harus memakai ayunan.
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
        public int LastSwingEdges { get; private set; }

        readonly List<Node> _nodes = new List<Node>(8);
        readonly List<Vector2> _anchors = new List<Vector2>(4);
        readonly int[] _dist = new int[16];
        readonly bool[] _viaSwing = new bool[16];
        readonly Queue<int> _queue = new Queue<int>(16);

        /// <summary>Jangkauan lompat horizontal untuk beda tinggi dh (positif = naik).</summary>
        public float JumpReach(float dh, GameplayParameters p)
        {
            float reach = p.JumpDistance * safety;
            if (dh > 0f) reach *= 1f - 0.5f * dh / p.JumpHeight;
            else reach += p.moveSpeed * Mathf.Sqrt(2f * -dh / p.Gravity) * safety; // turun: waktu di udara bertambah
            return reach;
        }

        /// <summary>Tinggi tambahan dari peti tarik pada chunk yang sedang divalidasi (0 bila tanpa peti).</summary>
        float _crateBoost;

        public bool CanJump(Node a, Node b, GameplayParameters p)
        {
            float gap = Mathf.Max(0f, b.left - a.right);
            float dh = b.top - a.top;
            if (dh > p.JumpHeight * safety)
            {
                // Chunk berpeti: peti yang ditarik ke kaki tebing menambah tinggi lompat efektif.
                if (_crateBoost <= 0f || dh - _crateBoost > p.JumpHeight * safety || gap > 1.5f) return false;
                dh -= _crateBoost;
            }
            return gap <= JumpReach(dh, p);
        }

        /// <summary>
        /// Cek ayunan a->b lewat jangkar: tali dari tepi platform a ke jangkar harus muat dalam
        /// jangkauan jangkar dan setengah panjang benang; titik terjauh ayunan (simetris pendulum)
        /// ditambah lontaran saat lepas harus mencapai tepi platform b.
        /// </summary>
        public bool CanSwing(Node a, Node b, List<Vector2> anchors, GameplayParameters p)
        {
            // Tali dibatasi jangkauan jangkar dan sisa benang di tepi platform a.
            float maxRope = Mathf.Min(p.anchorRange, RemainingThreadAt(a.right));
            Vector2 start = new Vector2(a.right, a.top + 1f);
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 an = anchors[i];
                if (an.x < a.right - 1f || an.x > b.left + 1f) continue;
                if (an.y - start.y < 0.5f) continue;
                float rope = Vector2.Distance(an, start);
                if (rope > maxRope) continue;
                float dyB = an.y - (b.top + 1f);
                if (dyB <= 0f) continue;
                float horiz = dyB < rope ? Mathf.Sqrt(rope * rope - dyB * dyB) : 0f;
                float reach = an.x + horiz + p.JumpDistance * 0.4f;
                if (reach >= b.left + 0.3f) return true;
            }
            return false;
        }

        float _budgetX;
        float _budget;

        /// <summary>
        /// Perkiraan sisa benang (dengan faktor aman) pada posisi x. Benang diukur sepanjang jalur,
        /// jadi jarak horizontal dikali PathFactor sebagai perkiraan panjang jalur.
        /// </summary>
        public float RemainingThreadAt(float x) =>
            (_budget - Mathf.Abs(x - _budgetX) * GameplayParameters.PathFactor) * 0.9f;

        /// <summary>
        /// Validasi chunk kandidat yang diletakkan pada offsetX setelah platform "entry".
        /// threadBudget = sisa benang (satuan jalur) di awal chunk (dari checkpoint aktif saat itu).
        /// Mengembalikan null bila lolos, atau alasan penolakan.
        /// </summary>
        public string Validate(Node entry, ChunkDefinition c, float offsetX, GameplayParameters p, float threadBudget)
        {
            _budgetX = offsetX;
            _budget = threadBudget;
            _crateBoost = c.crateSpawns != null && c.crateSpawns.Count > 0 ? ChunkDefinition.CrateHeight : 0f;
            // Benang (satuan jalur) harus cukup untuk melintasi chunk dari checkpoint di awal chunk.
            // PathFactor sudah memberi margin, jadi cukup lebar chunk + 1 unit (chunk 18 unit muat di benang 32).
            if ((c.width + 1f) * GameplayParameters.PathFactor > p.threadLength) return "lebih panjang dari benang";

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
            _viaSwing[0] = false;
            bool requireSwing = c.swingOnly;
            _queue.Clear();
            _queue.Enqueue(0);
            int swingEdges = 0;
            while (_queue.Count > 0)
            {
                int u = _queue.Dequeue();
                for (int v = 0; v < n; v++)
                {
                    if (_dist[v] >= 0 || _nodes[v].left < _nodes[u].left) continue;
                    bool jump = CanJump(_nodes[u], _nodes[v], p);
                    // Chunk ayun wajib: lompat melintasi celah ditandai tidak mungkin.
                    if (jump && requireSwing && _nodes[v].left - _nodes[u].right > 0.5f) jump = false;
                    bool swing = !jump && CanSwing(_nodes[u], _nodes[v], _anchors, p);
                    if (!jump && !swing) continue;
                    if (swing) swingEdges++;
                    _dist[v] = _dist[u] + 1;
                    _viaSwing[v] = _viaSwing[u] || swing;
                    _queue.Enqueue(v);
                }
            }

            LastNodeCount = n;
            LastPathLength = _dist[n - 1];
            LastSwingEdges = swingEdges;
            if (_dist[n - 1] >= 0)
            {
                if (requireSwing && !_viaSwing[n - 1]) return "chunk ayun wajib tanpa rute ayun";
                return null;
            }
            if (requireSwing)
                return "rute ayun tidak terjangkau (jangkar " + p.anchorRange.ToString("0.0") +
                       ", benang " + RemainingThreadAt(offsetX).ToString("0.0") + ")";

            // Cari celah yang gagal untuk alasan yang informatif.
            float worst = 0f;
            for (int i = 1; i < n; i++) worst = Mathf.Max(worst, _nodes[i].left - _nodes[i - 1].right);
            return "BFS gagal: celah " + worst.ToString("0.0") + " > lompat " +
                   (p.JumpDistance * safety).ToString("0.0") + ", tali maks " +
                   Mathf.Min(p.anchorRange, RemainingThreadAt(offsetX)).ToString("0.0");
        }
    }
}
