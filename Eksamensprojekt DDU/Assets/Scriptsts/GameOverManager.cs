using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Forbedret Game Over Manager med fade-ind, slow-motion, statistik,
/// tilfældige overlevelsestips, og knapper til Restart, Main Menu og Quit.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Opret et UI Canvas med følgende struktur:
///
///      Canvas
///      └── GameOverPanel (CanvasGroup, Image – sort baggrund, alpha 0 ved start)
///          ├── TitleText          (TMP_Text – "GAME OVER")
///          ├── SubtitleText       (TMP_Text – vises under titlen, f.eks. "Caught by the angel")
///          ├── StatsText          (TMP_Text – viser overlevelsestid)
///          ├── TipText            (TMP_Text – tilfældigt tip)
///          ├── ButtonGroup        (GameObject der holder knapperne)
///          │   ├── RestartButton  (Button)
///          │   ├── MenuButton     (Button)
///          │   └── QuitButton     (Button)
///          └── VersionText        (TMP_Text – valgfrit, viser build-version)
///
///   3. Træk alle referencer ind i Inspector.
///   4. Sæt "Main Menu Scene Name" til navnet på din main menu scene.
///   5. Knapperne forbindes automatisk i Start() — du behøver ikke sætte OnClick i Inspector.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    // ─────────────────────────────────────────────
    //  UI REFERENCER
    // ─────────────────────────────────────────────
    [Header("Panels")]
    [Tooltip("Rod-CanvasGroup der fader ind ved game over. Sæt alpha=0 og deaktiver ved start.")]
    public CanvasGroup gameOverPanel;

    [Header("Tekst")]
    [Tooltip("Stor overskrift – f.eks. 'GAME OVER'")]
    public TMP_Text titleText;

    [Tooltip("Undertitel – f.eks. årsagen til game over")]
    public TMP_Text subtitleText;

    [Tooltip("Viser spilletid og eventuelt andre stats")]
    public TMP_Text statsText;

    [Tooltip("Viser et tilfældigt overlevelsestip")]
    public TMP_Text tipText;

    [Header("Knapper")]
    public Button restartButton;
    public Button menuButton;
    public Button quitButton;

    [Header("Knaptekster (valgfrit – udfyldes automatisk hvis tomt)")]
    public string restartLabel = "Prøv igen";
    public string menuLabel = "Hovedmenu";
    public string quitLabel = "Afslut";

    // ─────────────────────────────────────────────
    //  INDSTILLINGER
    // ─────────────────────────────────────────────
    [Header("Timing")]
    [Tooltip("Sekunder fra game over til knapperne vises (giver fade-ind tid)")]
    public float buttonDelay = 1.2f;

    [Tooltip("Sekunder det tager at fade panelet ind")]
    public float fadeDuration = 1.5f;

    [Tooltip("Sekunder slow-motion varer inden spillet pauses helt")]
    public float slowMoDuration = 0.6f;

    [Header("Slow-motion")]
    [Tooltip("Tidsskala under slow-motion-effekten (0.05 = meget langsomt)")]
    [Range(0.01f, 0.5f)]
    public float slowMoTimeScale = 0.08f;

    [Header("Scene")]
    [Tooltip("Eksakt navn på din main menu scene (som i Build Settings)")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Lyd")]
    public AudioClip gameOverSound;
    public AudioSource audioSource;

    // ─────────────────────────────────────────────
    //  OVERLEVELSESTIPS
    // ─────────────────────────────────────────────
    [Header("Overlevelsestips")]
    [Tooltip("Tilfældigt tip vises ved game over. Tilføj dine egne hints her.")]
    public string[] survivalTips = new string[]
    {
        "Tip: Hold øjnene på englen – den bevæger sig kun når du ikke kigger.",
        "Tip: Papkassen kan redde dit liv – brug den klogt.",
        "Tip: Lyt efter bevægelyd. Stilhed er ikke altid sikkert.",
        "Tip: Englen er hurtigere end den ser ud. Hold afstand.",
        "Tip: Kig ud af kassen med forsigtighed – englen kan stadig se dig.",
    };

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    private bool _triggered;
    private float _sessionStartTime;

    // ─────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (gameOverPanel != null)
        {
            gameOverPanel.alpha = 0f;
            gameOverPanel.interactable = false;
            gameOverPanel.blocksRaycasts = false;
            gameOverPanel.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        _sessionStartTime = Time.time;

        // Sæt knap-labels
        SetButtonLabel(restartButton, restartLabel);
        SetButtonLabel(menuButton, menuLabel);
        SetButtonLabel(quitButton, quitLabel);

        // Tilslut knap-events automatisk
        restartButton?.onClick.AddListener(Restart);
        menuButton?.onClick.AddListener(GoToMainMenu);
        quitButton?.onClick.AddListener(QuitGame);

        // Skjul knapper til game over trigges
        SetButtonGroupVisible(false);
    }

    // ─────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Trigges af WeepingAngelEnemy når spilleren fanges.
    /// subtitle kan bruges til at vise årsagen, f.eks. "Caught by the angel".
    /// </summary>
    public void TriggerGameOver(string subtitle = "Du blev fanget...")
    {
        if (_triggered) return;
        _triggered = true;

        Debug.Log("[GameOverManager] Game Over!");

        if (audioSource != null && gameOverSound != null)
            audioSource.PlayOneShot(gameOverSound);

        StartCoroutine(GameOverSequence(subtitle));
    }

    // ─────────────────────────────────────────────
    //  SEKVENS
    // ─────────────────────────────────────────────
    IEnumerator GameOverSequence(string subtitle)
    {
        // ── Slow-motion-effekt ────────────────────
        Time.timeScale = slowMoTimeScale;
        Time.fixedDeltaTime = 0.02f * slowMoTimeScale;

        yield return new WaitForSecondsRealtime(slowMoDuration);

        // Pause spillet helt
        Time.timeScale = 0f;

        // ── Frigiv cursor ─────────────────────────
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ── Udfyld UI ─────────────────────────────
        if (subtitleText != null)
            subtitleText.text = subtitle;

        if (statsText != null)
        {
            float survived = Time.time - _sessionStartTime;

            int baseScore = ScoreManager.Instance != null ? ScoreManager.Instance.TotalScore : 0;
            float multiplier = ShiftTimer.Instance != null ? ShiftTimer.Instance.ScoreMultiplier : 1f;
            int finalScore = Mathf.RoundToInt(baseScore * multiplier);

            string phase = "";
            string bonus = "";
            if (ShiftTimer.Instance != null)
            {
                switch (ShiftTimer.Instance.Phase)
                {
                    case ShiftTimer.ShiftPhase.Normal:
                        phase = "Afsluttet tidligt";
                        bonus = $"Tidlig bonus: x{multiplier:F1}";
                        break;
                    case ShiftTimer.ShiftPhase.Pressure:
                        phase = "Afsluttet i god tid";
                        bonus = $"Slut-multiplikator: x{multiplier:F1}";
                        break;
                    case ShiftTimer.ShiftPhase.Overtime:
                        phase = "Afsluttet i overtid";
                        bonus = $"Overtidsstraf: x{multiplier:F2}";
                        break;
                    case ShiftTimer.ShiftPhase.Ended:
                        phase = "Skiftet udløb";
                        bonus = "Ingen bonus";
                        break;
                }
            }

            statsText.text = $"Du overlevede i {FormatTime(survived)}\n\n" +
                             $"{phase}\n" +
                             $"Score: {baseScore} PT\n" +
                             $"{bonus}\n\n" +
                             $"FINAL: {finalScore} PT";
        }

        if (tipText != null && survivalTips.Length > 0)
            tipText.text = survivalTips[Random.Range(0, survivalTips.Length)];

        // ── Fade panel ind ────────────────────────
        if (gameOverPanel != null)
        {
            gameOverPanel.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                gameOverPanel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            gameOverPanel.alpha = 1f;
        }

        // ── Vent lidt inden knapper vises ─────────
        yield return new WaitForSecondsRealtime(buttonDelay);

        gameOverPanel.interactable = true;
        gameOverPanel.blocksRaycasts = true;
        SetButtonGroupVisible(true);
    }

    // ─────────────────────────────────────────────
    //  KNAP-HANDLINGER
    // ─────────────────────────────────────────────
    public void Restart()
    {
        ResetTimeScale();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        ResetTimeScale();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        ResetTimeScale();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────
    //  HJÆLPEMETODER
    // ─────────────────────────────────────────────
    void ResetTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    void SetButtonGroupVisible(bool visible)
    {
        if (restartButton != null) restartButton.gameObject.SetActive(visible);
        if (menuButton != null) menuButton.gameObject.SetActive(visible);
        if (quitButton != null) quitButton.gameObject.SetActive(visible);
    }

    void SetButtonLabel(Button btn, string label)
    {
        if (btn == null || string.IsNullOrEmpty(label)) return;
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = label;
    }

    string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return m > 0 ? $"{m}m {s:00}s" : $"{s} sekunder";
    }

    // ─────────────────────────────────────────────
    //  SIKKERHED: nulstil timescale hvis scriptet
    //  destrueres mens spillet er pauset
    // ─────────────────────────────────────────────
    void OnDestroy()
    {
        if (Time.timeScale == 0f)
            ResetTimeScale();
    }
}