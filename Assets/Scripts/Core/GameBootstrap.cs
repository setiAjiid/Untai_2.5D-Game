using UnityEngine;

namespace Untai.Core
{
    public static class GameBootstrap
    {
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