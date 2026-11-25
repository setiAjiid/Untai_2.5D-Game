using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Untai.Core;

namespace Untai.EditorTools
{
    /// <summary>
    /// Menu Untai > Fix Title Wiring: memperbaiki sambungan Title Screen lewat API Unity
    /// (aman dipakai bila scene sempat tertimpa dari Editor yang terbuka):
    /// - TitleManager.mainSceneName = Untai_Main.
    /// - GameDirector di Untai_Main memakai randomStartSeed = true.
    /// - Urutan Build Settings: TitleScreen, lalu Untai_Main.
    /// </summary>
    public static class TitleWiring
    {
        public const string TitleScenePath = "Assets/Scenes/TitleScreen.unity";
        public const string MainScenePath = "Assets/Scenes/Untai_Main.unity";

        [MenuItem("Untai/Fix Title Wiring")]
        public static void Fix()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;

            var main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var director = Object.FindAnyObjectByType<GameDirector>();
            if (director != null)
            {
                director.randomStartSeed = true;
                EditorUtility.SetDirty(director);
                EditorSceneManager.SaveScene(main);
            }
            else Debug.LogWarning("[Untai] GameDirector tidak ditemukan di " + MainScenePath);

            var title = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            var manager = Object.FindAnyObjectByType<TitleManager>();
            if (manager != null)
            {
                manager.mainSceneName = TitleManager.DefaultMainScene;
                OnTitleManagerFound(manager);
                EditorUtility.SetDirty(manager);
                EditorSceneManager.SaveScene(title);
            }
            else Debug.LogWarning("[Untai] TitleManager tidak ditemukan di " + TitleScenePath);

            BuildSettingsSetup.EnsureSceneOrder();

            if (!string.IsNullOrEmpty(previous) && previous != TitleScenePath)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("[Untai] Title Screen tersambung ke Untai_Main.");
        }

        /// <summary>Langkah tambahan pada TitleManager (diisi langkah berikutnya, misal kolom seed).</summary>
        static void OnTitleManagerFound(TitleManager manager) { }
    }
}
