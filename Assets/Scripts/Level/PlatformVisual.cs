using UnityEngine;
using Untai.Core;

namespace Untai.Level
{
    /// <summary>
    /// Tampilan platform per tingkat chunk (mudah, sedang, sulit). Dipasang pada prefab Platform;
    /// ganti material di Inspector untuk mengubah warnanya di game.
    /// </summary>
    public class PlatformVisual : MonoBehaviour
    {
        public static readonly Color[] DefaultTierColors =
        {
            new Color(0.42f, 0.55f, 0.45f), // mudah
            new Color(0.42f, 0.48f, 0.62f), // sedang
            new Color(0.58f, 0.42f, 0.6f),  // sulit
        };

        public Renderer targetRenderer;
        public Material easyMaterial;
        public Material mediumMaterial;
        public Material hardMaterial;

        public void SetTier(int tier)
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer == null) return;
            tier = Mathf.Clamp(tier, 0, 2);
            Material m = tier == 0 ? easyMaterial : tier == 1 ? mediumMaterial : hardMaterial;
            targetRenderer.sharedMaterial = Prims.MatOr(m, DefaultTierColors[tier]);
        }
    }
}
