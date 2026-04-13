using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// Main menu controller — pure uGUI version (no UI Toolkit).
/// Wire all buttons in the Inspector via their OnClick() events.
/// All handler methods are public so they appear in the Inspector dropdown.
/// </summary>
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
    [Tooltip("Place at its visible position in the editor. Script calculates the hidden position.")]
    public RectTransform optionsFolder;
    public Button optionsBackButton;
    public Slider sensitivitySlider;
    public Slider volumeSlider;
    [Range(0.1f, 10f)] public float defaultSensitivity = 1f;
    [Range(0f, 1f)] public float defaultVolume = 1f;

    [Header("═══ Credits")]
    [Tooltip("Must be on its own Canvas at a high sort order (e.g. 50).")]
    public RectTransform creditsOverlay;
    public VideoPlayer creditsVideoPlayer;
    public RawImage creditsRawImage;
    public Button creditsBackButton;

    [Header("═══ Sounds")]
    [Tooltip("AudioSource to play button sounds through. Auto-created if left null.")]
    public AudioSource menuAudioSource;

    [Tooltip("Sound that plays when any menu button is clicked.")]
    public AudioClip buttonClickSound;

    [Range(0f, 1f)] public float buttonClickVolume = 0.8f;

    [Header("═══ Gameplay")]
    [Tooltip("Drag the folder object's FolderInteractionController here.")]
    public FolderInteractionController folderInteraction;

    [Header("═══ Animation")]
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
    private Vector2 _optionsFolderVisiblePos;
    private Vector2 _optionsFolderHiddenPos;

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
        _mainFolderStartPos = mainFolder.anchoredPosition;

        _optionsFolderVisiblePos = optionsFolder.anchoredPosition;
        _optionsFolderHiddenPos = _optionsFolderVisiblePos - new Vector2(optionsFolder.rect.width + 60f, 0f);
        optionsFolder.anchoredPosition = _optionsFolderHiddenPos;
        optionsFolder.gameObject.SetActive(false);

        _creditsCanvasGroup = creditsOverlay.GetComponent<CanvasGroup>();
        if (_creditsCanvasGroup == null)
            _creditsCanvasGroup = creditsOverlay.gameObject.AddComponent<CanvasGroup>();
        _creditsRaycaster = creditsOverlay.GetComponentInParent<GraphicRaycaster>();
        SetCreditsVisible(false, instant: true);

        if (creditsVideoPlayer != null && creditsRawImage != null)
        {
            _creditsRenderTex = new RenderTexture(1920, 1080, 0);
            creditsVideoPlayer.targetTexture = _creditsRenderTex;
            creditsRawImage.texture = _creditsRenderTex;
        }

        if (folderInteraction != null)
            folderInteraction.interactionEnabled = false;

        if (menuAudioSource == null)
            menuAudioSource = gameObject.AddComponent<AudioSource>();
        menuAudioSource.playOnAwake = false;

        float savedVol = PlayerPrefs.GetFloat("MasterVolume", defaultVolume);
        float savedSens = PlayerPrefs.GetFloat("MouseSensitivity", defaultSensitivity);
        if (volumeSlider != null) { volumeSlider.value = savedVol; AudioListener.volume = savedVol; }
        if (sensitivitySlider != null) { sensitivitySlider.value = savedSens; ApplySensitivity(savedSens); }

        // Wire buttons via code as backup — also wire them in the Inspector
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
    // BUTTON HANDLERS — public for Inspector wiring
    // ─────────────────────────────────────────────────────────────

    public void OnStartPressed()
    {
        if (_animating || !_menuActive) return;
        PlayButtonClick();
        StartCoroutine(SlideOutAndStart());
    }

    public void OnOptionsPressed()
    {
        if (_animating) return;
        PlayButtonClick();
        StartCoroutine(SlideOptionsIn());
    }

    public void OnOptionsBack()
    {
        if (_animating) return;
        PlayButtonClick();
        StartCoroutine(SlideOptionsOut());
    }

    public void OnCreditsPressed()
    {
        if (_animating) return;
        PlayButtonClick();
        StartCoroutine(ShowCredits());
    }

    public void OnCreditsBack()
    {
        if (_animating) return;
        PlayButtonClick();
        StartCoroutine(HideCredits());
    }

    public void OnQuitPressed()
    {
        PlayButtonClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayButtonClick()
    {
        if (menuAudioSource != null && buttonClickSound != null)
            menuAudioSource.PlayOneShot(buttonClickSound, buttonClickVolume);
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

        if (optionsFolder.gameObject.activeSelf)
            yield return StartCoroutine(SlideOptionsOut());

        Vector2 startPos = mainFolder.anchoredPosition;
        Vector2 targetPos = startPos - new Vector2(slideOutDistance, 0f);
        yield return StartCoroutine(SlideRect(mainFolder, startPos, targetPos, slideOutDuration, slideOutCurve));

        mainFolder.gameObject.SetActive(false);

        if (folderInteraction != null)
            folderInteraction.interactionEnabled = true;

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
    // CREDITS VISIBILITY
    // ─────────────────────────────────────────────────────────────

    private void SetCreditsVisible(bool visible, bool instant)
    {
        creditsOverlay.gameObject.SetActive(visible);
        if (_creditsCanvasGroup != null)
        {
            if (instant) _creditsCanvasGroup.alpha = visible ? 1f : 0f;
            _creditsCanvasGroup.interactable = visible;
            _creditsCanvasGroup.blocksRaycasts = visible;
        }
        if (_creditsRaycaster != null)
            _creditsRaycaster.enabled = visible;
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
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