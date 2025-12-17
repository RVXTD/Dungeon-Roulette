using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathScreenUI : MonoBehaviour
{
    public string mainSceneName = "SampleScene";

    public void TryAgain()
    {
        SceneManager.LoadScene(mainSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
