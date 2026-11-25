using TMPro;
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
    /// - Kolom input seed (TMP_InputField) dibuat di atas tombol bila belum ada.
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

        /// <summary>Buat kolom input seed di Canvas bila TitleManager belum punya.</summary>
        static void OnTitleManagerFound(TitleManager manager)
        {
            if (manager.seedInput != null) return;
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) { Debug.LogWarning("[Untai] Canvas tidak ditemukan; kolom seed dilewati."); return; }

            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "SeedInput";
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -50f); // di atas tombol Start (y -158)
            rt.sizeDelta = new Vector2(300f, 50f);

            var input = go.GetComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.characterLimit = 9;
            if (input.placeholder is TMP_Text placeholder) placeholder.text = "Seed (kosong = acak)";
            if (input.textComponent != null) input.textComponent.fontSize = 22;
            manager.seedInput = input;
            Undo.RegisterCreatedObjectUndo(go, "Create Seed Input");
        }
    }
}
