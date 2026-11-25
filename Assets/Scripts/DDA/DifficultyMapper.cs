using System;
using System.Text;
using UnityEngine;
using Untai.Data;

namespace Untai.DDA
{
    /// <summary>
    /// Pemetaan skor kesulitan (0 = termudah, 1 = tersulit) ke GameplayParameters.
    /// Setiap parameter punya nilai mudah, nilai sulit (sekaligus batas clamp), dan batas
    /// perubahan per evaluasi (step limit) supaya gameplay tidak berubah drastis sekaligus.
    /// </summary>
    public class DifficultyMapper
    {
        [Serializable]
        public struct Range
        {
            public float easy, hard, maxStep;
            public Range(float easy, float hard, float maxStep) { this.easy = easy; this.hard = hard; this.maxStep = maxStep; }
            public float Target(float d) => Mathf.Lerp(easy, hard, d);
            public float Min => Mathf.Min(easy, hard);
            public float Max => Mathf.Max(easy, hard);
        }

        public Range gap = new Range(3.5f, 7.5f, 1.0f);
        public Range trap = new Range(0.1f, 0.85f, 0.2f);
        // Minimal 1 agar setiap chunk setelah S0 yang punya titik spawn selalu berisi enemy yang terlihat.
        public Range enemy = new Range(1f, 3f, 1f);
        public Range aggression = new Range(0.1f, 1f, 0.25f);
        public Range checkpoint = new Range(1f, 3f, 1f);
        // Benang (satuan jalur): jarak checkpoint rata-rata ~14 unit x PathFactor 1.6 = ~22 unit,
        // sehingga perkiraan pemakaian ~56% (mudah, 40) sampai ~70% (sulit, 32) di antara dua checkpoint.
        public Range thread = new Range(40f, 32f, 3f);
        public Range anchor = new Range(7.5f, 5.0f, 0.6f);   // makin sulit, jangkauan jangkar makin pendek
        public Range easyWeight = new Range(1f, 0.1f, 0.3f);
        public Range mediumWeight = new Range(0.4f, 0.8f, 0.2f);
        public Range hardWeight = new Range(0f, 1f, 0.3f);

        /// <summary>Terapkan langsung tanpa step limit (dipakai saat awal / reset).</summary>
        public void ApplyImmediate(float d, GameplayParameters p)
        {
            p.maxGapWidth = gap.Target(d);
            p.trapDensity = trap.Target(d);
            p.enemyCount = Mathf.RoundToInt(enemy.Target(d));
            p.aggression = aggression.Target(d);
            p.checkpointInterval = Mathf.RoundToInt(checkpoint.Target(d));
            p.threadLength = thread.Target(d);
            p.anchorRange = anchor.Target(d);
            p.easyWeight = easyWeight.Target(d);
            p.mediumWeight = mediumWeight.Target(d);
            p.hardWeight = hardWeight.Target(d);
        }

        /// <summary>
        /// Gerakkan tiap parameter menuju target skor d, dibatasi maxStep dan di-clamp ke rentangnya.
        /// Perubahan ditulis ke changes (misal "celah 5.1>5.6") untuk log HUD.
        /// </summary>
        public void Apply(float d, GameplayParameters p, StringBuilder changes)
        {
            p.maxGapWidth = Step("celah", p.maxGapWidth, gap, d, changes, "0.0");
            p.trapDensity = Step("jebakan", p.trapDensity, trap, d, changes, "0.00");
            p.enemyCount = Mathf.RoundToInt(Step("enemy", p.enemyCount, enemy, d, changes, "0", true));
            p.aggression = Step("agresi", p.aggression, aggression, d, changes, "0.00");
            p.checkpointInterval = Mathf.RoundToInt(Step("cp", p.checkpointInterval, checkpoint, d, changes, "0", true));
            p.threadLength = Step("benang", p.threadLength, thread, d, changes, "0.0");
            p.anchorRange = Step("jangkar", p.anchorRange, anchor, d, changes, "0.0");
            p.easyWeight = Step("wE", p.easyWeight, easyWeight, d, changes, "0.00");
            p.mediumWeight = Step("wM", p.mediumWeight, mediumWeight, d, changes, "0.00");
            p.hardWeight = Step("wH", p.hardWeight, hardWeight, d, changes, "0.00");
        }

        static float Step(string label, float current, Range r, float d, StringBuilder changes, string fmt,
            bool integer = false)
        {
            float target = r.Target(d);
            if (integer) target = Mathf.Round(target);
            float next = Mathf.Clamp(Mathf.MoveTowards(current, target, r.maxStep), r.Min, r.Max);
            if (integer) next = Mathf.Round(next);
            if (changes != null && Mathf.Abs(next - current) > 0.005f)
            {
                if (changes.Length > 0) changes.Append(", ");
                changes.Append(label).Append(' ').Append(current.ToString(fmt)).Append('>').Append(next.ToString(fmt));
            }
            return next;
        }
    }
}
