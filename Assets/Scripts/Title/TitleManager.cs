using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    [Header("Scene Names")]
    public string mainSceneName = "MainScene";
    public string setSeedSceneName = "SetSeedScene";

    public void GoToMainScene()
    {
        SceneManager.LoadScene(mainSceneName);
    }

    public void GoToSetSeedScene()
    {
        SceneManager.LoadScene(setSeedSceneName);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}