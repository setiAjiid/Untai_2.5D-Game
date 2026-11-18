using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Untai.Data;
using Untai.Level;

namespace Untai.DDA
{
    /// <summary>
    /// Adapter DDA untuk Untai: mengubah ChunkRecord dari PerformanceTracker menjadi
    /// PerformanceSample generik, meminta keputusan DDAManager, lalu memetakan skor
    /// kesulitan ke GameplayParameters.Active (dipakai PCG chunk berikutnya dan enemy).
    /// Evaluasi terjadi saat chunk selesai dan saat pemain mati.
    /// </summary>
    public class DDAController : MonoBehaviour
    {
        public const int MaxLog = 6;

        public static DDAController Instance { get; private set; }

        [Range(0f, 1f)] public float initialDifficulty = 0.4f;

        public readonly DDAManager Manager = new DDAManager();
        public readonly DifficultyScorer Scorer = new DifficultyScorer();
        public readonly DifficultyMapper Mapper = new DifficultyMapper();

        public int EvaluationCount { get; private set; }
        public float LastDecisionTime { get; private set; } = -100f;
        public DDADecision LastDecision { get; private set; }

        PerformanceTracker _tracker;
        bool _initialized;
        int _segChunk = -1;
        int _segDeaths;
        int _segDamage;
        float _segTime;
        readonly StringBuilder _changes = new StringBuilder(256);

        void Awake() => Instance = this;

        /// <summary>Terapkan skor awal ke parameter. Aman dipanggil berulang.</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            Manager.Reset(initialDifficulty);
            Mapper.ApplyImmediate(Manager.Difficulty, GameplayParameters.Active);
            GameplayParameters.NotifyChanged();
        }

        void Start()
        {
            Initialize();
            _tracker = PerformanceTracker.Instance;
            if (_tracker != null)
            {
                _tracker.ChunkCompleted += OnChunkCompleted;
                _tracker.PerformanceChanged += OnPerformanceChanged;
            }
        }

        void OnDestroy()
        {
            if (_tracker != null)
            {
                _tracker.ChunkCompleted -= OnChunkCompleted;
                _tracker.PerformanceChanged -= OnPerformanceChanged;
            }
        }

        float ExpectedTime(int chunkIndex)
        {
            var lb = LevelBuilder.Instance;
            if (lb != null && chunkIndex >= 0 && chunkIndex < lb.Chunks.Count)
                return lb.Chunks[chunkIndex].def.width / (GameplayParameters.Active.moveSpeed * 0.55f) + 1f;
            return 8f;
        }

        // Segmen = potongan permainan yang berakhir dengan kematian atau selesainya chunk.
        // Angka "dasar" segmen dipakai agar kematian tidak dihitung dua kali saat chunk selesai.
        void SyncSegment(ChunkRecord r)
        {
            if (r.chunkIndex != _segChunk || r.deaths < _segDeaths)
            {
                _segChunk = r.chunkIndex;
                _segDeaths = 0;
                _segDamage = 0;
                _segTime = 0f;
            }
        }

        PerformanceSample SegmentSample(ChunkRecord r, int failures, float resource)
        {
            return new PerformanceSample
            {
                failures = failures,
                damage = Mathf.Max(0, r.damage - _segDamage),
                time = Mathf.Max(0f, r.time - _segTime),
                expectedTime = ExpectedTime(r.chunkIndex),
                resourceRatio = resource
            };
        }

        void OnChunkCompleted(ChunkRecord r)
        {
            PerformanceSample sample;
            if (r.simulated)
            {
                // Catatan simulasi (tombol demo) dipakai apa adanya.
                sample = new PerformanceSample
                {
                    failures = r.deaths, damage = r.damage, time = r.time,
                    expectedTime = ExpectedTime(r.chunkIndex), resourceRatio = r.threadRatioAtCheckpoint
                };
            }
            else
            {
                SyncSegment(r);
                sample = SegmentSample(r, 0, r.threadRatioAtCheckpoint);
                _segChunk = -1;
                if (r.chunkIndex == 0) return; // chunk awal (aman) tidak dinilai
            }
            Scorer.Add(sample);
            Decide((r.simulated ? "simulasi " : "") + "chunk #" + r.chunkIndex + " selesai");
        }

        void OnPerformanceChanged(ChunkRecord r)
        {
            // Setiap kematian menutup satu segmen gagal dan langsung dievaluasi.
            // Damage tanpa kematian cukup dinilai saat segmen berikutnya ditutup.
            SyncSegment(r);
            if (r.deaths <= _segDeaths) return;
            var sample = SegmentSample(r, 1, -1f);
            _segDeaths = r.deaths;
            _segDamage = r.damage;
            _segTime = r.time;
            Scorer.Add(sample);
            Decide((r.simulated ? "simulasi mati" : "mati") + " di chunk #" + r.chunkIndex);
        }

        void Decide(string trigger)
        {
            Initialize();
            float perf = Scorer.Evaluate();
            var decision = Manager.Evaluate(perf);
            _changes.Length = 0;
            Mapper.Apply(decision.after, GameplayParameters.Active, _changes);
            EvaluationCount++;
            LastDecision = decision;
            LastDecisionTime = Time.time;
            GameplayParameters.NotifyChanged();
            OnDecisionLogged(trigger, decision, _changes.ToString());
        }

        /// <summary>Log keputusan DDA terbaru (paling baru di indeks 0) untuk HUD.</summary>
        public readonly List<string> Log = new List<string>(MaxLog + 1);

        void OnDecisionLogged(string trigger, DDADecision d, string changes)
        {
            string arrow = d.after > d.before + 0.0001f ? "naik" : d.after < d.before - 0.0001f ? "turun" : "tetap";
            string entry = "[" + Time.time.ToString("0") + "s] " + trigger + ": perf " + d.performance.ToString("0.00") +
                           " " + d.state + " -> skor " + d.before.ToString("0.00") + ">" + d.after.ToString("0.00") +
                           " (" + arrow + ")" + (changes.Length > 0 ? "\n      " + changes : "");
            Log.Insert(0, entry);
            if (Log.Count > MaxLog) Log.RemoveAt(Log.Count - 1);
            Debug.Log("[Untai DDA] " + entry);
        }

        /// <summary>Kembalikan DDA dan parameter ke kondisi awal.</summary>
        public void ResetDDA()
        {
            Scorer.Clear();
            Manager.Reset(initialDifficulty);
            Mapper.ApplyImmediate(Manager.Difficulty, GameplayParameters.Active);
            _segChunk = -1;
            GameplayParameters.NotifyChanged();
            Log.Insert(0, "[" + Time.time.ToString("0") + "s] RESET DDA -> skor " + Manager.Difficulty.ToString("0.00"));
            if (Log.Count > MaxLog) Log.RemoveAt(Log.Count - 1);
        }
    }
}
