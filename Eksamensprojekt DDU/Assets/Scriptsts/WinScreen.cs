using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Win screen der vises når spilleren bruger udgangsdøren.
///
/// OPSÆTNING:
///   1. Opret et Canvas (Screen Space - Overlay).
///   2. Lav et panel der dækker hele skærmen.
///   3. Tilføj WinScreen.cs til Canvas-objektet.
///   4. Forbind UI-elementerne i Inspector.
///   5. Sæt Canvas til inaktivt (deaktivér) ved spilstart — Show() aktiverer det.
/// </summary>
public class WinScreen : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Tekst der viser spillerens totale score.")]
    public TMP_Text scoreText;

    [Tooltip("Tekst der vises over scoren — f.eks. 'Skift afsluttet!'")]
    public TMP_Text titleText;

    [Tooltip("Knap til at genstarte scenen.")]
    public Button restartButton;

    [Tooltip("Knap til at afslutte spillet.")]
    public Button quitButton;

    [Header("Indstillinger")]
    [Tooltip("Tekst på titlen.")]
    public string titleMessage = "SKIFT AFSLUTTET";

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        // Sæt knap-events op
        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        // Canvas starter skjult
        gameObject.SetActive(false);
    }

    /// <summary>Viser win screen med den aktuelle score.</summary>
    public void Show()
    {
        gameObject.SetActive(true);

        if (ScoreManager.Instance == null)
        {
            Debug.LogError("ScoreManager mangler!");
            return;
        }

        int total = ScoreManager.Instance.TotalScore;

        Debug.Log($"Score fundet: {total}");

        if (titleText != null)
            titleText.text = titleMessage;

        if (scoreText != null)
            scoreText.text = $"TOTAL SCORE\n{total} PT";

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Time.timeScale = 0f;
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Quit()
    {
        Time.timeScale = 1f;
        Application.Quit();
        Debug.Log("[WinScreen] Afslutter spillet.");
    }
}