using System.Text;
using UnityEngine;
using Untai.AI;
using Untai.Data;
using Untai.DDA;
using Untai.Level;
using Untai.Thread;

namespace Untai.DebugTools
{
    /// <summary>
    /// HUD debug berbasis OnGUI yang menampilkan alur integrasi:
    /// Player Performance -> DDA -> parameter PCG &amp; AI -> Gameplay.
    /// Teks dibangun ulang tiap 0,2 detik dengan StringBuilder agar tidak alokasi per frame.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        public static bool Visible = true;
        public float refreshInterval = 0.2f;

        public ThreadController thread;

        readonly StringBuilder _sb = new StringBuilder(2048);
        readonly GUIContent _content = new GUIContent("");
        float _nextRefresh;
        GUIStyle _style;
        GUIStyle _boxStyle;
        GameplayParameters _previous;
        float _changeTime = -100f;

        void OnEnable() => GameplayParameters.Changed += OnParamsChanged;
        void OnDisable() => GameplayParameters.Changed -= OnParamsChanged;

        void Start()
        {
            _previous = ScriptableObject.CreateInstance<GameplayParameters>();
            _previous.hideFlags = HideFlags.DontSave;
            _previous.CopyFrom(GameplayParameters.Active);
        }

        void OnParamsChanged() => _changeTime = Time.time;

        void Update()
        {
            if (!Visible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            Rebuild();
            // Setelah 4 detik, nilai "sebelumnya" mengikuti nilai sekarang agar tanda perubahan hilang.
            if (Time.time - _changeTime > 4f && _previous != null) _previous.CopyFrom(GameplayParameters.Active);
        }

        void Rebuild()
        {
            var sb = _sb;
            sb.Length = 0;
            sb.Append("<b>UNTAI  Debug HUD</b>  <color=#999>(F1 sembunyikan)</color>\n");
            AppendPerformance(sb);
            AppendDDA(sb);
            AppendParameters(sb);
            AppendGameplay(sb);
            AppendEnemies(sb);
            _content.text = sb.ToString();
        }

        void AppendPerformance(StringBuilder sb)
        {
            sb.Append("\n<b><color=#7fd7ff>[1] PLAYER PERFORMANCE</color></b>\n");
            var t = PerformanceTracker.Instance;
            if (t == null) { sb.Append("  (tracker belum aktif)\n"); return; }
            var c = t.Current;
            sb.Append("  Chunk #").Append(c.chunkIndex)
              .Append("  waktu ").Append(c.time.ToString("0.0")).Append("s")
              .Append("  mati ").Append(c.deaths)
              .Append("  jatuh ").Append(c.falls)
              .Append("  dmg ").Append(c.damage).Append('\n');
            sb.Append("  Total: mati ").Append(t.TotalDeaths)
              .Append("  jatuh ").Append(t.TotalFalls)
              .Append("  dmg ").Append(t.TotalDamage)
              .Append("  | benang@CP ").Append((t.LastCheckpointThreadRatio * 100f).ToString("0")).Append("%\n");
            if (thread != null)
            {
                sb.Append("  Benang sisa ").Append(thread.Remaining.ToString("0.0"))
                  .Append(" / ").Append(thread.maxLength.ToString("0.0")).Append('\n');
            }
        }

        void AppendDDA(StringBuilder sb)
        {
            sb.Append("\n<b><color=#ff9af0>[2] DDA</color></b>\n");
            var dda = DDAController.Instance;
            if (dda == null) { sb.Append("  (DDA belum aktif)\n"); return; }
            var m = dda.Manager;
            string stateColor = m.State == DifficultyState.Struggling ? "#80ff80"
                : m.State == DifficultyState.TooEasy ? "#ff8080" : "#ffffff";
            sb.Append("  performa ").Append(m.Performance.ToString("0.00"))
              .Append(" (window ").Append(dda.Scorer.Count).Append(")  ->  <b>skor DDA ")
              .Append(m.Difficulty.ToString("0.00")).Append("</b>  <color=").Append(stateColor).Append('>')
              .Append(StateName(m.State)).Append("</color>\n");
            sb.Append("  ambang kesulitan ").Append(m.lowThreshold.ToString("0.00")).Append(", terlalu mudah ")
              .Append(m.highThreshold.ToString("0.00")).Append(" | evaluasi ")
              .Append(dda.EvaluationCount).Append('\n');
            // Log keputusan terbaru: pemicu -> performa -> kondisi -> skor -> parameter yang berubah.
            for (int i = 0; i < dda.Log.Count && i < 4; i++)
            {
                sb.Append(i == 0 ? "  <color=#ffe08a>" : "  <color=#aaaaaa>").Append(dda.Log[i]).Append("</color>\n");
            }
        }

        public static string StateName(DifficultyState s)
        {
            switch (s)
            {
                case DifficultyState.Struggling: return "KESULITAN (turunkan)";
                case DifficultyState.TooEasy: return "TERLALU MUDAH (naikkan)";
                default: return "SEIMBANG";
            }
        }

        void AppendParameters(StringBuilder sb)
        {
            var p = GameplayParameters.Active;
            var o = _previous != null ? _previous : p;
            sb.Append("\n<b><color=#ffd27f>[3] PARAMETER PCG & GAME AI</color></b>\n");
            Param(sb, "  celah maks", p.maxGapWidth, o.maxGapWidth, "0.0");
            Param(sb, "  jebakan", p.trapDensity, o.trapDensity, "0.00");
            Param(sb, "  enemy/chunk", p.enemyCount, o.enemyCount, "0");
            sb.Append('\n');
            Param(sb, "  agresivitas", p.aggression, o.aggression, "0.00");
            Param(sb, "  checkpoint/", p.checkpointInterval, o.checkpointInterval, "0");
            Param(sb, "  benang", p.threadLength, o.threadLength, "0.0");
            Param(sb, "jangkar", p.anchorRange, o.anchorRange, "0.0");
            sb.Append('\n');
            Param(sb, "  bobot E", p.easyWeight, o.easyWeight, "0.00");
            Param(sb, " M", p.mediumWeight, o.mediumWeight, "0.00");
            Param(sb, " H", p.hardWeight, o.hardWeight, "0.00");
            sb.Append('\n');
        }

        void AppendEnemies(StringBuilder sb)
        {
            sb.Append("\n<b><color=#ff9a9a>[5] GAME AI (Enemy FSM)</color></b>\n");
            var all = EnemyBrain.All;
            int patrol = 0, chase = 0, attack = 0, ret = 0;
            for (int i = 0; i < all.Count; i++)
            {
                switch (all[i].State)
                {
                    case EnemyState.Patrol: patrol++; break;
                    case EnemyState.Chase: chase++; break;
                    case EnemyState.Attack: attack++; break;
                    default: ret++; break;
                }
            }
            sb.Append("  aktif ").Append(all.Count)
              .Append("  | Patrol ").Append(patrol).Append("  Chase ").Append(chase)
              .Append("  Attack ").Append(attack).Append("  Return ").Append(ret).Append('\n');
            if (all.Count > 0)
            {
                var e0 = all[0];
                sb.Append("  agresivitas ").Append(e0.Aggression.ToString("0.00"))
                  .Append(" -> deteksi ").Append(e0.detectRange.ToString("0.0"))
                  .Append("  kejar ").Append(e0.chaseSpeed.ToString("0.0"))
                  .Append("  ancang ").Append(e0.attackWindup.ToString("0.00"))
                  .Append("s  jeda ").Append(e0.attackCooldown.ToString("0.0")).Append("s\n");
            }
            // Tampilkan enemy terdekat dari pemain (maksimal 4) beserta state-nya.
            Vector3 pp = thread != null ? thread.transform.position : Vector3.zero;
            int shown = 0;
            for (int i = 0; i < all.Count && shown < 4; i++)
            {
                var e = all[i];
                float d = Mathf.Abs(e.transform.position.x - pp.x);
                if (d > 25f) continue;
                shown++;
                sb.Append("  E").Append(e.Id).Append(' ')
                  .Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(EnemyBrain.StateColor(e.State))).Append('>')
                  .Append(EnemyBrain.StateLabel(e.State)).Append("</color>")
                  .Append("  jarak ").Append(d.ToString("0.0"))
                  .Append("  waktu state ").Append(e.StateTime.ToString("0.0")).Append("s\n");
            }
        }

        void AppendGameplay(StringBuilder sb)
        {
            sb.Append("\n<b><color=#b4ff9a>[4] GAMEPLAY (PCG)</color></b>\n");
            var lb = LevelBuilder.Instance;
            if (lb == null) { sb.Append("  (level belum dibangun)\n"); return; }
            sb.Append("  <b>SEED ").Append(lb.Seed).Append("</b>  level ").Append(lb.LevelNumber)
              .Append("  chunk pemain #").Append(lb.PlayerChunkIndex)
              .Append(" / ").Append(lb.chunksPerLevel + 1).Append('\n');
            sb.Append("  urutan: ").Append(lb.Sequence).Append('\n');
            sb.Append("  terakhir: ").Append(lb.LastNote).Append('\n');
        }

        /// <summary>Tulis "label nilai" dengan tanda naik (merah) atau turun (hijau) jika baru berubah.</summary>
        static void Param(StringBuilder sb, string label, float now, float before, string fmt)
        {
            sb.Append(label).Append(' ');
            float d = now - before;
            if (d > 0.0001f) sb.Append("<color=#ff8080>").Append(now.ToString(fmt)).Append("+</color>");
            else if (d < -0.0001f) sb.Append("<color=#80ff80>").Append(now.ToString(fmt)).Append("-</color>");
            else sb.Append(now.ToString(fmt));
            sb.Append("  ");
        }

        void OnGUI()
        {
            if (!Visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, wordWrap = false };
                _style.normal.textColor = Color.white;
                _boxStyle = new GUIStyle(GUI.skin.box);
            }
            var content = _content;
            float h = _style.CalcHeight(content, PanelWidth - 16f) + 12f;
            GUI.Box(new Rect(8f, 8f, PanelWidth, h), GUIContent.none, _boxStyle);
            GUI.Box(new Rect(8f, 8f, PanelWidth, h), GUIContent.none, _boxStyle); // dua lapis agar lebih gelap
            GUI.Label(new Rect(16f, 12f, PanelWidth - 16f, h), content, _style);
            DrawFlowBanner();
        }

        const float PanelWidth = 600f;
        static readonly string[] FlowStages = { "PLAYER PERFORMANCE", "DDA", "PARAMETER PCG & AI", "GAMEPLAY BERUBAH" };

        /// <summary>
        /// Banner alur integrasi di kanan atas. Setelah DDA mengambil keputusan, tiap tahap
        /// menyala bergantian sehingga alur Performance -> DDA -> Parameter -> Gameplay terlihat jelas.
        /// </summary>
        void DrawFlowBanner()
        {
            var dda = DDAController.Instance;
            float since = dda != null ? Time.time - dda.LastDecisionTime : 100f;
            float w = 150f, gap = 22f;
            float x = Screen.width - (w * 4f + gap * 3f) - 10f;
            float y = 10f;
            Color old = GUI.color;
            for (int i = 0; i < FlowStages.Length; i++)
            {
                bool lit = since >= i * 0.5f && since < 3f;
                GUI.color = lit ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.75f);
                GUI.Box(new Rect(x + i * (w + gap), y, w, 26f), FlowStages[i]);
                if (i < FlowStages.Length - 1)
                {
                    GUI.color = Color.white;
                    GUI.Label(new Rect(x + i * (w + gap) + w + 4f, y + 3f, gap, 22f), "->");
                }
            }
            GUI.color = old;
        }
    }
}
