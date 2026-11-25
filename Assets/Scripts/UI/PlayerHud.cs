using UnityEngine;
using Untai.Core;
using Untai.Level;
using Untai.Player;
using Untai.Thread;

namespace Untai.UI
{
    /// <summary>
    /// HUD pemain (terpisah dari HUD debug, selalu tampil walau F1 menyembunyikan HUD debug):
    /// nyawa sebagai kotak hati, bar sisa benang, serta nomor level dan seed di pojok.
    /// Umpan balik: layar berkedip merah dan hati yang hilang berkedip saat terkena damage
    /// (GameEvents.PlayerDamaged), serta pesan singkat saat respawn (GameEvents.PlayerDied).
    /// Teks angka hanya dibangun ulang saat nilainya berubah agar tidak alokasi per frame.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        public PlayerHealth health;
        public ThreadController thread;

        [Header("Tata letak")]
        public float heartSize = 26f;
        public float heartGap = 6f;
        public float barWidth = 240f;
        public float barHeight = 14f;
        [Tooltip("Sisa benang (rasio) di bawah nilai ini membuat bar memerah.")]
        public float lowThreadRatio = 0.25f;

        [Header("Umpan balik damage")]
        public float flashDuration = 0.3f;
        [Range(0f, 1f)] public float flashAlpha = 0.35f;
        public float lostHeartBlinkTime = 0.9f;
        public float respawnMessageTime = 1.6f;

        static readonly Color HeartFull = new Color(0.95f, 0.25f, 0.35f);
        static readonly Color HeartEmpty = new Color(0.2f, 0.2f, 0.22f, 0.85f);
        static readonly Color Outline = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color ThreadColor = new Color(0.85f, 0.8f, 1f);
        static readonly Color ThreadLowColor = new Color(1f, 0.2f, 0.2f);

        readonly GUIContent _threadText = new GUIContent("");
        readonly GUIContent _levelText = new GUIContent("");
        int _threadKey = int.MinValue;
        int _levelKey = int.MinValue;
        GUIStyle _small;
        GUIStyle _label;
        GUIStyle _message;
        static readonly GUIContent RespawnText = new GUIContent("Kembali ke checkpoint");

        float _flashStart = -100f;
        float _lostUntil = -100f;
        int _lostFrom, _lostTo; // indeks hati yang baru hilang [from, to)
        float _respawnUntil = -100f;

        void OnEnable()
        {
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.PlayerDied += OnDied;
        }

        void OnDisable()
        {
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PlayerDied -= OnDied;
        }

        // Event dipicu setelah Health dikurangi, jadi hati yang hilang = [Health, Health + amount).
        void OnDamaged(int amount)
        {
            _flashStart = Time.time;
            if (health == null) return;
            _lostFrom = Mathf.Max(0, health.Health);
            _lostTo = Mathf.Min(health.maxHealth, _lostFrom + amount);
            _lostUntil = Time.time + lostHeartBlinkTime;
        }

        void OnDied(DeathCause cause)
        {
            _flashStart = Time.time;
            _respawnUntil = Time.time + respawnMessageTime;
        }

        void Start()
        {
            var d = GameDirector.Instance;
            if (d != null)
            {
                if (health == null) health = d.Health;
                if (thread == null) thread = d.Thread;
            }
        }

        void OnGUI()
        {
            if (health == null) return;
            EnsureStyles();
            Color old = GUI.color;

            DrawDamageFlash();
            float left = 16f;
            float top = Screen.height - 120f; // di atas legenda tombol demo
            DrawHearts(left, top);
            if (thread != null) DrawThreadBar(left, top + heartSize + 10f);
            DrawLevelInfo();
            DrawRespawnMessage();

            GUI.color = old;
        }

        void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.LowerRight };
            _small.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            _message = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            _message.normal.textColor = new Color(1f, 0.85f, 0.85f);
        }

        /// <summary>Lapisan merah layar penuh yang memudar singkat setelah terkena damage.</summary>
        void DrawDamageFlash()
        {
            float t = (Time.time - _flashStart) / flashDuration;
            if (t < 0f || t >= 1f) return;
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 0f, 0f, flashAlpha * (1f - t)));
        }

        void DrawRespawnMessage()
        {
            if (Time.time >= _respawnUntil) return;
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 40f), RespawnText, _message);
        }

        /// <summary>Nyawa: satu kotak per maxHealth, terisi sesuai Health.</summary>
        void DrawHearts(float x, float y)
        {
            int max = health.maxHealth;
            int hp = health.Health;
            bool blinking = Time.time < _lostUntil;
            bool blinkOn = Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x + i * (heartSize + heartGap), y, heartSize, heartSize);
                Fill(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Outline);
                Color c = i < hp ? HeartFull : HeartEmpty;
                // Hati yang baru hilang berkedip putih sesaat.
                if (blinking && i >= hp && i >= _lostFrom && i < _lostTo && blinkOn) c = Color.white;
                Fill(r, c);
            }
        }

        /// <summary>Bar sisa benang (Remaining / maxLength) yang memerah saat hampir habis.</summary>
        void DrawThreadBar(float x, float y)
        {
            float ratio = thread.RemainingRatio;
            var back = new Rect(x, y, barWidth, barHeight);
            Fill(new Rect(back.x - 2f, back.y - 2f, back.width + 4f, back.height + 4f), Outline);
            Fill(back, BarBack);
            Color c = ratio < lowThreadRatio
                ? Color.Lerp(ThreadLowColor, ThreadColor, ratio / lowThreadRatio * 0.5f)
                : ThreadColor;
            if (ratio < lowThreadRatio * 0.5f && Mathf.Repeat(Time.time * 4f, 1f) < 0.5f) c = Color.white;
            Fill(new Rect(back.x, back.y, back.width * ratio, back.height), c);

            // Perbarui teks hanya bila nilai (per 0,1 unit) berubah.
            int key = Mathf.RoundToInt(thread.Remaining * 10f) * 10000 + Mathf.RoundToInt(thread.maxLength * 10f);
            if (key != _threadKey)
            {
                _threadKey = key;
                _threadText.text = "BENANG  " + thread.Remaining.ToString("0.0") + " / " + thread.maxLength.ToString("0");
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(x + barWidth + 10f, y - 3f, 200f, 20f), _threadText, _label);
        }

        /// <summary>Nomor level dan seed, kecil di pojok kanan bawah.</summary>
        void DrawLevelInfo()
        {
            var lb = LevelBuilder.Instance;
            if (lb == null) return;
            int key = lb.LevelNumber * 1000003 + lb.Seed;
            if (key != _levelKey)
            {
                _levelKey = key;
                _levelText.text = "Level " + lb.LevelNumber + "  |  Seed " + lb.Seed;
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width - 316f, Screen.height - 82f, 300f, 20f), _levelText, _small);
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }
    }
}
