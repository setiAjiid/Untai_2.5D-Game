using System;
using System.Collections.Generic;
using UnityEngine;
using Untai.Core;

namespace Untai.DDA
{
    /// <summary>Catatan performa pemain untuk satu chunk.</summary>
    [Serializable]
    public class ChunkRecord
    {
        public int chunkIndex;
        public int deaths;
        public int falls;
        public int damage;
        public float time;
        /// <summary>Rasio sisa benang saat mencapai checkpoint di chunk ini (-1 jika tidak ada).</summary>
        public float threadRatioAtCheckpoint = -1f;
        public bool simulated;

        public void Clear(int index)
        {
            chunkIndex = index; deaths = 0; falls = 0; damage = 0; time = 0f;
            threadRatioAtCheckpoint = -1f; simulated = false;
        }

        public void CopyFrom(ChunkRecord o)
        {
            chunkIndex = o.chunkIndex; deaths = o.deaths; falls = o.falls; damage = o.damage;
            time = o.time; threadRatioAtCheckpoint = o.threadRatioAtCheckpoint; simulated = o.simulated;
        }
    }

    /// <summary>
    /// Pencatat performa: mendengarkan GameEvents (kematian, jatuh, damage, checkpoint, masuk chunk)
    /// lalu menyusun ChunkRecord per chunk. Event ChunkCompleted dan PerformanceChanged
    /// dipakai DDA untuk mengevaluasi ulang tingkat kesulitan.
    /// </summary>
    public class PerformanceTracker : MonoBehaviour
    {
        public const int MaxHistory = 32;

        public static PerformanceTracker Instance { get; private set; }

        public ChunkRecord Current { get; } = new ChunkRecord();
        /// <summary>Riwayat chunk selesai, terbaru di akhir (ring sederhana, maksimal MaxHistory).</summary>
        public readonly List<ChunkRecord> History = new List<ChunkRecord>(MaxHistory);

        public int TotalDeaths { get; private set; }
        public int TotalFalls { get; private set; }
        public int TotalDamage { get; private set; }
        public float LastCheckpointThreadRatio { get; private set; } = 1f;

        /// <summary>Chunk selesai (pemain masuk chunk berikutnya).</summary>
        public event Action<ChunkRecord> ChunkCompleted;
        /// <summary>Ada kejadian penting di chunk berjalan (mati/damage).</summary>
        public event Action<ChunkRecord> PerformanceChanged;

        float _chunkStartTime;

        void Awake()
        {
            Instance = this;
            BeginLevel();
        }

        void OnEnable()
        {
            GameEvents.PlayerDied += OnDied;
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.ChunkEntered += OnChunkEntered;
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.ChunkEntered -= OnChunkEntered;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            Current.time = Time.time - _chunkStartTime;
        }

        /// <summary>Mulai level baru: chunk berjalan direset, riwayat dipertahankan untuk DDA.</summary>
        public void BeginLevel()
        {
            Current.Clear(0);
            _chunkStartTime = Time.time;
            LastCheckpointThreadRatio = 1f;
        }

        void OnDied(DeathCause cause)
        {
            Current.deaths++;
            TotalDeaths++;
            if (cause == DeathCause.Fall) { Current.falls++; TotalFalls++; }
            if (cause == DeathCause.Simulated) Current.simulated = true;
            PerformanceChanged?.Invoke(Current);
        }

        void OnDamaged(int amount)
        {
            Current.damage += amount;
            TotalDamage += amount;
            PerformanceChanged?.Invoke(Current);
        }

        void OnCheckpoint(Vector3 pos, float ratio)
        {
            Current.threadRatioAtCheckpoint = ratio;
            LastCheckpointThreadRatio = ratio;
        }

        void OnChunkEntered(int index)
        {
            if (index <= Current.chunkIndex) return;
            CompleteCurrent();
            Current.Clear(index);
            _chunkStartTime = Time.time;
        }

        void CompleteCurrent()
        {
            Current.time = Time.time - _chunkStartTime;
            Push(Current);
        }

        /// <summary>Masukkan catatan buatan (tombol demo) seolah-olah chunk selesai.</summary>
        public void InjectRecord(ChunkRecord record)
        {
            record.simulated = true;
            Push(record);
        }

        void Push(ChunkRecord source)
        {
            ChunkRecord rec;
            if (History.Count >= MaxHistory)
            {
                rec = History[0];
                History.RemoveAt(0);
            }
            else rec = new ChunkRecord();
            rec.CopyFrom(source);
            History.Add(rec);
            ChunkCompleted?.Invoke(rec);
        }
    }
}
