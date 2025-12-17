using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathTransition : MonoBehaviour
{
    public static DeathTransition Instance { get; private set; }

    [Header("Fade")]
    public CanvasGroup fadeGroup;
    public float fadeDuration = 1.0f;

    [Header("Scenes")]
    public string deathSceneName = "DeathScreen";

    private bool transitioning;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Optional: keep this across scenes
        DontDestroyOnLoad(gameObject);

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }
    }

    public void PlayerDied()
    {
        if (transitioning) return;
        StartCoroutine(FadeToDeathScene());
    }

    private IEnumerator FadeToDeathScene()
    {
        transitioning = true;

        yield return Fade(1f); // fade to black
        SceneManager.LoadScene(deathSceneName);
        yield return null;

        // After loading, try to find a fadeGroup in the new scene (optional)
        // If you keep the same CanvasGroup across scenes, you can skip this.
        yield return Fade(1f); // stay black for a frame

        transitioning = false;
    }

    public IEnumerator Fade(float targetAlpha)
    {
        if (fadeGroup == null) yield break;

        fadeGroup.blocksRaycasts = true;

        float startAlpha = fadeGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = targetAlpha;

        if (Mathf.Approximately(targetAlpha, 0f))
            fadeGroup.blocksRaycasts = false;
    }
}
