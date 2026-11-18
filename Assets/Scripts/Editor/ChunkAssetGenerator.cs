using System.IO;
using UnityEditor;
using UnityEngine;
using Untai.Data;
using Untai.PCG;

namespace Untai.EditorTools
{
    /// <summary>
    /// Membuat aset chunk contoh dan aset GameplayParameters di Assets/Data/Resources/Untai.
    /// Dijalankan otomatis sekali saat Editor memuat proyek jika aset belum ada,
    /// dan bisa dipanggil manual lewat menu Untai.
    /// </summary>
    [InitializeOnLoad]
    public static class ChunkAssetGenerator
    {
        const string DataRoot = "Assets/Data/Resources/Untai";
        const string ChunkFolder = DataRoot + "/TandemChunks";

        static ChunkAssetGenerator()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!AssetDatabase.IsValidFolder(ChunkFolder) ||
                    AssetDatabase.FindAssets("t:ChunkDefinition", new[] { ChunkFolder }).Length == 0)
                    GenerateSampleChunks();
                if (AssetDatabase.LoadAssetAtPath<GameplayParameters>(DataRoot + "/GameplayParameters.asset") == null)
                    CreateParametersAsset();
            };
        }

        [MenuItem("Untai/Generate Sample Chunks")]
        public static void GenerateSampleChunks()
        {
            Directory.CreateDirectory(ChunkFolder);
            foreach (var def in ChunkLibrary.CreateDefaults())
            {
                string path = ChunkFolder + "/" + def.id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<ChunkDefinition>(path);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(def, existing);
                    existing.name = def.id;
                    EditorUtility.SetDirty(existing);
                }
                else AssetDatabase.CreateAsset(def, path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Untai] Chunk contoh dibuat di " + ChunkFolder);
        }

        [MenuItem("Untai/Create Gameplay Parameters Asset")]
        public static void CreateParametersAsset()
        {
            Directory.CreateDirectory(DataRoot);
            string path = DataRoot + "/GameplayParameters.asset";
            if (AssetDatabase.LoadAssetAtPath<GameplayParameters>(path) != null) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameplayParameters>(), path);
            AssetDatabase.SaveAssets();
        }
    }
}
