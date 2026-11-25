using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Untai.Core;

/// <summary>
/// Tombol Title Screen. Keduanya membuka scene Untai_Main:
/// - Mulai (GoToMainScene): seed dari kolom input; kosong berarti seed acak.
/// - Mulai dengan seed (GoToSetSeedScene): seed dari kolom input; kosong berarti fixedSeed.
/// Kolom input bersifat opsional (dibuat lewat menu Untai > Fix Title Wiring).
/// </summary>
public class TitleManager : MonoBehaviour
{
    public const string DefaultMainScene = "Untai_Main";

    [Header("Scene")]
    public string mainSceneName = DefaultMainScene;

    [Header("Seed")]
    [Tooltip("Seed untuk tombol Mulai dengan seed.")]
    public int fixedSeed = 12345;
    [Tooltip("Kolom input seed (opsional).")]
    public TMP_InputField seedInput;

    public void GoToMainScene()
    {
        if (TryReadSeed(out int seed)) GameLaunch.RequestSeed(seed);
        else GameLaunch.Clear(); // GameDirector di Untai_Main memakai randomStartSeed
        LoadMain();
    }

    /// <summary>Nama lama dipertahankan karena tombol di scene memanggil metode ini.</summary>
    public void GoToSetSeedScene()
    {
        GameLaunch.RequestSeed(TryReadSeed(out int seed) ? seed : fixedSeed);
        LoadMain();
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    bool TryReadSeed(out int seed)
    {
        seed = 0;
        return seedInput != null && int.TryParse(seedInput.text.Trim(), out seed);
    }

    void LoadMain()
    {
        // Jaga-jaga bila nama di Inspector salah atau scene lama sudah dihapus.
        string scene = Application.CanStreamedLevelBeLoaded(mainSceneName) ? mainSceneName : DefaultMainScene;
        SceneManager.LoadScene(scene);
    }
}
