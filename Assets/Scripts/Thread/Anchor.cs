using System.Collections.Generic;
using UnityEngine;
using Untai.Core;
using Untai.Data;

namespace Untai.Thread
{
    /// <summary>Titik jangkar untuk mengayun. Semua jangkar aktif terdaftar di All.</summary>
    public class Anchor : MonoBehaviour
    {
        public static readonly List<Anchor> All = new List<Anchor>();

        public static readonly Color DefaultIdleColor = new Color(0.55f, 0.75f, 1f);
        public static readonly Color DefaultHighlightColor = new Color(1f, 1f, 0.3f);

        [Tooltip("Renderer yang berganti material saat jangkar bisa dikait.")]
        public Renderer targetRenderer;
        public Material idleMaterial;
        public Material highlightMaterial;

        bool _highlighted;

        /// <summary>Buat jangkar dari prefab PrefabLibrary, atau primitif bila prefab kosong.</summary>
        public static Anchor Create(Vector3 position, Transform parent)
        {
            var prefab = PrefabLibrary.Get(l => l.anchor);
            if (prefab != null)
            {
                var a = Prims.Spawn(prefab, position, parent).GetComponent<Anchor>();
                if (a != null) return a;
            }
            return CreatePrimitive(position, parent);
        }

        /// <summary>Versi primitif dari kode (fallback, juga dipakai generator prefab).</summary>
        public static Anchor CreatePrimitive(Vector3 position, Transform parent)
        {
            var go = Prims.Create(PrimitiveType.Sphere, "Anchor", position, Vector3.one * 0.6f, DefaultIdleColor, parent, false);
            var a = go.AddComponent<Anchor>();
            a.targetRenderer = go.GetComponent<Renderer>();
            return a;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetHighlight(bool on)
        {
            if (_highlighted == on || targetRenderer == null) return;
            _highlighted = on;
            targetRenderer.sharedMaterial = on
                ? Prims.MatOr(highlightMaterial, DefaultHighlightColor)
                : Prims.MatOr(idleMaterial, DefaultIdleColor);
        }
    }
}
