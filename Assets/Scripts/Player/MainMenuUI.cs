using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    public GameObject mainMenuPanel;
    public GameObject howToPlayPanel;

    void Start()
    {
        mainMenuPanel.SetActive(true);
        howToPlayPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void ShowHowToPlay()
    {
        mainMenuPanel.SetActive(false);
        howToPlayPanel.SetActive(true);
    }

    public void BackToMenu()
    {
        howToPlayPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
