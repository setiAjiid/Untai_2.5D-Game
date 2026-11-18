using UnityEngine;
using Untai.Player;

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
            // Sandbox sementara: lantai datar untuk menguji gerak.
            var root = new GameObject("Sandbox").transform;
            Prims.Box("Ground", new Vector3(10f, -0.5f, 0f), new Vector3(40f, 1f, 3f), new Color(0.35f, 0.4f, 0.45f), root);
            Prims.Box("Step", new Vector3(8f, 0.75f, 0f), new Vector3(3f, 1.5f, 3f), new Color(0.35f, 0.4f, 0.45f), root);
            var player = PlayerMotor.CreatePlayer(new Vector3(0f, 1.5f, 0f));
            player.gameObject.AddComponent<Thread.ThreadController>();
            player.gameObject.AddComponent<Thread.ThreadRenderer>();
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
