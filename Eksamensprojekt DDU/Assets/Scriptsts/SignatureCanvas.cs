using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a UI RawImage that acts as the signature pad.
/// Pen audio loops continuously while drawing; volume is mapped to cursor speed.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class SignatureCanvas : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerMoveHandler
{
    // ─────────────────────────────────────────────────────────────
    // INSPECTOR PARAMETERS
    // ─────────────────────────────────────────────────────────────

    [Header("═══ References")]
    [Tooltip("The FolderInteractionController to notify when signing is complete.")]
    public FolderInteractionController interactionController;

    [Header("═══ Pen Sprite")]
    [Tooltip("UI Image component used to show the pen cursor.")]
    public RectTransform penCursorImage;

    [Tooltip("Sprite to display as the pen cursor.")]
    public Sprite penSprite;

    [Tooltip("Size of the pen cursor sprite in pixels.")]
    public Vector2 penSize = new Vector2(48f, 48f);

    [Tooltip("Offset from the mouse position to the pen tip.")]
    public Vector2 penTipOffset = new Vector2(-16f, 16f);

    [Tooltip("Show pen cursor only while mouse button is held (true), or whenever hovering (false).")]
    public bool showPenOnlyWhileDrawing = false;

    [Header("═══ Ink Settings")]
    [Tooltip("Color of the ink strokes.")]
    public Color inkColor = new Color(0.05f, 0.05f, 0.15f, 1f);

    [Tooltip("Radius (pixels) of each brush stamp.")]
    [Range(1, 20)] public int brushRadius = 4;

    [Tooltip("Softness of the brush edge (0 = hard, 1 = fully soft).")]
    [Range(0f, 1f)] public float brushSoftness = 0.35f;

    [Header("═══ Pen Sound")]
    [Tooltip("AudioSource for pen sound. Leave null to use the one on FolderInteractionController.")]
    public AudioSource penAudioSource;

    [Tooltip("Looping clip that plays while drawing. Volume is driven by cursor speed.")]
    public AudioClip soundPenWriting;

    [Tooltip("Maximum volume of the pen sound (reached at max cursor speed).")]
    [Range(0f, 1f)] public float penMaxVolume = 0.8f;

    [Tooltip("Minimum volume of the pen sound while drawing but barely moving.")]
    [Range(0f, 1f)] public float penMinVolume = 0.05f;

    [Tooltip("Cursor speed (pixels/sec) at which the pen sound reaches maximum volume.")]
    [Range(10f, 2000f)] public float penMaxSpeedThreshold = 400f;

    [Tooltip("How smoothly volume responds to speed changes (higher = faster response).")]
    [Range(1f, 30f)] public float penVolumeSmoothing = 8f;

    [Tooltip("How quickly volume fades to zero after the pen lifts (seconds).")]
    [Range(0.01f, 1f)] public float penFadeOutTime = 0.12f;

    [Header("═══ Completion")]
    [Tooltip("Minimum total pixels painted before the signature is considered valid.")]
    [Range(10, 100000)] public int minPixelsToPaint = 50000;

    [Tooltip("Optional 'Sign here' prompt object; hidden when drawing starts.")]
    public GameObject signPromptObject;

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private RawImage _rawImage;
    private Texture2D _tex;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    private bool _isDrawing = false;
    private Vector2 _lastPos;
    private Vector2 _lastScreenPos;
    private int _totalPixelsPainted = 0;
    private bool _signatureSubmitted = false;

    private float _currentPenVolume = 0f;
    private float _targetPenVolume = 0f;
    private Coroutine _fadeOutCoroutine;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        _rectTransform = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();

        int w = Mathf.RoundToInt(_rectTransform.rect.width);
        int h = Mathf.RoundToInt(_rectTransform.rect.height);
        if (w < 4) w = 512;
        if (h < 4) h = 256;

        _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        ClearCanvas();
        _rawImage.texture = _tex;
        _rawImage.color = Color.white;

        // Set up pen cursor
        if (penCursorImage != null)
        {
            penCursorImage.sizeDelta = penSize;
            var img = penCursorImage.GetComponent<Image>();
            if (img != null && penSprite != null) img.sprite = penSprite;
            penCursorImage.gameObject.SetActive(false);
        }

        // Always use a dedicated AudioSource for the pen — never borrow the folder's
        if (penAudioSource == null)
        {
            penAudioSource = gameObject.AddComponent<AudioSource>();
            penAudioSource.playOnAwake = false;
            penAudioSource.loop = true;
        }
    }

    private void Update()
    {
        // Move pen cursor
        if (penCursorImage != null && penCursorImage.gameObject.activeSelf)
            MovePenCursorToMouse();

        // Smooth volume toward target
        if (penAudioSource != null && penAudioSource.isPlaying)
        {
            _currentPenVolume = Mathf.Lerp(_currentPenVolume, _targetPenVolume,
                Time.deltaTime * penVolumeSmoothing);
            penAudioSource.volume = _currentPenVolume;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // POINTER EVENTS
    // ─────────────────────────────────────────────────────────────

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!showPenOnlyWhileDrawing && penCursorImage != null)
            penCursorImage.gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDrawing = true;
        _lastPos = ScreenToTexCoord(eventData.position);
        _lastScreenPos = eventData.position;

        if (signPromptObject != null) signPromptObject.SetActive(false);
        if (penCursorImage != null) penCursorImage.gameObject.SetActive(true);

        // Stop any fade-out and start looping
        if (_fadeOutCoroutine != null) StopCoroutine(_fadeOutCoroutine);
        StartPenSound();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDrawing) return;

        // Calculate cursor speed in screen pixels/sec
        float screenDist = Vector2.Distance(eventData.position, _lastScreenPos);
        float speed = screenDist / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastScreenPos = eventData.position;

        // Map speed to target volume
        float speedT = Mathf.Clamp01(speed / penMaxSpeedThreshold);
        _targetPenVolume = Mathf.Lerp(penMinVolume, penMaxVolume, speedT);

        Vector2 currentPos = ScreenToTexCoord(eventData.position);
        DrawLine(_lastPos, currentPos);
        _lastPos = currentPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDrawing = false;
        _targetPenVolume = 0f;

        if (showPenOnlyWhileDrawing && penCursorImage != null)
            penCursorImage.gameObject.SetActive(false);

        // Fade out smoothly then stop the audio
        if (_fadeOutCoroutine != null) StopCoroutine(_fadeOutCoroutine);
        _fadeOutCoroutine = StartCoroutine(FadeOutPenSound());

        if (!_signatureSubmitted && _totalPixelsPainted >= minPixelsToPaint)
        {
            _signatureSubmitted = true;
            StartCoroutine(NotifyAfterDelay(0.6f));
        }
    }

    private void OnDisable()
    {
        StopPenSoundImmediate();
        if (penCursorImage != null) penCursorImage.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // PEN CURSOR
    // ─────────────────────────────────────────────────────────────

    private void MovePenCursorToMouse()
    {
        if (_parentCanvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentCanvas.GetComponent<RectTransform>(),
            Input.mousePosition,
            _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera,
            out Vector2 localPoint);
        penCursorImage.localPosition = localPoint + penTipOffset;
    }

    // ─────────────────────────────────────────────────────────────
    // DRAWING
    // ─────────────────────────────────────────────────────────────

    private void DrawLine(Vector2 from, Vector2 to)
    {
        int steps = Mathf.Max(Mathf.CeilToInt(Vector2.Distance(from, to)), 1);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Vector2.Lerp(from, to, t);
            PaintBrush(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
        }
        _tex.Apply();
    }

    private void PaintBrush(int cx, int cy)
    {
        int r = brushRadius;
        for (int x = cx - r; x <= cx + r; x++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (x < 0 || x >= _tex.width || y < 0 || y >= _tex.height) continue;
                float dist = Vector2.Distance(new Vector2(cx, cy), new Vector2(x, y));
                if (dist > r) continue;
                float alpha = 1f - Mathf.Clamp01((dist / r - (1f - brushSoftness)) / brushSoftness);
                Color existing = _tex.GetPixel(x, y);
                Color blended = Color.Lerp(existing, inkColor, inkColor.a * Mathf.Clamp01(alpha));
                _tex.SetPixel(x, y, blended);
                _totalPixelsPainted++;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // PEN SOUND
    // ─────────────────────────────────────────────────────────────

    private void StartPenSound()
    {
        if (penAudioSource == null || soundPenWriting == null) return;
        if (penAudioSource.clip != soundPenWriting || !penAudioSource.isPlaying)
        {
            penAudioSource.clip = soundPenWriting;
            penAudioSource.loop = true;
            penAudioSource.volume = penMinVolume;
            _currentPenVolume = penMinVolume;
            penAudioSource.Play();
        }
    }

    private IEnumerator FadeOutPenSound()
    {
        float startVol = _currentPenVolume;
        float elapsed = 0f;

        while (elapsed < penFadeOutTime)
        {
            elapsed += Time.deltaTime;
            _currentPenVolume = Mathf.Lerp(startVol, 0f, elapsed / penFadeOutTime);
            if (penAudioSource != null) penAudioSource.volume = _currentPenVolume;
            yield return null;
        }

        StopPenSoundImmediate();
    }

    private void StopPenSoundImmediate()
    {
        if (penAudioSource == null) return;
        penAudioSource.loop = false;
        penAudioSource.Stop();
        _currentPenVolume = 0f;
        _targetPenVolume = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────

    private Vector2 ScreenToTexCoord(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, screenPos, null, out Vector2 localPoint);
        return new Vector2(
            localPoint.x + _rectTransform.rect.width * 0.5f,
            localPoint.y + _rectTransform.rect.height * 0.5f);
    }

    private void ClearCanvas()
    {
        Color32[] pixels = new Color32[_tex.width * _tex.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
        _tex.SetPixels32(pixels);
        _tex.Apply();
    }

    private IEnumerator NotifyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (interactionController != null)
            interactionController.SignatureCompleted();
        else
            Debug.LogWarning("[SignatureCanvas] interactionController reference not set!");
    }
}