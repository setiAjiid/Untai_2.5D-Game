using System.Collections.Generic;
using UnityEditor;

namespace Untai.EditorTools
{
    /// <summary>
    /// Menjaga urutan Build Settings: TitleScreen di indeks 0, Untai_Main berikutnya.
    /// Scene yang sudah tidak ada di disk dibuang dari daftar.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildSettingsSetup
    {
        const string TitleScene = "Assets/Scenes/TitleScreen.unity";
        const string MainScene = "Assets/Scenes/Untai_Main.unity";

        static BuildSettingsSetup()
        {
            EditorApplication.delayCall += EnsureSceneOrder;
        }

        [MenuItem("Untai/Fix Build Settings Scene Order")]
        public static void EnsureSceneOrder()
        {
            var current = EditorBuildSettings.scenes;
            var scenes = new List<EditorBuildSettingsScene>(current.Length + 2);
            AddIfExists(scenes, TitleScene);
            AddIfExists(scenes, MainScene);
            foreach (var s in current)
            {
                if (s.path == TitleScene || s.path == MainScene) continue;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) == null) continue; // scene sudah dihapus
                scenes.Add(s);
            }

            if (SameOrder(current, scenes)) return;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void AddIfExists(List<EditorBuildSettingsScene> list, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                list.Add(new EditorBuildSettingsScene(path, true));
        }

        static bool SameOrder(EditorBuildSettingsScene[] a, List<EditorBuildSettingsScene> b)
        {
            if (a.Length != b.Count) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].path != b[i].path || a[i].enabled != b[i].enabled) return false;
            return true;
        }
    }
}
