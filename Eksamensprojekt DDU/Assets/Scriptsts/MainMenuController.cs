using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────────────────

    [Header("═══ Main Folder")]
    public RectTransform mainFolder;
    public Button startButton;
    public Button optionsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("═══ Options Folder")]
    [Tooltip("Place this in the editor at the position where it should be VISIBLE (to the right of main folder).")]
    public RectTransform optionsFolder;
    public Button optionsBackButton;
    public Slider sensitivitySlider;
    public Slider volumeSlider;
    [Range(0.1f, 10f)] public float defaultSensitivity = 1f;
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Header("═══ Credits")]
    [Tooltip("The CreditsOverlay root — must be on its own Canvas at a high sort order.")]
    public RectTransform creditsOverlay;
    public VideoPlayer creditsVideoPlayer;
    public RawImage creditsRawImage;
    public Button creditsBackButton;

    [Header("═══ Input Blocker")]
    [Tooltip("A full-screen transparent Image that sits over the game world to block folder clicks while menu is open. Put it on MainMenuCanvas, stretched full screen, alpha=0, Raycast Target ON.")]
    public Image inputBlocker;

    [Header("═══ Animation")]
    [Tooltip("How far the main folder slides left when Start is pressed (pixels).")]
    public float slideOutDistance = 900f;
    [Range(0.1f, 2f)] public float slideOutDuration = 0.6f;
    public AnimationCurve slideOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Range(0.1f, 2f)] public float optionsSlideDuration = 0.45f;
    public AnimationCurve optionsSlideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Range(0.1f, 2f)] public float creditsFadeDuration = 0.4f;

    [Header("═══ Sensitivity Target")]
    public MonoBehaviour sensitivityTarget;
    public string sensitivityFieldName = "mouseSensitivity";

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private Vector2 _mainFolderStartPos;
    private Vector2 _optionsFolderVisiblePos;   // where it sits when open  (set from editor position)
    private Vector2 _optionsFolderHiddenPos;    // off to the LEFT, hidden

    private CanvasGroup _creditsCanvasGroup;
    private GraphicRaycaster _creditsRaycaster;

    private bool _menuActive = true;
    private bool _animating = false;

    private RenderTexture _creditsRenderTex;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // ── Main folder ──────────────────────────────────────────
        _mainFolderStartPos = mainFolder.anchoredPosition;

        // ── Options folder ───────────────────────────────────────
        // Visible pos = wherever you placed it in the editor (to the right of main folder)
        // Hidden pos  = off to the LEFT by its own width + a gap
        _optionsFolderVisiblePos = optionsFolder.anchoredPosition;
        _optionsFolderHiddenPos = _optionsFolderVisiblePos - new Vector2(optionsFolder.rect.width + 60f, 0f);
        optionsFolder.anchoredPosition = _optionsFolderHiddenPos;
        optionsFolder.gameObject.SetActive(false);

        // ── Credits overlay ──────────────────────────────────────
        // CreditsOverlay lives on its OWN canvas — we control visibility
        // via its CanvasGroup AND its GraphicRaycaster
        _creditsCanvasGroup = creditsOverlay.GetComponent<CanvasGroup>();
        if (_creditsCanvasGroup == null)
            _creditsCanvasGroup = creditsOverlay.gameObject.AddComponent<CanvasGroup>();

        _creditsRaycaster = creditsOverlay.GetComponentInParent<GraphicRaycaster>();

        SetCreditsVisible(false, instant: true);

        // ── Video render texture ─────────────────────────────────
        if (creditsVideoPlayer != null && creditsRawImage != null)
        {
            _creditsRenderTex = new RenderTexture(1920, 1080, 0);
            creditsVideoPlayer.targetTexture = _creditsRenderTex;
            creditsRawImage.texture = _creditsRenderTex;
        }

        // ── Input blocker — active while menu is showing ─────────
        if (inputBlocker != null)
        {
            inputBlocker.color = new Color(0, 0, 0, 0); // fully transparent
            inputBlocker.raycastTarget = true;                   // eats all clicks
            inputBlocker.gameObject.SetActive(true);
        }

        // ── Saved settings ───────────────────────────────────────
        float savedVol = PlayerPrefs.GetFloat("MasterVolume", defaultVolume);
        float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", defaultSensitivity);

        if (volumeSlider != null) { volumeSlider.value = savedVol; AudioListener.volume = savedVol; }
        if (sensitivitySlider != null) { sensitivitySlider.value = savedSens; ApplySensitivity(savedSens); }

        // ── Wire buttons ─────────────────────────────────────────
        startButton?.onClick.AddListener(OnStartPressed);
        optionsButton?.onClick.AddListener(OnOptionsPressed);
        creditsButton?.onClick.AddListener(OnCreditsPressed);
        quitButton?.onClick.AddListener(OnQuitPressed);
        optionsBackButton?.onClick.AddListener(OnOptionsBack);
        creditsBackButton?.onClick.AddListener(OnCreditsBack);

        volumeSlider?.onValueChanged.AddListener(OnVolumeChanged);
        sensitivitySlider?.onValueChanged.AddListener(OnSensitivityChanged);
    }

    // ─────────────────────────────────────────────────────────────
    // BUTTON HANDLERS
    // ─────────────────────────────────────────────────────────────

    private void OnStartPressed()
    {
        if (_animating || !_menuActive) return;
        StartCoroutine(SlideOutAndStart());
    }

    private void OnOptionsPressed()
    {
        if (_animating) return;
        StartCoroutine(SlideOptionsIn());
    }

    private void OnOptionsBack()
    {
        if (_animating) return;
        StartCoroutine(SlideOptionsOut());
    }

    private void OnCreditsPressed()
    {
        if (_animating) return;
        StartCoroutine(ShowCredits());
    }

    private void OnCreditsBack()
    {
        if (_animating) return;
        StartCoroutine(HideCredits());
    }

    private void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // SLIDER HANDLERS
    // ─────────────────────────────────────────────────────────────

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    private void OnSensitivityChanged(float value)
    {
        ApplySensitivity(value);
        PlayerPrefs.SetFloat("MouseSensitivity", value);
    }

    private void ApplySensitivity(float value)
    {
        if (sensitivityTarget == null || string.IsNullOrEmpty(sensitivityFieldName)) return;
        var field = sensitivityTarget.GetType().GetField(sensitivityFieldName);
        if (field != null && field.FieldType == typeof(float))
            field.SetValue(sensitivityTarget, value);
    }

    // ─────────────────────────────────────────────────────────────
    // ANIMATIONS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator SlideOutAndStart()
    {
        _animating = true;
        _menuActive = false;

        // Slide options out too if open
        if (optionsFolder.gameObject.activeSelf)
            yield return StartCoroutine(SlideOptionsOut());

        // Slide main folder left off screen
        Vector2 startPos = mainFolder.anchoredPosition;
        Vector2 targetPos = startPos - new Vector2(slideOutDistance, 0f);
        yield return StartCoroutine(SlideRect(mainFolder, startPos, targetPos, slideOutDuration, slideOutCurve));

        mainFolder.gameObject.SetActive(false);

        // Remove the input blocker — gameplay can now receive clicks
        if (inputBlocker != null)
            inputBlocker.gameObject.SetActive(false);

        _animating = false;
    }

    private IEnumerator SlideOptionsIn()
    {
        _animating = true;
        optionsFolder.anchoredPosition = _optionsFolderHiddenPos;
        optionsFolder.gameObject.SetActive(true);
        yield return StartCoroutine(SlideRect(optionsFolder,
            _optionsFolderHiddenPos, _optionsFolderVisiblePos,
            optionsSlideDuration, optionsSlideCurve));
        _animating = false;
    }

    private IEnumerator SlideOptionsOut()
    {
        _animating = true;
        yield return StartCoroutine(SlideRect(optionsFolder,
            _optionsFolderVisiblePos, _optionsFolderHiddenPos,
            optionsSlideDuration, optionsSlideCurve));
        optionsFolder.gameObject.SetActive(false);
        _animating = false;
    }

    private IEnumerator ShowCredits()
    {
        _animating = true;
        SetCreditsVisible(true, instant: false);
        creditsVideoPlayer?.Play();
        yield return StartCoroutine(FadeCanvasGroup(_creditsCanvasGroup, 0f, 1f, creditsFadeDuration));
        _animating = false;
    }

    private IEnumerator HideCredits()
    {
        _animating = true;
        yield return StartCoroutine(FadeCanvasGroup(_creditsCanvasGroup, 1f, 0f, creditsFadeDuration));
        creditsVideoPlayer?.Stop();
        SetCreditsVisible(false, instant: true);
        _animating = false;
    }

    // ─────────────────────────────────────────────────────────────
    // CREDITS VISIBILITY HELPER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Controls whether the credits overlay is visible and interactive.
    /// Handles both the CanvasGroup alpha AND the GraphicRaycaster on the
    /// credits canvas so the back button is always hittable when shown.
    /// </summary>
    private void SetCreditsVisible(bool visible, bool instant)
    {
        creditsOverlay.gameObject.SetActive(visible);

        if (_creditsCanvasGroup != null)
        {
            if (instant) _creditsCanvasGroup.alpha = visible ? 1f : 0f;
            _creditsCanvasGroup.interactable = visible;
            _creditsCanvasGroup.blocksRaycasts = visible;
        }

        // Enable/disable the GraphicRaycaster on the credits canvas
        // so the back button can actually receive pointer events
        if (_creditsRaycaster != null)
            _creditsRaycaster.enabled = visible;
    }

    // ─────────────────────────────────────────────────────────────
    // ANIMATION HELPERS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator SlideRect(RectTransform rt, Vector2 from, Vector2 to,
                                  float duration, AnimationCurve curve)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            rt.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private void OnDestroy()
    {
        if (_creditsRenderTex != null)
            _creditsRenderTex.Release();
    }
}