using System.Collections.Generic;
using UnityEngine;

namespace Untai.Core
{
    /// <summary>
    /// Utilitas pembuat primitif berwarna saat runtime (pengganti aset sementara).
    /// Material di-cache per warna agar tidak membuat material baru tiap objek.
    /// </summary>
    public static class Prims
    {
        static readonly Dictionary<Color, Material> s_Materials = new Dictionary<Color, Material>();
        static Font s_Font;

        public static Material Mat(Color color)
        {
            if (s_Materials.TryGetValue(color, out var m) && m != null) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader) { name = "Untai_" + ColorUtility.ToHtmlStringRGB(color) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            s_Materials[color] = m;
            return m;
        }

        public static GameObject Create(PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Color color, Transform parent, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            // DestroyImmediate agar juga aman dipakai generator prefab di mode Edit.
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Instansiasi prefab pada posisi dunia tanpa akhiran "(Clone)".</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Transform parent)
        {
            var go = Object.Instantiate(prefab, position, prefab.transform.rotation, parent);
            go.name = prefab.name;
            return go;
        }

        /// <summary>Material dari Inspector bila ada, jika tidak material runtime berwarna fallback.</summary>
        public static Material MatOr(Material assigned, Color fallback) => assigned != null ? assigned : Mat(fallback);

        /// <summary>Kotak dengan pusat dan ukuran dunia.</summary>
        public static GameObject Box(string name, Vector3 center, Vector3 size, Color color, Transform parent,
            bool keepCollider = true)
        {
            return Create(PrimitiveType.Cube, name, center, size, color, parent, keepCollider);
        }

        /// <summary>Teks 3D sederhana (TextMesh) untuk label di dunia.</summary>
        public static TextMesh Label(string text, Vector3 position, Transform parent, Color color, float size = 0.12f)
        {
            if (s_Font == null) s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var tm = go.AddComponent<TextMesh>();
            tm.font = s_Font;
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = s_Font.material;
            return tm;
        }
    }
}
