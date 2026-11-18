using UnityEngine;
using Untai.Data;
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
            // Sandbox sementara: lantai, lubang, checkpoint, dan goal untuk menguji mekanik.
            var root = new GameObject("Sandbox").transform;
            var grey = new Color(0.35f, 0.4f, 0.45f);
            Prims.Box("Ground A", new Vector3(5f, -0.5f, 0f), new Vector3(14f, 1f, 3f), grey, root);
            Prims.Box("Ground B", new Vector3(20f, -0.5f, 0f), new Vector3(12f, 1f, 3f), grey, root);
            Prims.Box("Ground C", new Vector3(36f, -0.5f, 0f), new Vector3(10f, 1f, 3f), grey, root);

            var player = PlayerMotor.CreatePlayer(new Vector3(0f, 1.5f, 0f));
            var thread = player.gameObject.AddComponent<ThreadController>();
            player.gameObject.AddComponent<ThreadRenderer>();
            var health = player.gameObject.AddComponent<PlayerHealth>();
            player.gameObject.AddComponent<ThreadSwing>();

            Checkpoint.Create(new Vector3(-1f, 0f, 0f), GameplayParameters.Active.threadLength, root).Activate(thread, true);
            Checkpoint.Create(new Vector3(16f, 0f, 0f), GameplayParameters.Active.threadLength, root);
            Anchor.Create(new Vector3(28.5f, 4.5f, 0f), root);
            Goal.Create(new Vector3(38f, 0f, 0f), root);
            var kill = new GameObject("KillZone").AddComponent<KillZone>();
            kill.player = health;

            CameraRig.SideFollowCamera.Attach(player.transform);
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
