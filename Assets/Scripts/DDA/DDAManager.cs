using System;
using System.Collections.Generic;
using UnityEngine;

namespace Untai.DDA
{
    /// <summary>
    /// Sampel performa generik satu segmen permainan. Tidak tahu apa pun soal benang atau
    /// platformer, sehingga DDAManager bisa dipakai ulang di game lain.
    /// </summary>
    public struct PerformanceSample
    {
        public int failures;        // kematian / gagal
        public int damage;          // damage diterima
        public float time;          // waktu menyelesaikan segmen
        public float expectedTime;  // waktu wajar segmen
        public float resourceRatio; // sisa sumber daya 0..1, atau -1 bila tidak ada data
    }

    public enum DifficultyState { Struggling, Balanced, TooEasy }

    public struct DDADecision
    {
        public float performance;
        public DifficultyState state;
        public float before;
        public float after;
    }

    /// <summary>
    /// Skor performa dari rolling window beberapa segmen terakhir (yang terbaru berbobot lebih besar).
    /// Hasil 0..1: 0 = sangat kesulitan, 1 = sangat lancar.
    /// </summary>
    public class DifficultyScorer
    {
        public int windowSize = 4;
        public float recencyDecay = 0.7f;

        readonly List<PerformanceSample> _window = new List<PerformanceSample>(8);
        public int Count => _window.Count;

        public void Add(PerformanceSample s)
        {
            _window.Add(s);
            while (_window.Count > windowSize) _window.RemoveAt(0);
        }

        public void Clear() => _window.Clear();

        /// <summary>Skor satu sampel: gabungan kematian, damage, waktu, dan sisa sumber daya.</summary>
        public static float Score(PerformanceSample s)
        {
            float death = 1f - Mathf.Clamp01(s.failures / 2f);
            float dmg = 1f - Mathf.Clamp01(s.damage / 3f);
            float exp = Mathf.Max(1f, s.expectedTime);
            float time = Mathf.Clamp01((2f * exp - s.time) / exp); // <= waktu wajar: 1, >= 2x: 0
            float res = s.resourceRatio < 0f ? 0.6f : Mathf.Clamp01(s.resourceRatio * 1.5f);
            return 0.45f * death + 0.2f * dmg + 0.2f * time + 0.15f * res;
        }

        /// <summary>Rata-rata berbobot (terbaru = bobot 1, sebelumnya dikali recencyDecay).</summary>
        public float Evaluate(PerformanceSample? inProgress = null)
        {
            float sum = 0f, wsum = 0f, w = 1f;
            if (inProgress.HasValue)
            {
                sum += Score(inProgress.Value) * 1.2f;
                wsum += 1.2f;
            }
            for (int i = _window.Count - 1; i >= 0; i--)
            {
                sum += Score(_window[i]) * w;
                wsum += w;
                w *= recencyDecay;
            }
            return wsum > 0f ? sum / wsum : 0.6f;
        }
    }

    /// <summary>
    /// Inti DDA mandiri: menerima skor performa (0..1) dan menghasilkan skor kesulitan (0..1)
    /// dengan tiga kondisi. Perubahan per evaluasi dibatasi maxStep agar tidak melonjak.
    /// </summary>
    public class DDAManager
    {
        public float lowThreshold = 0.45f;   // di bawah ini: pemain kesulitan
        public float highThreshold = 0.72f;  // di atas ini: terlalu mudah
        public float maxStep = 0.12f;
        public float minStep = 0.04f;

        public float Difficulty { get; private set; } = 0.4f;
        public float Performance { get; private set; } = 0.6f;
        public DifficultyState State { get; private set; } = DifficultyState.Balanced;

        public event Action<DDADecision> Decided;

        public void Reset(float difficulty)
        {
            Difficulty = Mathf.Clamp01(difficulty);
            Performance = 0.6f;
            State = DifficultyState.Balanced;
        }

        public static DifficultyState Classify(float performance, float low, float high)
        {
            if (performance < low) return DifficultyState.Struggling;
            if (performance > high) return DifficultyState.TooEasy;
            return DifficultyState.Balanced;
        }

        public DDADecision Evaluate(float performance)
        {
            Performance = Mathf.Clamp01(performance);
            State = Classify(Performance, lowThreshold, highThreshold);
            float before = Difficulty;
            float delta = 0f;
            // Besar perubahan sebanding dengan jarak skor dari batas, dibatasi minStep..maxStep.
            if (State == DifficultyState.Struggling)
                delta = -Mathf.Clamp((lowThreshold - Performance) / lowThreshold * maxStep * 2f, minStep, maxStep);
            else if (State == DifficultyState.TooEasy)
                delta = Mathf.Clamp((Performance - highThreshold) / (1f - highThreshold) * maxStep * 2f, minStep, maxStep);
            Difficulty = Mathf.Clamp01(Difficulty + delta);

            var d = new DDADecision { performance = Performance, state = State, before = before, after = Difficulty };
            Decided?.Invoke(d);
            return d;
        }
    }
}
