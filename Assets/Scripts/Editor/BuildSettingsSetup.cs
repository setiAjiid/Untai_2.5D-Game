using System.Collections.Generic;
using UnityEditor;

namespace Untai.EditorTools
{
    /// <summary>Memastikan scene utama Untai_Main berada di urutan pertama Build Settings.</summary>
    [InitializeOnLoad]
    public static class BuildSettingsSetup
    {
        const string MainScene = "Assets/Scenes/Untai_Main.unity";

        static BuildSettingsSetup()
        {
            EditorApplication.delayCall += EnsureMainScene;
        }

        [MenuItem("Untai/Add Main Scene To Build Settings")]
        public static void EnsureMainScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null) return;
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Count > 0 && scenes[0].path == MainScene) return;
            scenes.RemoveAll(s => s.path == MainScene);
            scenes.Insert(0, new EditorBuildSettingsScene(MainScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
