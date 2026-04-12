using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach this to the Folder GameObject.
/// Manages hover glow, camera zoom-in, signature phase, blink transitions, and scene switch.
/// All tunable parameters are exposed in the Inspector.
/// </summary>
public class FolderInteractionController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // INSPECTOR PARAMETERS
    // ─────────────────────────────────────────────────────────────

    [Header("═══ References")]
    [Tooltip("The main camera that will be animated.")]
    public Camera mainCamera;

    [Tooltip("The CanvasGroup that wraps the signature UI (pen + canvas).")]
    public CanvasGroup signatureCanvasGroup;

    [Tooltip("The CanvasGroup for the new document screen shown while eyes are closed.")]
    public CanvasGroup newDocumentCanvasGroup;

    [Tooltip("The Canvas component on the NewDocumentPanel — needed to set sort order above the eyelids.")]
    public Canvas newDocumentCanvas;

    [Tooltip("Sort order for the document canvas. Must be HIGHER than EyeBlinkCanvas sort order so it renders on top of the eyelids.")]
    public int documentCanvasSortOrder = 20;

    [Tooltip("Background image for the new document panel (e.g. a paper texture). Assign a UI Image component.")]
    public UnityEngine.UI.Image documentBackgroundImage;

    [Tooltip("Sprite to use as the document background (paper texture, letter etc). Drag a Sprite asset here.")]
    public Sprite documentBackgroundSprite;

    [Tooltip("Text component that will typewrite itself.")]
    public TMPro.TextMeshProUGUI typewriterText;

    [Tooltip("Full-screen black blink overlay. Leave null if using EyeBlinkController.")]
    public CanvasGroup blinkOverlay;

    [Tooltip("Assign the EyeBlinkController for the eye-shaped blink. If set, overrides blinkOverlay.")]
    public EyeBlinkController eyeBlink;

    [Tooltip("A canvas containing just a fullscreen black image (sort order 5). Activated immediately after eyes close to block the game world.")]
    public Canvas blackBackgroundCanvas;

    [Header("═══ Hover Glow")]
    [Tooltip("Renderer whose material emission will be pulsed on hover.")]
    public Renderer folderRenderer;

    [Tooltip("Emission color when hovered.")]
    public Color hoverEmissionColor = new Color(0.2f, 0.6f, 1f);

    [Tooltip("Speed of the emission pulse while hovering.")]
    [Range(0.5f, 10f)] public float pulseSpeed = 2f;

    [Tooltip("Min brightness of the pulse (0 = fully off).")]
    [Range(0f, 2f)] public float pulseMin = 0.3f;

    [Tooltip("Max brightness of the pulse.")]
    [Range(0f, 5f)] public float pulseMax = 1.4f;

    [Header("═══ Camera Zoom-In")]
    [Tooltip("World-space position the camera moves TO when the folder is clicked.")]
    public Vector3 zoomTargetPosition = new Vector3(0f, 1.5f, -1f);

    [Tooltip("World-space Euler angles the camera rotates TO when zooming in.")]
    public Vector3 zoomTargetRotation = new Vector3(10f, 0f, 0f);

    [Tooltip("Duration (seconds) of the zoom-in movement.")]
    [Range(0.1f, 5f)] public float zoomInDuration = 1.8f;

    [Tooltip("Animation curve for the zoom-in easing.")]
    public AnimationCurve zoomInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("═══ Camera Pull-Back (after signing)")]
    [Tooltip("World-space position the camera pulls back TO after signing.")]
    public Vector3 pullBackPosition = new Vector3(0f, 1.8f, -3f);

    [Tooltip("World-space Euler angles for pull-back.")]
    public Vector3 pullBackRotation = new Vector3(5f, 0f, 0f);

    [Tooltip("Duration (seconds) of the pull-back movement.")]
    [Range(0.1f, 5f)] public float pullBackDuration = 1.2f;

    [Tooltip("Animation curve for pull-back easing.")]
    public AnimationCurve pullBackCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("═══ Blink / Eye Close")]
    [Tooltip("Duration (seconds) of one eye-close animation.")]
    [Range(0.05f, 2f)] public float blinkCloseDuration = 0.4f;

    [Tooltip("Duration (seconds) of one eye-open animation.")]
    [Range(0.05f, 2f)] public float blinkOpenDuration = 0.5f;

    [Tooltip("How long the eyes stay shut between close and open (seconds).")]
    [Range(0f, 3f)] public float blinkHoldDuration = 0.2f;

    [Header("═══ New Document / Typewriter")]
    [Tooltip("The full text that will typewrite itself on the new document.")]
    [TextArea(4, 12)]
    public string typewriterFullText = "MEMORANDUM\n\nYour cooperation has been noted.\nThe agreement is now binding.\n\nSign below to confirm receipt.";

    [Tooltip("Color of the typewriter text.")]
    public Color typewriterTextColor = new Color(0.05f, 0.05f, 0.1f, 1f);

    [Tooltip("Characters per second — how fast the text appears on screen.")]
    [Range(1f, 100f)] public float typewriterTextSpeed = 28f;

    [Tooltip("Sound ticks per second — how fast the keystroke sound fires. Set independently from text speed.")]
    [Range(1f, 100f)] public float typewriterSoundSpeed = 28f;

    [Tooltip("Delay (seconds) before the typewriter starts after eyes close.")]
    [Range(0f, 3f)] public float typewriterStartDelay = 0.6f;

    [Tooltip("How long the player reads the finished document before the scene switches (seconds).")]
    [Range(0.5f, 10f)] public float documentReadTime = 3.5f;

    [Tooltip("Duration (seconds) of the document fade-in after eyes close.")]
    [Range(0f, 3f)] public float documentFadeInDuration = 0.8f;

    [Tooltip("Duration (seconds) of the document fade-out before the scene switches.")]
    [Range(0f, 3f)] public float documentFadeOutDuration = 1f;

    [Header("═══ Scene Switch")]
    [Tooltip("Name of the scene to load after the sequence. Leave empty to only log to console.")]
    public string nextSceneName = "";

    // ─────────────────────────────────────────────────────────────
    // SOUNDS
    // ─────────────────────────────────────────────────────────────

    [Header("═══ Sounds")]
    [Tooltip("AudioSource used to play all sounds. Leave null to auto-create one on this GameObject.")]
    public AudioSource audioSource;

    [Tooltip("Looping sound that plays while hovering over the folder.")]
    public AudioClip soundHover;
    [Range(0f, 1f)] public float hoverVolume = 0.4f;

    [Tooltip("One-shot sound when the folder is clicked.")]
    public AudioClip soundClick;
    [Range(0f, 1f)] public float clickVolume = 0.8f;

    [Tooltip("Looping sound while the camera zooms in toward the folder.")]
    public AudioClip soundZoomIn;
    [Range(0f, 1f)] public float zoomInVolume = 0.5f;

    [Tooltip("Looping sound while the camera pulls back after signing.")]
    public AudioClip soundPullBack;
    [Range(0f, 1f)] public float pullBackVolume = 0.5f;

    [Tooltip("Looping sound while the eyelids are closing.")]
    public AudioClip soundEyeClose;
    [Range(0f, 1f)] public float eyeCloseVolume = 0.6f;

    [Tooltip("One-shot sound per character typed by the typewriter.")]
    public AudioClip soundTypewriter;
    [Range(0f, 1f)] public float typewriterVolume = 0.5f;

    [Tooltip("Pitch randomization on each typewriter tick so it sounds more natural.")]
    [Range(0f, 0.5f)] public float typewriterPitchVariance = 0.1f;

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private bool _isHovered = false;
    private bool _isClicked = false;
    private bool _signatureComplete = false;

    private Vector3 _camStartPos;
    private Quaternion _camStartRot;

    private Material _folderMat;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

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

        if (mainCamera == null)
            mainCamera = Camera.main;

        _camStartPos = mainCamera.transform.position;
        _camStartRot = mainCamera.transform.rotation;

        if (newDocumentCanvas != null)
            DontDestroyOnLoad(newDocumentCanvas.gameObject);

        // Auto-create AudioSource if none assigned
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        // Apply typewriter text color now so it's correct from the start
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
    }

    private void Update()
    {
        if (_isClicked) return;
        HandleHoverGlow();
        HandleClick();
    }

    // ─────────────────────────────────────────────────────────────
    // HOVER
    // ─────────────────────────────────────────────────────────────

    private void HandleHoverGlow()
    {
        if (!_isHovered)
        {
            SetEmission(Color.black);
            return;
        }

        float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        float brightness = Mathf.Lerp(pulseMin, pulseMax, t);
        SetEmission(hoverEmissionColor * brightness);
    }

    private void OnMouseEnter()
    {
        _isHovered = true;
        PlayLooping(soundHover, hoverVolume);
    }

    private void OnMouseExit()
    {
        _isHovered = false;
        StopLooping();
    }

    // ─────────────────────────────────────────────────────────────
    // CLICK
    // ─────────────────────────────────────────────────────────────

    private void HandleClick()
    {
        if (Input.GetMouseButtonDown(0) && _isHovered)
        {
            _isClicked = true;
            SetEmission(Color.black);
            StopLooping();
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

        // 3. Wait for signature (pen sounds handled by SignatureCanvas)
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

        // 6. Close eyes — stays closed
        PlayLooping(soundEyeClose, eyeCloseVolume);
        yield return StartCoroutine(CloseEyesOnly());
        StopLooping();

        // Block the game world immediately with a plain black canvas
        if (blackBackgroundCanvas != null)
            blackBackgroundCanvas.gameObject.SetActive(true);

        // 7. Show document ON TOP of closed eyelids — set sort order first, then fade in
        ApplyDocumentBackground();
        if (typewriterText != null) typewriterText.color = typewriterTextColor;
        if (newDocumentCanvas != null) newDocumentCanvas.sortingOrder = documentCanvasSortOrder;
        SetCanvasAlpha(newDocumentCanvasGroup, 0f); // ensure starting from 0 before fade
        yield return StartCoroutine(FadeCanvas(newDocumentCanvasGroup, 0f, 1f, documentFadeInDuration));

        // 8. Typewriter
        if (typewriterText != null)
        {
            yield return new WaitForSeconds(typewriterStartDelay);
            yield return StartCoroutine(Typewrite(typewriterFullText));
        }

        // 9. Read pause
        yield return new WaitForSeconds(documentReadTime);

        // 10. Fade document back to black before scene switch
        yield return StartCoroutine(FadeCanvas(newDocumentCanvasGroup, 1f, 0f, documentFadeOutDuration));

        // 11. Switch scene — eyes stay shut, SceneOpenEyes opens them in the new scene
        Debug.Log("[FolderInteraction] *** SCENE SWITCH TRIGGERED ***  →  " +
                  (string.IsNullOrEmpty(nextSceneName) ? "(no scene name set)" : nextSceneName));

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(nextSceneName);
            load.allowSceneActivation = false;
            while (load.progress < 0.9f)
                yield return null;
            load.allowSceneActivation = true;
        }
        else
        {
            yield return StartCoroutine(OpenEyesOnly());
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

    public void SignatureCompleted()
    {
        _signatureComplete = true;
    }

    // ─────────────────────────────────────────────────────────────
    // CAMERA
    // ─────────────────────────────────────────────────────────────

    private IEnumerator MoveCameraTo(Vector3 targetPos, Quaternion targetRot, float duration, AnimationCurve curve)
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
    // BLINK
    // ─────────────────────────────────────────────────────────────

    private IEnumerator Blink()
    {
        yield return StartCoroutine(CloseEyesOnly());
        yield return new WaitForSeconds(blinkHoldDuration);
        yield return StartCoroutine(OpenEyesOnly());
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

            // Fire sound tick on its own independent timer
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
        if (clip == null || audioSource == null) return;
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void StopLooping()
    {
        if (audioSource == null) return;
        audioSource.loop = false;
        audioSource.Stop();
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
        if (_folderMat != null)
            _folderMat.SetColor(EmissionColor, color);
    }

    private void SetCanvasAlpha(CanvasGroup cg, float alpha)
    {
        if (cg != null)
        {
            cg.alpha = alpha;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
    }
}