using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Replaces the simple black-fade blink with two eyelid panels that slide
/// together (close) and apart (open) like a real eye, with a black fill
/// that covers the screen once the lids meet.
///
/// Setup:
///   1. Create a UI Canvas child called "EyeBlinkOverlay" (Screen Space Overlay, sort order high e.g. 10)
///   2. Inside it create:
///        - "EyelidTop"    → UI Image, anchor stretch-top,    pivot (0.5, 1)
///        - "EyelidBottom" → UI Image, anchor stretch-bottom, pivot (0.5, 0)
///        - "BlackFill"    → UI Image (black), anchor stretch-all, alpha=0
///   3. Assign all three in the Inspector below.
///   4. On FolderInteractionController, assign this component to the "Eye Blink" slot
///      and REMOVE the old blinkOverlay CanvasGroup reference (leave it null).
/// </summary>
public class EyeBlinkController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // INSPECTOR PARAMETERS
    // ─────────────────────────────────────────────────────────────

    [Header("═══ Eyelid Images")]
    [Tooltip("Top eyelid Image (anchored to top of screen).")]
    public RectTransform eyelidTop;

    [Tooltip("Bottom eyelid Image (anchored to bottom of screen).")]
    public RectTransform eyelidBottom;

    [Tooltip("Full-screen black Image shown when eye is fully closed.")]
    public Image blackFill;

    [Header("═══ Eyelid Appearance")]
    [Tooltip("Color of the eyelid skin.")]
    public Color eyelidColor = new Color(0.18f, 0.13f, 0.11f, 1f);

    [Tooltip("Height of each eyelid panel at rest (fully open). Should be ~0 or slightly negative so they're offscreen.")]
    public float eyelidRestHeight = -20f;

    [Tooltip("Height each eyelid travels to fully close the eye (in pixels). Should be ~half screen height + overlap.")]
    public float eyelidCloseHeight = 600f;

    [Header("═══ Timing")]
    [Tooltip("Duration of the eye closing animation.")]
    [Range(0.05f, 2f)] public float closeDuration = 0.35f;

    [Tooltip("Duration of the eye opening animation.")]
    [Range(0.05f, 2f)] public float openDuration = 0.45f;

    [Tooltip("How long the eye stays shut between close and open.")]
    [Range(0f, 3f)] public float holdDuration = 0.15f;

    [Header("═══ Easing")]
    [Tooltip("Curve for the closing movement (suggest ease-in).")]
    public AnimationCurve closeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Curve for the opening movement (suggest ease-out).")]
    public AnimationCurve openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private Image _topImage;
    private Image _botImage;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Persist into the next scene so SceneOpenEyes can open the eyes there
        DontDestroyOnLoad(transform.root.gameObject);

        if (eyelidTop != null) _topImage = eyelidTop.GetComponent<Image>();
        if (eyelidBottom != null) _botImage = eyelidBottom.GetComponent<Image>();

        ApplyEyelidColor();
        SetEyelidsOpen();

        if (blackFill != null)
        {
            var c = blackFill.color;
            c.a = 0f;
            blackFill.color = c;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // PUBLIC API — called by FolderInteractionController
    // ─────────────────────────────────────────────────────────────

    /// <summary>Full blink: close → hold → open.</summary>
    public IEnumerator Blink()
    {
        yield return StartCoroutine(CloseEye());
        yield return new WaitForSeconds(holdDuration);
        yield return StartCoroutine(OpenEye());
    }

    /// <summary>Close eye only (e.g. before scene switch — leave closed).</summary>
    public IEnumerator CloseEye()
    {
        yield return StartCoroutine(AnimateEyelids(eyelidRestHeight, eyelidCloseHeight, closeDuration, closeCurve));
        // Once fully closed, show the black fill and hide eyelids (seamless)
        SetBlackFillAlpha(1f);
        SetEyelidsOpen(); // reset lids so they're ready for next open
    }

    /// <summary>Open eye from black fill.</summary>
    public IEnumerator OpenEye()
    {
        // Start from closed state: black fill visible, lids hidden
        SetBlackFillAlpha(1f);
        SetEyelidsAt(eyelidCloseHeight);

        // Fade out black fill slightly before the lids animate (feels more natural)
        yield return StartCoroutine(FadeBlackFill(1f, 0f, openDuration * 0.3f));

        // Animate lids opening
        yield return StartCoroutine(AnimateEyelids(eyelidCloseHeight, eyelidRestHeight, openDuration, openCurve));
    }

    // ─────────────────────────────────────────────────────────────
    // ANIMATION HELPERS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator AnimateEyelids(float fromHeight, float toHeight, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            float h = Mathf.Lerp(fromHeight, toHeight, t);
            SetEyelidsAt(h);
            yield return null;
        }

        SetEyelidsAt(toHeight);
    }

    private IEnumerator FadeBlackFill(float from, float to, float duration)
    {
        if (blackFill == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetBlackFillAlpha(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetBlackFillAlpha(to);
    }

    // ─────────────────────────────────────────────────────────────
    // SETTERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the height (sizeDelta.y) of both eyelid panels.
    /// Top eyelid grows downward, bottom eyelid grows upward.
    /// </summary>
    private void SetEyelidsAt(float height)
    {
        if (eyelidTop != null)
        {
            var sd = eyelidTop.sizeDelta;
            sd.y = height;
            eyelidTop.sizeDelta = sd;
        }

        if (eyelidBottom != null)
        {
            var sd = eyelidBottom.sizeDelta;
            sd.y = height;
            eyelidBottom.sizeDelta = sd;
        }
    }

    private void SetEyelidsOpen() => SetEyelidsAt(eyelidRestHeight);

    private void SetBlackFillAlpha(float a)
    {
        if (blackFill == null) return;
        var c = blackFill.color;
        c.a = a;
        blackFill.color = c;
    }

    private void ApplyEyelidColor()
    {
        if (_topImage != null) _topImage.color = eyelidColor;
        if (_botImage != null) _botImage.color = eyelidColor;
    }
}