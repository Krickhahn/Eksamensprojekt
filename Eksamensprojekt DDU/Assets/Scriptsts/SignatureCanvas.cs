using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a UI RawImage that acts as the signature pad.
/// Draws mouse strokes on a transparent texture.
/// Supports a pen sprite that follows the cursor while drawing,
/// and a pen writing sound that plays while the mouse is dragged.
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
    [Tooltip("UI Image component used to show the pen cursor. Place it as a child of the signature panel.")]
    public RectTransform penCursorImage;

    [Tooltip("Sprite to display as the pen cursor.")]
    public Sprite penSprite;

    [Tooltip("Size of the pen cursor sprite in pixels.")]
    public Vector2 penSize = new Vector2(48f, 48f);

    [Tooltip("Offset from the mouse position to the pen tip (so the tip aligns with where ink appears). Tweak to match your pen sprite's tip position.")]
    public Vector2 penTipOffset = new Vector2(-16f, 16f);

    [Tooltip("Show the pen cursor only while the mouse button is held down (true), or whenever hovering (false).")]
    public bool showPenOnlyWhileDrawing = false;

    [Header("═══ Pen Settings")]
    [Tooltip("Color of the ink strokes.")]
    public Color inkColor = new Color(0.05f, 0.05f, 0.15f, 1f);

    [Tooltip("Radius (pixels) of each brush stamp.")]
    [Range(1, 20)] public int brushRadius = 4;

    [Tooltip("Softness of the brush edge (0 = hard circle, 1 = fully soft).")]
    [Range(0f, 1f)] public float brushSoftness = 0.35f;

    [Header("═══ Pen Sound")]
    [Tooltip("AudioSource to play pen sounds through. Leave null to use the one on FolderInteractionController.")]
    public AudioSource penAudioSource;

    [Tooltip("Looping sound that plays while the player is drawing their signature.")]
    public AudioClip soundPenWriting;

    [Range(0f, 1f)] public float penWritingVolume = 0.5f;

    [Header("═══ Completion")]
    [Tooltip("Minimum total pixels painted before the signature is considered valid.")]
    [Range(10, 100000)] public int minPixelsToPaint = 50000;

    [Tooltip("Optional: show a 'Sign here' prompt; hides when drawing starts.")]
    public GameObject signPromptObject;

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private RawImage _rawImage;
    private Texture2D _tex;
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;

    private bool _isDrawing = false;
    private bool _isHovering = false;
    private Vector2 _lastPos;
    private int _totalPixelsPainted = 0;
    private bool _signatureSubmitted = false;

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
        _rawImage.color = new Color(1f, 1f, 1f, 1f);

        // Set up pen cursor sprite
        if (penCursorImage != null)
        {
            penCursorImage.sizeDelta = penSize;
            var img = penCursorImage.GetComponent<Image>();
            if (img != null && penSprite != null)
                img.sprite = penSprite;
            penCursorImage.gameObject.SetActive(false);
        }

        // Auto-find audio source if none set
        if (penAudioSource == null && interactionController != null)
            penAudioSource = interactionController.audioSource;
    }

    private void Update()
    {
        // Move pen cursor to follow mouse
        if (penCursorImage != null && penCursorImage.gameObject.activeSelf)
            MovePenCursorToMouse();
    }

    // ─────────────────────────────────────────────────────────────
    // POINTER EVENTS
    // ─────────────────────────────────────────────────────────────

    public void OnPointerMove(PointerEventData eventData)
    {
        _isHovering = true;

        if (!showPenOnlyWhileDrawing && penCursorImage != null)
            penCursorImage.gameObject.SetActive(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDrawing = true;
        _lastPos = ScreenToTexCoord(eventData.position);

        if (signPromptObject != null)
            signPromptObject.SetActive(false);

        if (penCursorImage != null)
            penCursorImage.gameObject.SetActive(true);

        StartPenSound();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDrawing) return;
        Vector2 currentPos = ScreenToTexCoord(eventData.position);
        DrawLine(_lastPos, currentPos);
        _lastPos = currentPos;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDrawing = false;
        StopPenSound();

        if (showPenOnlyWhileDrawing && penCursorImage != null)
            penCursorImage.gameObject.SetActive(false);

        if (!_signatureSubmitted && _totalPixelsPainted >= minPixelsToPaint)
        {
            _signatureSubmitted = true;
            StartCoroutine(NotifyAfterDelay(0.6f));
        }
    }

    private void OnDisable()
    {
        StopPenSound();
        if (penCursorImage != null)
            penCursorImage.gameObject.SetActive(false);
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
                alpha = Mathf.Clamp01(alpha);

                Color existing = _tex.GetPixel(x, y);
                Color blended = Color.Lerp(existing, inkColor, inkColor.a * alpha);
                _tex.SetPixel(x, y, blended);
                _totalPixelsPainted++;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // SOUND
    // ─────────────────────────────────────────────────────────────

    private void StartPenSound()
    {
        if (penAudioSource == null || soundPenWriting == null) return;
        penAudioSource.clip = soundPenWriting;
        penAudioSource.volume = penWritingVolume;
        penAudioSource.loop = true;
        penAudioSource.Play();
    }

    private void StopPenSound()
    {
        if (penAudioSource == null) return;
        penAudioSource.loop = false;
        penAudioSource.Stop();
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────

    private Vector2 ScreenToTexCoord(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, screenPos, null, out Vector2 localPoint);
        float u = localPoint.x + _rectTransform.rect.width * 0.5f;
        float v = localPoint.y + _rectTransform.rect.height * 0.5f;
        return new Vector2(u, v);
    }

    private void ClearCanvas()
    {
        Color32[] pixels = new Color32[_tex.width * _tex.height];
        Color32 clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
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