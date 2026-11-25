using UnityEngine;
using UnityEngine.SceneManagement;
using Untai.Core;

/// <summary>
/// Tombol Title Screen. Keduanya membuka scene Untai_Main:
/// - Mulai (GoToMainScene): seed acak.
/// - Mulai dengan seed (GoToSetSeedScene): seed tetap fixedSeed.
/// </summary>
public class TitleManager : MonoBehaviour
{
    public const string DefaultMainScene = "Untai_Main";

    [Header("Scene")]
    public string mainSceneName = DefaultMainScene;

    [Header("Seed")]
    [Tooltip("Seed untuk tombol Mulai dengan seed.")]
    public int fixedSeed = 12345;

    public void GoToMainScene()
    {
        GameLaunch.Clear(); // GameDirector di Untai_Main memakai randomStartSeed
        LoadMain();
    }

    /// <summary>Nama lama dipertahankan karena tombol di scene memanggil metode ini.</summary>
    public void GoToSetSeedScene()
    {
        GameLaunch.RequestSeed(fixedSeed);
        LoadMain();
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    void LoadMain()
    {
        // Jaga-jaga bila nama di Inspector salah atau scene lama sudah dihapus.
        string scene = Application.CanStreamedLevelBeLoaded(mainSceneName) ? mainSceneName : DefaultMainScene;
        SceneManager.LoadScene(scene);
    }
}
