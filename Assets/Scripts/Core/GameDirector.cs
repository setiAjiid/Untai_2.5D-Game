using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        [Tooltip("Seed acak saat scene dibuka (kecuali Title Screen meminta seed tertentu lewat GameLaunch).")]
        public bool randomStartSeed = true;

        [Header("Aset (isi di Inspector; kosong = dimuat dari Resources/Untai)")]
        public GameplayParameters parameters;
        public PrefabLibrary prefabs;

        [Header("Referensi scene (opsional; kosong = dibuat otomatis)")]
        public PlayerMotor player;
        public KillZone killZone;

        public static GameDirector Instance { get; private set; }

        public PlayerMotor Player { get; private set; }
        public PlayerHealth Health { get; private set; }
        public ThreadController Thread { get; private set; }
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

        void Start()
        {
            GameBootstrap.EnsureLight();

            // Pemain dan benang: pakai instance di scene bila ada, jika tidak dari prefab/primitif.
            Player = player != null ? player : FindAnyObjectByType<PlayerMotor>();
            if (Player == null) Player = PlayerMotor.Spawn(new Vector3(2.5f, 1.2f, 0f));
            PlayerMotor.EnsureComponents(Player.gameObject);
            Thread = Player.GetComponent<ThreadController>();
            Health = Player.GetComponent<PlayerHealth>();

            // Urutan alur: performa -> DDA -> parameter -> PCG dan AI.
            Tracker = GetOrAdd<PerformanceTracker>();
            Dda = GetOrAdd<DDAController>();
            Level = GetOrAdd<LevelBuilder>();
            Level.player = Player;
            Level.thread = Thread;
            Spawner = GetOrAdd<EnemySpawner>();
            Spawner.Bind(Level, Health);

            var kill = killZone != null ? killZone : KillZone.FindOrCreate();
            kill.player = Health;
            kill.killHeight = Level.killHeight;

            GetOrAdd<DebugTools.DebugHud>().thread = Thread;
            // HUD pemain selalu tampil (F1 hanya menyembunyikan HUD debug).
            var hud = GetOrAdd<UI.PlayerHud>();
            hud.health = Health;
            hud.thread = Thread;
            GetOrAdd<DebugTools.DemoHotkeys>();
            CameraRig.SideFollowCamera.Attach(Player.transform);

            // Parameter awal DDA diterapkan sebelum chunk pertama dirakit.
            Dda.Initialize();

            // Prioritas seed: permintaan Title Screen, lalu acak, lalu startSeed.
            int seed;
            if (!GameLaunch.TryTakeSeed(out seed)) seed = randomStartSeed ? _seedRng.Next(1000, 99999) : startSeed;
            BuildLevel(seed, 1);
        }

        /// <summary>Bangun level dari seed; riwayat performa dan DDA tetap berlanjut antar level.</summary>
        public void BuildLevel(int seed, int levelNumber)
        {
            CancelInvoke(nameof(NextLevel));
            _levelComplete = false;
            Player.InputEnabled = true;
            Tracker.BeginLevel();
            Health.ResetFull();
            Level.BuildLevel(seed, levelNumber);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraRig.SideFollowCamera>() : null;
            if (cam != null) cam.Snap();
            Flash("LEVEL " + levelNumber + "  |  SEED " + seed);
        }

        // ---------------- Game loop ----------------

        public float nextLevelDelay = 2.5f;
        [Tooltip("Scene Title Screen yang dibuka saat menekan Esc.")]
        public string titleSceneName = "TitleScreen";
        bool _levelComplete;
        string _message = "";
        float _messageUntil;
        GUIStyle _messageStyle;
        readonly System.Random _seedRng = new System.Random();

        void Update()
        {
            // Esc: kembali ke Title Screen (bila scene-nya ada di Build Settings).
            if (InputReader.KeyDown(Key.Escape) && Application.CanStreamedLevelBeLoaded(titleSceneName))
                SceneManager.LoadScene(titleSceneName);
        }

        void OnEnable() => GameEvents.GoalReached += OnGoalReached;
        void OnDisable() => GameEvents.GoalReached -= OnGoalReached;

        void OnGoalReached()
        {
            if (_levelComplete) return;
            _levelComplete = true;
            Player.InputEnabled = false;
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
