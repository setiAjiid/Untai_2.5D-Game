using UnityEngine;
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
    public class GameDirector : MonoBehaviour
    {
        public int startSeed = 12345;

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
        }

        void Start()
        {
            GameBootstrap.EnsureLight();

            // Pemain dan benang.
            Player = PlayerMotor.CreatePlayer(new Vector3(2.5f, 1.2f, 0f));
            Thread = Player.gameObject.AddComponent<ThreadController>();
            Player.gameObject.AddComponent<ThreadRenderer>();
            Health = Player.gameObject.AddComponent<PlayerHealth>();
            Player.gameObject.AddComponent<ThreadSwing>();

            // Urutan alur: performa -> DDA -> parameter -> PCG dan AI.
            Tracker = gameObject.AddComponent<PerformanceTracker>();
            Dda = gameObject.AddComponent<DDAController>();
            Level = gameObject.AddComponent<LevelBuilder>();
            Level.player = Player;
            Level.thread = Thread;
            Spawner = gameObject.AddComponent<EnemySpawner>();
            Spawner.Bind(Level, Health);

            var kill = gameObject.AddComponent<KillZone>();
            kill.player = Health;
            kill.killHeight = Level.killHeight;

            gameObject.AddComponent<DebugTools.DebugHud>().thread = Thread;
            CameraRig.SideFollowCamera.Attach(Player.transform);

            // Parameter awal DDA diterapkan sebelum chunk pertama dirakit.
            Dda.Initialize();
            BuildLevel(startSeed, 1);
        }

        /// <summary>Bangun level dari seed; riwayat performa dan DDA tetap berlanjut antar level.</summary>
        public void BuildLevel(int seed, int levelNumber)
        {
            Tracker.BeginLevel();
            Health.ResetFull();
            Level.BuildLevel(seed, levelNumber);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraRig.SideFollowCamera>() : null;
            if (cam != null) cam.Snap();
        }
    }
}
