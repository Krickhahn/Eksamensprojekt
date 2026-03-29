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

    [Tooltip("Knap til at gå til hovedmenuen.")]
    public Button mainMenuButton;

    [Header("Indstillinger")]
    [Tooltip("Tekst på titlen.")]
    public string titleMessage = "SKIFT AFSLUTTET";

    [Tooltip("Navnet på din Main Menu scene — skal matche præcist i Build Settings.")]
    public string mainMenuSceneName = "MainMenu";

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        // Sæt knap-events op
        if (restartButton != null)
            restartButton.onClick.AddListener(Restart);

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);

        // Canvas starter skjult
        gameObject.SetActive(false);
    }

    /// <summary>Viser win screen med den aktuelle score.</summary>
    public void Show()
    {
        gameObject.SetActive(true);

        // Vis score
        int total = ScoreManager.Instance != null ? ScoreManager.Instance.TotalScore : 0;

        if (titleText != null)
            titleText.text = titleMessage;

        string phase = "";
        if (ShiftTimer.Instance != null)
        {
            phase = ShiftTimer.Instance.Phase switch
            {
                ShiftTimer.ShiftPhase.Normal => "Afsluttet tidligt",
                ShiftTimer.ShiftPhase.Pressure => "Afsluttet i god tid",
                ShiftTimer.ShiftPhase.Overtime => "Afsluttet i overtid",
                ShiftTimer.ShiftPhase.Ended => "Skiftet udløb",
                _ => ""
            };
        }

        if (scoreText != null)
            scoreText.text = $"TOTAL SCORE\n{total} PT\n{phase}";

        // Lås musen op så spilleren kan klikke på knapperne
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Sæt tid til pause (valgfrit — fjern hvis du ikke vil fryse spillet)
        Time.timeScale = 0f;

        Debug.Log($"[WinScreen] Vist med score: {total}");
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void Quit()
    {
        Time.timeScale = 1f;
        Application.Quit();
        Debug.Log("[WinScreen] Afslutter spillet.");
    }
}