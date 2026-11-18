using UnityEngine;

namespace Untai.Core
{
    /// <summary>
    /// Bootstrap otomatis: scene apa pun yang dijalankan dengan Play langsung berisi game.
    /// Jika scene sudah memiliki GameDirector (misal Untai_Main), objek itulah yang dipakai.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (Object.FindAnyObjectByType<GameDirector>() != null) return;
            new GameObject("Untai Game").AddComponent<GameDirector>();
        }

        public static void EnsureLight()
        {
            if (Object.FindAnyObjectByType<Light>() != null) return;
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            go.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
    }
}
