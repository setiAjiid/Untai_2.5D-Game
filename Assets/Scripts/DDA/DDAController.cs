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
        int _deathChunk = -1;
        int _deathCount;
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

        PerformanceSample ToSample(ChunkRecord r, bool inProgress)
        {
            float expected = 8f;
            var lb = LevelBuilder.Instance;
            if (lb != null && r.chunkIndex >= 0 && r.chunkIndex < lb.Chunks.Count)
                expected = lb.Chunks[r.chunkIndex].def.width / (GameplayParameters.Active.moveSpeed * 0.55f) + 1f;
            return new PerformanceSample
            {
                failures = r.deaths,
                damage = r.damage,
                // Chunk yang masih berjalan tidak dinilai dari kecepatannya.
                time = inProgress ? Mathf.Max(r.time, expected) : r.time,
                expectedTime = expected,
                resourceRatio = r.threadRatioAtCheckpoint
            };
        }

        void OnChunkCompleted(ChunkRecord r)
        {
            Scorer.Add(ToSample(r, false));
            Decide((r.simulated ? "simulasi " : "") + "chunk #" + r.chunkIndex + " selesai", null);
        }

        void OnPerformanceChanged(ChunkRecord r)
        {
            // Hanya kematian yang memicu evaluasi langsung; damage cukup dinilai saat chunk selesai.
            if (r.chunkIndex != _deathChunk || r.deaths < _deathCount) { _deathChunk = r.chunkIndex; _deathCount = 0; }
            if (r.deaths <= _deathCount) return;
            _deathCount = r.deaths;
            Decide((r.simulated ? "simulasi mati" : "mati") + " di chunk #" + r.chunkIndex, ToSample(r, true));
        }

        void Decide(string trigger, PerformanceSample? inProgress)
        {
            Initialize();
            float perf = Scorer.Evaluate(inProgress);
            var decision = Manager.Evaluate(perf);
            _changes.Length = 0;
            Mapper.Apply(decision.after, GameplayParameters.Active, _changes);
            EvaluationCount++;
            LastDecision = decision;
            LastDecisionTime = Time.time;
            GameplayParameters.NotifyChanged();
            OnDecisionLogged(trigger, decision, _changes.ToString());
        }

        /// <summary>Titik log keputusan (diisi pada commit log HUD).</summary>
        protected virtual void OnDecisionLogged(string trigger, DDADecision d, string changes) { }

        /// <summary>Kembalikan DDA dan parameter ke kondisi awal.</summary>
        public void ResetDDA()
        {
            Scorer.Clear();
            Manager.Reset(initialDifficulty);
            Mapper.ApplyImmediate(Manager.Difficulty, GameplayParameters.Active);
            _deathChunk = -1;
            _deathCount = 0;
            GameplayParameters.NotifyChanged();
        }
    }
}
