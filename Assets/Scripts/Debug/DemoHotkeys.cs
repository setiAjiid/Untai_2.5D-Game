using UnityEngine;
using UnityEngine.InputSystem;
using Untai.Core;
using Untai.DDA;
using Untai.Level;

namespace Untai.DebugTools
{
    /// <summary>
    /// Tombol demo untuk rekaman/presentasi agar perubahan DDA bisa ditunjukkan cepat:
    ///   F1 HUD | F2 simulasi mati | F3 simulasi performa bagus | F4 simulasi performa buruk
    ///   F5 seed baru | F6 ulang seed sama | F7 level berikutnya | F8 reset DDA
    ///   F9 lompat ke chunk berikutnya | R respawn di checkpoint
    /// </summary>
    public class DemoHotkeys : MonoBehaviour
    {
        public const string Legend =
            "F1 HUD  F2 sim. mati  F3 sim. bagus  F4 sim. buruk  F5 seed baru  F6 ulang seed  " +
            "F7 level berikut  F8 reset DDA  F9 chunk berikut  F10 paksa aksi partner  R respawn";

        GameDirector _director;
        GUIStyle _style;

        void Start()
        {
            _director = GetComponent<GameDirector>();
            if (_director == null) _director = GameDirector.Instance;
        }

        void Update()
        {
            if (_director == null || _director.Level == null) return;

            if (InputReader.KeyDown(Key.F1)) DebugHud.Visible = !DebugHud.Visible;

            if (InputReader.KeyDown(Key.F2))
            {
                _director.Flash("Simulasi: pemain mati");
                _director.Health.Die(DeathCause.Simulated);
            }

            if (InputReader.KeyDown(Key.F3)) Inject(true);
            if (InputReader.KeyDown(Key.F4)) Inject(false);
            if (InputReader.KeyDown(Key.F5)) _director.NewRandomSeed();
            if (InputReader.KeyDown(Key.F6)) _director.RestartLevel();
            if (InputReader.KeyDown(Key.F7)) _director.NextLevel();

            if (InputReader.KeyDown(Key.F8))
            {
                _director.Dda.ResetDDA();
                _director.Flash("DDA direset ke skor awal " + _director.Dda.initialDifficulty.ToString("0.00"));
            }

            if (InputReader.KeyDown(Key.F9)) SkipToNextChunk();

            if (InputReader.KeyDown(Key.F10) && _director.Partner != null)
            {
                _director.Partner.CycleForced();
                var f = _director.Partner.Forced;
                _director.Flash(f.HasValue ? "Partner dipaksa: " + f.Value : "Partner: Utility AI bebas memilih");
            }

            if (InputReader.KeyDown(Key.R)) _director.Health.Respawn();
        }

        /// <summary>
        /// Masukkan catatan chunk buatan ke tracker seolah-olah pemain menyelesaikan chunk:
        /// bagus = tanpa mati/damage, cepat, sisa benang banyak; buruk = sering mati, lambat.
        /// Catatan ini mengalir lewat jalur yang sama (Tracker -> DDA -> Parameter).
        /// </summary>
        void Inject(bool good)
        {
            var rec = new ChunkRecord
            {
                chunkIndex = _director.Tracker.Current.chunkIndex,
                deaths = good ? 0 : 2,
                falls = good ? 0 : 1,
                damage = good ? 0 : 3,
                time = good ? 3f : 30f,
                threadRatioAtCheckpoint = good ? 0.9f : 0.05f
            };
            _director.Tracker.InjectRecord(rec);
            _director.Flash(good ? "Simulasi: performa BAGUS (chunk sempurna)" : "Simulasi: performa BURUK (kesulitan)");
        }

        /// <summary>Pindahkan pemain ke awal chunk berikutnya (memicu perakitan chunk di depan).</summary>
        void SkipToNextChunk()
        {
            var lb = _director.Level;
            int next = lb.PlayerChunkIndex + 1;
            if (next >= lb.Chunks.Count) return;
            BuiltChunk c = lb.Chunks[next];
            float top = c.def.platforms[0].top;
            var cp = FindCheckpointIn(c);
            if (cp != null) cp.Activate();
            // Pindahkan kedua karakter Tandem ke awal chunk berikutnya.
            _director.Tandem.RespawnAll(new Vector3(c.startX + 2.5f, top + 1.2f, 0f));
        }

        static Checkpoint FindCheckpointIn(BuiltChunk c)
        {
            var all = FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                float x = all[i].transform.position.x;
                if (x >= c.startX && x < c.endX) return all[i];
            }
            return null;
        }

        void OnGUI()
        {
            if (!DebugHud.Visible) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _style.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }
            var r = new Rect(8f, Screen.height - 50f, Screen.width - 16f, 42f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(16f, r.y + 4f, r.width - 16f, 18f), Legend, _style);
            GUI.Label(new Rect(16f, r.y + 21f, r.width - 16f, 18f),
                "Main: A/D gerak, Space lompat, Tab/Q ganti karakter, E perintah partner (tunggu/ikuti), Shift/J kait jangkar, W/S panjat, F tarik partner", _style);
        }
    }
}
