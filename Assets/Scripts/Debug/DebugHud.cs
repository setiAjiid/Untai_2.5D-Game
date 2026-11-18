using System.Text;
using UnityEngine;
using Untai.Data;
using Untai.DDA;
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
            AppendParameters(sb);
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
            sb.Append('\n');
            Param(sb, "  bobot E", p.easyWeight, o.easyWeight, "0.00");
            Param(sb, " M", p.mediumWeight, o.mediumWeight, "0.00");
            Param(sb, " H", p.hardWeight, o.hardWeight, "0.00");
            sb.Append('\n');
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
                _style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, wordWrap = false };
                _style.normal.textColor = Color.white;
                _boxStyle = new GUIStyle(GUI.skin.box);
            }
            var content = _content;
            float h = _style.CalcHeight(content, 470f) + 12f;
            GUI.Box(new Rect(8f, 8f, 486f, h), GUIContent.none, _boxStyle);
            GUI.Label(new Rect(16f, 12f, 470f, h), content, _style);
        }
    }
}
