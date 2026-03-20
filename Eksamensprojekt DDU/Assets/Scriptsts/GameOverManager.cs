using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Håndterer game over når WeepingAngelEnemy fanger spilleren.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. (Valgfrit) Tilslut et Canvas med et sort billede til fadeImage
///      for en fade-to-black effekt inden scene reload.
///   3. (Valgfrit) Tilslut en AudioClip til gameOverSound.
///   4. Justér restartDelay til hvor lang tid der er inden scenen genstartes.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("Indstillinger")]
    [Tooltip("Sekunder inden scenen genstartes efter game over.")]
    public float restartDelay = 3f;

    [Header("Visuals (valgfrit)")]
    [Tooltip("Et UI Image der fader til sort ved game over. Sæt alpha til 0 ved start.")]
    public Image fadeImage;

    [Tooltip("Hvor hurtigt skærmen fader til sort.")]
    public float fadeDuration = 1f;

    [Header("Lyd (valgfrit)")]
    public AudioClip gameOverSound;
    public AudioSource audioSource;

    private bool _triggered = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Kaldes af WeepingAngelEnemy når spilleren fanges.
    /// </summary>
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
        // Lås spillerens input
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Fade til sort hvis fadeImage er sat op
        if (fadeImage != null)
        {
            float elapsed = 0f;
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadeImage.color = c;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(restartDelay);
        }

        // Genstart scenen
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}