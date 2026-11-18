using UnityEngine;
using Untai.Level;
using Untai.Player;
using Untai.Thread;

namespace Untai.Core
{
    /// <summary>
    /// Bootstrap otomatis: scene apa pun yang dijalankan dengan Play langsung berisi game.
    /// Jika scene sudah memiliki GameBootstrap, objek itulah yang dipakai.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("Untai Game").AddComponent<GameBootstrap>();
        }

        void Start()
        {
            EnsureLight();
            var player = PlayerMotor.CreatePlayer(new Vector3(2.5f, 1.2f, 0f));
            var thread = player.gameObject.AddComponent<ThreadController>();
            player.gameObject.AddComponent<ThreadRenderer>();
            var health = player.gameObject.AddComponent<PlayerHealth>();
            player.gameObject.AddComponent<ThreadSwing>();

            gameObject.AddComponent<DDA.PerformanceTracker>();
            var dda = gameObject.AddComponent<DDA.DDAController>();
            var level = gameObject.AddComponent<LevelBuilder>();
            level.player = player;
            level.thread = thread;
            gameObject.AddComponent<AI.EnemySpawner>().Bind(level, health);
            var kill = gameObject.AddComponent<KillZone>();
            kill.player = health;
            kill.killHeight = level.killHeight;
            gameObject.AddComponent<DebugTools.DebugHud>().thread = thread;
            CameraRig.SideFollowCamera.Attach(player.transform);

            dda.Initialize(); // parameter awal DDA diterapkan sebelum chunk pertama dirakit
            level.BuildLevel(12345, 1);
        }

        public static void EnsureLight()
        {
            if (FindAnyObjectByType<Light>() != null) return;
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
    }
}
