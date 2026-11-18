using UnityEngine;
using Untai.Data;
using Untai.AI;
using Untai.DDA;
using Untai.Level;
using Untai.Player;
using Untai.Thread;

namespace Untai.Core
{
    /// <summary>
    /// Perakit seluruh sistem dan pemilik alur integrasi:
    ///
    ///   Player Performance  (PerformanceTracker: mati, jatuh, damage, waktu, sisa benang)
    ///        -> DDA         (DDAController: skor performa rolling window -> skor kesulitan)
    ///        -> Parameter   (DifficultyMapper -> GameplayParameters.Active)
    ///        -> PCG         (LevelBuilder merakit chunk berikutnya dengan parameter terbaru)
    ///        -> Game AI     (EnemyBrain memakai agresivitas terbaru lewat event Changed)
    ///        -> Perubahan Gameplay yang dialami pemain, lalu kembali tercatat sebagai performa.
    /// </summary>
    [DefaultExecutionOrder(-100)] // set sumber parameter & prefab sebelum skrip lain membacanya
    public class GameDirector : MonoBehaviour
    {
        public int startSeed = 12345;

        [Header("Aset (isi di Inspector; kosong = dimuat dari Resources/Untai)")]
        public GameplayParameters parameters;
        public PrefabLibrary prefabs;

        [Header("Referensi scene (opsional; kosong = dibuat otomatis)")]
        public PlayerMotor characterA;
        public PlayerMotor characterB;
        [Tooltip("Objek dengan komponen Tandem dan TandemRope.")]
        public TandemRope rope;
        public KillZone killZone;

        public static GameDirector Instance { get; private set; }

        public Tandem Tandem { get; private set; }
        public TandemRope Rope { get; private set; }
        public PartnerBrain Partner { get; private set; }
        /// <summary>Karakter yang sedang dikendalikan.</summary>
        public PlayerMotor Player => Tandem != null ? Tandem.Active : null;
        public PlayerHealth Health => Tandem != null ? Tandem.ActiveHealth : null;
        public PerformanceTracker Tracker { get; private set; }
        public DDAController Dda { get; private set; }
        public LevelBuilder Level { get; private set; }
        public EnemySpawner Spawner { get; private set; }

        void Awake()
        {
            Instance = this;
            GameplayParameters.UseSource(parameters);
            PrefabLibrary.SetActive(prefabs);
        }

        T GetOrAdd<T>() where T : Component
        {
            var c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        static PlayerMotor FindCharacter(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<PlayerMotor>() : null;
        }

        void Start()
        {
            GameBootstrap.EnsureLight();

            // Dua karakter Tandem: pakai instance di scene bila ada, jika tidak dari prefab/primitif.
            var a = characterA != null ? characterA : FindCharacter("CharacterA");
            var b = characterB != null ? characterB : FindCharacter("CharacterB");
            if (a == null) a = PlayerMotor.Spawn(0, new Vector3(2.5f, 1.2f, 0f));
            if (b == null) b = PlayerMotor.Spawn(1, new Vector3(1.3f, 1.2f, 0f));
            PlayerMotor.EnsureComponents(a.gameObject);
            PlayerMotor.EnsureComponents(b.gameObject);

            // Untai pengikat: objek Tandem + TandemRope.
            Rope = rope != null ? rope : TandemRope.FindOrCreate();
            Tandem = Rope.GetComponent<Tandem>();
            if (Tandem == null) Tandem = Rope.gameObject.AddComponent<Tandem>();
            Tandem.Bind(a, b);
            Rope.Bind(Tandem);

            // Partner AI: karakter yang tidak dikendalikan pemain dijalankan PartnerBrain.
            Partner = Rope.GetComponent<PartnerBrain>();
            if (Partner == null) Partner = Rope.gameObject.AddComponent<PartnerBrain>();
            Partner.Bind(Tandem, Rope);
            Tandem.PartnerInput = Partner;

            // Urutan alur: performa -> DDA -> parameter -> PCG dan AI.
            Tracker = GetOrAdd<PerformanceTracker>();
            Dda = GetOrAdd<DDAController>();
            Level = GetOrAdd<LevelBuilder>();
            Level.tandem = Tandem;
            Spawner = GetOrAdd<EnemySpawner>();
            Spawner.Bind(Level, a.GetComponent<PlayerHealth>());

            var kill = killZone != null ? killZone : KillZone.FindOrCreate();
            kill.player = a.GetComponent<PlayerHealth>();
            kill.killHeight = Level.killHeight;

            GetOrAdd<DebugTools.DebugHud>();
            GetOrAdd<DebugTools.DemoHotkeys>();

            // Kamera mengikuti karakter aktif dan berpindah saat berganti karakter.
            var cam = CameraRig.SideFollowCamera.Attach(Tandem.Active.transform);
            Tandem.Switched += m => cam.SetTarget(m.transform, false);

            // Parameter awal DDA diterapkan sebelum chunk pertama dirakit.
            Dda.Initialize();
            BuildLevel(startSeed, 1);
        }

        /// <summary>Bangun level dari seed; riwayat performa dan DDA tetap berlanjut antar level.</summary>
        public void BuildLevel(int seed, int levelNumber)
        {
            CancelInvoke(nameof(NextLevel));
            _levelComplete = false;
            Tandem.Locked = false;
            Tracker.BeginLevel();
            Health.ResetFull();
            Level.BuildLevel(seed, levelNumber);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraRig.SideFollowCamera>() : null;
            if (cam != null) cam.Snap();
            Flash("LEVEL " + levelNumber + "  |  SEED " + seed);
        }

        // ---------------- Game loop ----------------

        public float nextLevelDelay = 2.5f;
        bool _levelComplete;
        string _message = "";
        float _messageUntil;
        GUIStyle _messageStyle;
        readonly System.Random _seedRng = new System.Random();

        void OnEnable() => GameEvents.GoalReached += OnGoalReached;
        void OnDisable() => GameEvents.GoalReached -= OnGoalReached;

        void OnGoalReached()
        {
            if (_levelComplete) return;
            _levelComplete = true;
            Tandem.Locked = true;
            Flash("LEVEL " + Level.LevelNumber + " SELESAI!  Lanjut ke level berikutnya...", nextLevelDelay);
            Invoke(nameof(NextLevel), nextLevelDelay);
        }

        /// <summary>Seed level berikutnya diturunkan dari seed sekarang (deterministik).</summary>
        public static int NextSeed(int seed) => (int)(((long)seed * 7919L + 104729L) % 99991L);

        public void NextLevel() => BuildLevel(NextSeed(Level.Seed), Level.LevelNumber + 1);
        public void RestartLevel() => BuildLevel(Level.Seed, Level.LevelNumber);
        public void NewRandomSeed() => BuildLevel(_seedRng.Next(1000, 99999), Level.LevelNumber);

        /// <summary>Pesan singkat di tengah layar.</summary>
        public void Flash(string message, float duration = 2f)
        {
            _message = message;
            _messageUntil = Time.time + duration;
        }

        void OnGUI()
        {
            if (Time.time > _messageUntil) return;
            if (_messageStyle == null)
            {
                _messageStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
                };
                _messageStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
            }
            var r = new Rect(0f, Screen.height * 0.25f, Screen.width, 50f);
            GUI.Box(new Rect(Screen.width * 0.2f, r.y, Screen.width * 0.6f, r.height), GUIContent.none);
            GUI.Label(r, _message, _messageStyle);
        }
    }
}
