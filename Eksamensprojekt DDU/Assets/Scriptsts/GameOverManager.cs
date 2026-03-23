using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Viser game over skærm og genstarter scenen når WeepingAngel fanger spilleren.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Opret et UI Canvas med:
///      - Et sort Image der dækker hele skærmen (gameOverPanel) — sæt alpha til 0 ved start
///      - Et Text eller TMP_Text felt med "GAME OVER" (gameOverText) — sæt alpha til 0 ved start
///   3. Træk disse ind i Inspector-felterne.
///   4. Juster restartDelay til hvor lang tid game over skærmen vises.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("Panel der fader til sort ved game over. Sæt Image alpha til 0 ved start.")]
    public CanvasGroup gameOverPanel;

    [Tooltip("Sekunder game over skærmen vises inden scene genstartes.")]
    public float restartDelay = 3f;

    [Tooltip("Sekunder det tager at fade ind til game over skærmen.")]
    public float fadeDuration = 1f;

    [Header("Lyd (valgfrit)")]
    public AudioClip gameOverSound;
    public AudioSource audioSource;

    private bool _triggered = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Sørg for at panelet er usynligt ved start
        if (gameOverPanel != null)
        {
            gameOverPanel.alpha = 0f;
            gameOverPanel.gameObject.SetActive(false);
        }
    }

    /// <summary>Kaldes af WeepingAngelEnemy når spilleren fanges.</summary>
    public void TriggerGameOver()
    {
        if (_triggered) return;
        _triggered = true;

        Debug.Log("[GameOverManager] Game Over!");

        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);

        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        // Lås cursor fri
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Fade til game over skærm
        if (gameOverPanel != null)
        {
            gameOverPanel.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                gameOverPanel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            gameOverPanel.alpha = 1f;
        }

        yield return new WaitForSeconds(restartDelay);

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}