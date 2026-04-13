using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;

/// <summary>
/// Attach this to the Folder GameObject.
/// Eye-close effect is an AudioMixer low-pass filter sweep (EQ cutoff drops as eyes close)
/// mimicking the muffled sound of falling asleep, rather than a separate audio clip.
///
/// SETUP FOR EQ SWEEP:
///   1. Create an AudioMixer asset (Assets > Create > Audio Mixer), name it "GameMixer"
///   2. In the AudioMixer window, select the Master group
///   3. Add an "Audio Low Pass" effect to it
///   4. Right-click the "Cutoff freq" knob > Expose Parameter, name it "LowPassCutoff"
///   5. Assign the AudioMixer to the "Game Audio Mixer" slot below
///   6. Set all AudioSources in your scene to output to the GameMixer Master group
/// </summary>
public class FolderInteractionController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // INSPECTOR PARAMETERS
    // ─────────────────────────────────────────────────────────────

    [Header("═══ References")]
    public Camera mainCamera;

    [Tooltip("The CanvasGroup that wraps the signature UI.")]
    public CanvasGroup signatureCanvasGroup;

    [Tooltip("The CanvasGroup for the new document screen shown while eyes are closed.")]
    public CanvasGroup newDocumentCanvasGroup;

    [Tooltip("The Canvas component on the NewDocumentPanel.")]
    public Canvas newDocumentCanvas;

    [Tooltip("Sort order for the document canvas. Must be higher than EyeBlinkCanvas.")]
    public int documentCanvasSortOrder = 20;

    [Tooltip("Background Image component on the document panel.")]
    public UnityEngine.UI.Image documentBackgroundImage;

    [Tooltip("Paper texture sprite for the document background.")]
    public Sprite documentBackgroundSprite;

    [Tooltip("TextMeshPro text component for the typewriter.")]
    public TMPro.TextMeshProUGUI typewriterText;

    [Tooltip("Fallback full-screen black blink overlay CanvasGroup. Leave null if using EyeBlinkController.")]
    public CanvasGroup blinkOverlay;

    [Tooltip("EyeBlinkController for the eye-shaped blink. Overrides blinkOverlay if set.")]
    public EyeBlinkController eyeBlink;

    [Tooltip("A canvas with a fullscreen black image (sort order 5). Activated after eyes close to block the game world.")]
    public Canvas blackBackgroundCanvas;

    [Header("═══ Hover Glow")]
    public Renderer folderRenderer;
    public Color hoverEmissionColor = new Color(0.2f, 0.6f, 1f);
    [Range(0.5f, 10f)] public float pulseSpeed = 2f;
    [Range(0f, 2f)] public float pulseMin = 0.3f;
    [Range(0f, 5f)] public float pulseMax = 1.4f;

    [Header("═══ Camera Zoom-In")]
    public Vector3 zoomTargetPosition = new Vector3(0f, 1.5f, -1f);
    public Vector3 zoomTargetRotation = new Vector3(10f, 0f, 0f);
    [Range(0.1f, 5f)] public float zoomInDuration = 1.8f;
    public AnimationCurve zoomInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("═══ Camera Pull-Back")]
    public Vector3 pullBackPosition = new Vector3(0f, 1.8f, -3f);
    public Vector3 pullBackRotation = new Vector3(5f, 0f, 0f);
    [Range(0.1f, 5f)] public float pullBackDuration = 1.2f;
    public AnimationCurve pullBackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("═══ Blink / Eye Close")]
    [Range(0.05f, 2f)] public float blinkCloseDuration = 0.4f;
    [Range(0.05f, 2f)] public float blinkOpenDuration = 0.5f;
    [Range(0f, 3f)] public float blinkHoldDuration = 0.2f;

    [Header("═══ New Document / Typewriter")]
    [TextArea(4, 12)]
    public string typewriterFullText = "MEMORANDUM\n\nYour cooperation has been noted.\nThe agreement is now binding.\n\nSign below to confirm receipt.";

    [Tooltip("Color of the typewriter text.")]
    public Color typewriterTextColor = new Color(0.05f, 0.05f, 0.1f, 1f);

    [Tooltip("Characters per second — how fast text appears on screen.")]
    [Range(1f, 100f)] public float typewriterTextSpeed = 28f;

    [Tooltip("Sound ticks per second — independent from text speed.")]
    [Range(1f, 100f)] public float typewriterSoundSpeed = 28f;

    [Range(0f, 3f)] public float typewriterStartDelay = 0.6f;
    [Range(0.5f, 10f)] public float documentReadTime = 3.5f;
    [Range(0f, 3f)] public float documentFadeInDuration = 0.8f;
    [Range(0f, 3f)] public float documentFadeOutDuration = 1f;

    [Header("═══ Scene Switch")]
    public string nextSceneName = "";

    // ─────────────────────────────────────────────────────────────
    // SOUNDS
    // ─────────────────────────────────────────────────────────────

    [Header("═══ Sounds")]
    [Tooltip("AudioSource used for all non-pen sounds. Auto-created if left null.")]
    public AudioSource audioSource;

    [Tooltip("One-shot sound on folder hover entry.")]
    public AudioClip soundHover;
    [Range(0f, 1f)] public float hoverVolume = 0.4f;

    [Tooltip("One-shot sound on folder click.")]
    public AudioClip soundClick;
    [Range(0f, 1f)] public float clickVolume = 0.8f;

    [Tooltip("Looping sound during camera zoom-in.")]
    public AudioClip soundZoomIn;
    [Range(0f, 1f)] public float zoomInVolume = 0.5f;

    [Tooltip("Looping sound during camera pull-back.")]
    public AudioClip soundPullBack;
    [Range(0f, 1f)] public float pullBackVolume = 0.5f;

    [Tooltip("One-shot sound per character typed.")]
    public AudioClip soundTypewriter;
    [Range(0f, 1f)] public float typewriterVolume = 0.5f;
    [Range(0f, 0.5f)] public float typewriterPitchVariance = 0.1f;

    // ─────────────────────────────────────────────────────────────
    // EQ SWEEP (eye close effect)
    // ─────────────────────────────────────────────────────────────

    [Header("═══ Eye Close EQ Sweep")]
    [Tooltip("The AudioMixer that has a Low Pass filter on its Master group with 'LowPassCutoff' exposed.")]
    public AudioMixer gameMixer;

    [Tooltip("Exact name of the exposed Low Pass cutoff parameter in the AudioMixer.")]
    public string lowPassParameterName = "LowPassCutoff";

    [Tooltip("Normal cutoff frequency when eyes are open (Hz). 22000 = fully open / no filtering.")]
    [Range(100f, 22000f)] public float eqOpenFrequency = 22000f;

    [Tooltip("Cutoff frequency when eyes are fully closed (Hz). Lower = more muffled / asleep.")]
    [Range(10f, 5000f)] public float eqClosedFrequency = 300f;

    [Tooltip("How long the EQ sweeps from open to closed (should match blinkCloseDuration).")]
    [Range(0.05f, 5f)] public float eqSweepDownDuration = 0.8f;

    [Tooltip("How long the EQ sweeps from closed back to open when eyes open in new scene.")]
    [Range(0.05f, 5f)] public float eqSweepUpDuration = 1.2f;

    [Tooltip("Easing curve for the EQ sweep down (closing).")]
    public AnimationCurve eqCloseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Easing curve for the EQ sweep up (opening).")]
    public AnimationCurve eqOpenCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private bool _isHovered = false;
    private bool _isClicked = false;
    private bool _signatureComplete = false;

    /// <summary>Set to true by MainMenuController when the player presses Start.</summary>
    [HideInInspector]
    public bool interactionEnabled
    {
        get => _interactionEnabled;
        set
        {
            _interactionEnabled = value;
            if (_collider != null) _collider.enabled = value;
        }
    }
    private bool _interactionEnabled = false;

    // Cache the collider so we can toggle it
    private Collider _collider;

    private Material _folderMat;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    // Separate source for looping ambient sounds so they never stomp one-shots
    private AudioSource _loopSource;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (folderRenderer != null)
        {
            _folderMat = folderRenderer.material;
            _folderMat.EnableKeyword("_EMISSION");
            SetEmission(Color.black);
        }

        if (mainCamera == null) mainCamera = Camera.main;

        // Disable collider until menu hands over control
        _collider = GetComponent<Collider>();
        if (_collider != null) _collider.enabled = false;

        if (newDocumentCanvas != null)
            DontDestroyOnLoad(newDocumentCanvas.gameObject);

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.outputAudioMixerGroup = null; // bypass mixer entirely for SFX

        // Dedicated source for looping sounds — never interrupts one-shots
        _loopSource = gameObject.AddComponent<AudioSource>();
        _loopSource.playOnAwake = false;
        _loopSource.loop = true;
        _loopSource.outputAudioMixerGroup = null; // also bypass mixer

        if (typewriterText != null)
        {
            typewriterText.color = typewriterTextColor;
            typewriterText.text = "";
        }

        SetCanvasAlpha(signatureCanvasGroup, 0f);
        SetCanvasAlpha(newDocumentCanvasGroup, 0f);
        SetCanvasAlpha(blinkOverlay, 0f);

        if (blackBackgroundCanvas != null)
            blackBackgroundCanvas.gameObject.SetActive(false);

        // Ensure EQ starts fully open
        SetLowPass(eqOpenFrequency);
    }

    private void Update()
    {
        if (!interactionEnabled || _isClicked) return;
        HandleHoverGlow();
        HandleClick();
    }

    // ─────────────────────────────────────────────────────────────
    // HOVER
    // ─────────────────────────────────────────────────────────────

    private void HandleHoverGlow()
    {
        if (!_isHovered) { SetEmission(Color.black); return; }
        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        SetEmission(hoverEmissionColor * Mathf.Lerp(pulseMin, pulseMax, t));
    }

    private void OnMouseEnter()
    {
        if (!interactionEnabled || _isClicked) return;
        _isHovered = true;
        if (soundHover != null && audioSource != null && !audioSource.isPlaying)
            audioSource.PlayOneShot(soundHover, hoverVolume);
    }

    private void OnMouseExit()
    {
        if (!interactionEnabled || _isClicked) return;
        _isHovered = false;
    }

    private void HandleClick()
    {
        if (Input.GetMouseButtonDown(0) && _isHovered)
        {
            _isClicked = true;
            SetEmission(Color.black);
            if (audioSource != null && audioSource.clip == soundHover) audioSource.Stop();
            PlayOneShot(soundClick, clickVolume);
            StartCoroutine(FullSequence());
        }
    }

    // ─────────────────────────────────────────────────────────────
    // MASTER SEQUENCE
    // ─────────────────────────────────────────────────────────────

    private IEnumerator FullSequence()
    {
        // 1. Zoom in
        PlayLooping(soundZoomIn, zoomInVolume);
        yield return StartCoroutine(MoveCameraTo(
            zoomTargetPosition, Quaternion.Euler(zoomTargetRotation),
            zoomInDuration, zoomInCurve));
        StopLooping();

        // 2. Show signature canvas
        yield return StartCoroutine(FadeCanvas(signatureCanvasGroup, 0f, 1f, 0.4f));
        signatureCanvasGroup.interactable = true;
        signatureCanvasGroup.blocksRaycasts = true;

        // 3. Wait for signature
        yield return StartCoroutine(WaitForSignature());

        // 4. Hide signature canvas
        yield return StartCoroutine(FadeCanvas(signatureCanvasGroup, 1f, 0f, 0.3f));
        signatureCanvasGroup.interactable = false;
        signatureCanvasGroup.blocksRaycasts = false;

        // 5. Pull back
        PlayLooping(soundPullBack, pullBackVolume);
        yield return StartCoroutine(MoveCameraTo(
            pullBackPosition, Quaternion.Euler(pullBackRotation),
            pullBackDuration, pullBackCurve));
        StopLooping();

        // 6. EQ sweep down + eyelids close simultaneously
        yield return StartCoroutine(CloseEyesWithEQ());

        // 7. Block world with black canvas
        if (blackBackgroundCanvas != null)
            blackBackgroundCanvas.gameObject.SetActive(true);

        // 8. Document fades in over closed lids
        ApplyDocumentBackground();
        if (typewriterText != null) typewriterText.color = typewriterTextColor;
        if (newDocumentCanvas != null) newDocumentCanvas.sortingOrder = documentCanvasSortOrder;
        SetCanvasAlpha(newDocumentCanvasGroup, 0f);
        yield return StartCoroutine(FadeCanvas(newDocumentCanvasGroup, 0f, 1f, documentFadeInDuration));

        // 9. Typewriter
        if (typewriterText != null)
        {
            yield return new WaitForSeconds(typewriterStartDelay);
            yield return StartCoroutine(Typewrite(typewriterFullText));
        }

        // 10. Read pause
        yield return new WaitForSeconds(documentReadTime);

        // 11. Fade document out
        yield return StartCoroutine(FadeCanvas(newDocumentCanvasGroup, 1f, 0f, documentFadeOutDuration));

        // 12. Switch scene — EQ stays muffled, SceneOpenEyes opens lids + sweeps EQ back up
        Debug.Log("[FolderInteraction] *** SCENE SWITCH TRIGGERED ***  →  " +
                  (string.IsNullOrEmpty(nextSceneName) ? "(no scene name set)" : nextSceneName));

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(nextSceneName);
            load.allowSceneActivation = false;
            while (load.progress < 0.9f) yield return null;
            load.allowSceneActivation = true;
        }
        else
        {
            // No scene — open eyes and sweep EQ back up for testing
            yield return StartCoroutine(OpenEyesWithEQ());
        }
    }

    // ─────────────────────────────────────────────────────────────
    // SIGNATURE
    // ─────────────────────────────────────────────────────────────

    private IEnumerator WaitForSignature()
    {
        _signatureComplete = false;
        yield return new WaitUntil(() => _signatureComplete);
    }

    public void SignatureCompleted() => _signatureComplete = true;

    // ─────────────────────────────────────────────────────────────
    // CAMERA
    // ─────────────────────────────────────────────────────────────

    private IEnumerator MoveCameraTo(Vector3 targetPos, Quaternion targetRot,
                                     float duration, AnimationCurve curve)
    {
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
    }

    // ─────────────────────────────────────────────────────────────
    // BLINK + EQ
    // ─────────────────────────────────────────────────────────────

    /// <summary>Runs eyelid close and EQ sweep down simultaneously.</summary>
    private IEnumerator CloseEyesWithEQ()
    {
        // Run both in parallel
        Coroutine lidClose = StartCoroutine(CloseEyesOnly());
        Coroutine eqSweep = StartCoroutine(SweepEQ(eqOpenFrequency, eqClosedFrequency,
                                                     eqSweepDownDuration, eqCloseCurve));
        yield return lidClose;
        yield return eqSweep;
    }

    /// <summary>Runs eyelid open and EQ sweep up simultaneously.</summary>
    public IEnumerator OpenEyesWithEQ()
    {
        Coroutine lidOpen = StartCoroutine(OpenEyesOnly());
        Coroutine eqSweep = StartCoroutine(SweepEQ(eqClosedFrequency, eqOpenFrequency,
                                                    eqSweepUpDuration, eqOpenCurve));
        yield return lidOpen;
        yield return eqSweep;
    }

    private IEnumerator CloseEyesOnly()
    {
        if (eyeBlink != null)
            yield return StartCoroutine(eyeBlink.CloseEye());
        else
            yield return StartCoroutine(FadeCanvas(blinkOverlay, 0f, 1f, blinkCloseDuration));
    }

    public IEnumerator OpenEyesOnly()
    {
        if (eyeBlink != null)
            yield return StartCoroutine(eyeBlink.OpenEye());
        else
            yield return StartCoroutine(FadeCanvas(blinkOverlay, 1f, 0f, blinkOpenDuration));
    }

    /// <summary>Sweeps the AudioMixer low-pass cutoff from one frequency to another.</summary>
    private IEnumerator SweepEQ(float fromHz, float toHz, float duration, AnimationCurve curve)
    {
        if (gameMixer == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetLowPass(Mathf.Lerp(fromHz, toHz, t));
            yield return null;
        }
        SetLowPass(toHz);
    }

    private void SetLowPass(float hz)
    {
        if (gameMixer != null)
            gameMixer.SetFloat(lowPassParameterName, hz);
    }

    private void ApplyDocumentBackground()
    {
        if (documentBackgroundImage != null && documentBackgroundSprite != null)
            documentBackgroundImage.sprite = documentBackgroundSprite;
    }

    // ─────────────────────────────────────────────────────────────
    // TYPEWRITER
    // ─────────────────────────────────────────────────────────────

    private IEnumerator Typewrite(string fullText)
    {
        typewriterText.text = "";
        float textDelay = 1f / typewriterTextSpeed;
        float soundDelay = 1f / typewriterSoundSpeed;
        float soundTimer = 0f;

        foreach (char c in fullText)
        {
            typewriterText.text += c;
            soundTimer += textDelay;
            if (soundTimer >= soundDelay && c != ' ' && c != '\n')
            {
                PlayTypewriterTick();
                soundTimer = 0f;
            }
            yield return new WaitForSeconds(textDelay);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // SOUND HELPERS
    // ─────────────────────────────────────────────────────────────

    public void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    private void PlayLooping(AudioClip clip, float volume)
    {
        if (clip == null || _loopSource == null) return;
        _loopSource.clip = clip;
        _loopSource.volume = volume;
        _loopSource.loop = true;
        _loopSource.Play();
    }

    private void StopLooping()
    {
        if (_loopSource == null) return;
        _loopSource.Stop();
    }

    private void PlayTypewriterTick()
    {
        if (audioSource == null || soundTypewriter == null) return;
        float prev = audioSource.pitch;
        audioSource.pitch = 1f + Random.Range(-typewriterPitchVariance, typewriterPitchVariance);
        audioSource.PlayOneShot(soundTypewriter, typewriterVolume);
        audioSource.pitch = prev;
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;
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

    private void SetEmission(Color color)
    {
        if (_folderMat != null) _folderMat.SetColor(EmissionColor, color);
    }

    private void SetCanvasAlpha(CanvasGroup cg, float alpha)
    {
        if (cg == null) return;
        cg.alpha = alpha;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }
}