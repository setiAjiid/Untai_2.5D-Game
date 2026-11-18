using System.Collections.Generic;
using UnityEngine;
using Untai.Core;

namespace Untai.Thread
{
    /// <summary>Titik jangkar untuk mengayun. Semua jangkar aktif terdaftar di All.</summary>
    public class Anchor : MonoBehaviour
    {
        public static readonly List<Anchor> All = new List<Anchor>();

        static readonly Color IdleColor = new Color(0.55f, 0.75f, 1f);
        static readonly Color HighlightColor = new Color(1f, 1f, 0.3f);
        Renderer _renderer;
        bool _highlighted;

        public static Anchor Create(Vector3 position, Transform parent)
        {
            var go = Prims.Create(PrimitiveType.Sphere, "Anchor", position, Vector3.one * 0.6f, IdleColor, parent, false);
            var a = go.AddComponent<Anchor>();
            a._renderer = go.GetComponent<Renderer>();
            return a;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void SetHighlight(bool on)
        {
            if (_highlighted == on || _renderer == null) return;
            _highlighted = on;
            _renderer.sharedMaterial = Prims.Mat(on ? HighlightColor : IdleColor);
        }
    }
}
