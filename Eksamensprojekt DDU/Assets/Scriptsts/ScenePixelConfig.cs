using UnityEngine;

/// <summary>
/// Placer dette script i din scene for at overstyre pixeleffektens værdier.
/// Har højere prioritet end Volume, men lavere end et aktivt Global Volume
/// med weight = 1. Bruges bedst som fallback eller til hurtige sceneopsætninger.
/// </summary>
public class ScenePixelConfig : MonoBehaviour
{
    [Header("Scene-specifik pixelering")]
    [Range(1f, 16f)]
    [Tooltip("Pixelstørrelse for denne scene.")]
    public float pixelSize = 4f;

    [Range(0f, 1f)]
    [Tooltip("Palettestrength for denne scene.")]
    public float paletteStrength = 0f;

    [Header("Overgangsindstillinger")]
    [Tooltip("Blend blød ind til disse værdier ved scene-start.")]
    public bool fadeOnStart = false;
    [Tooltip("Varighed af fade-ind i sekunder.")]
    public float fadeDuration = 1f;

    // Statisk reference så RenderFeature kan finde den
    public static ScenePixelConfig Current { get; private set; }

    // Interne værdier under fade
    private float _startPixelSize;
    private float _startPaletteStrength;
    private float _fadeTimer = 0f;
    private bool _isFading = false;

    private void OnEnable()
    {
        Current = this;

        if (fadeOnStart)
        {
            // Start fra "ingen effekt" og blend ind
            _startPixelSize = 1f;
            _startPaletteStrength = 0f;
            _fadeTimer = 0f;
            _isFading = true;
        }
    }

    private void OnDisable()
    {
        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        if (!_isFading) return;

        _fadeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_fadeTimer / fadeDuration);
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        // Opdater live-værdier via properties så RenderFeature læser korrekt
        _livePixelSize = Mathf.Lerp(_startPixelSize, pixelSize, smoothT);
        _livePaletteStrength = Mathf.Lerp(_startPaletteStrength, paletteStrength, smoothT);

        if (t >= 1f) _isFading = false;
    }

    // Live-værdier (bruges af RenderFeature)
    private float _livePixelSize = -1f;
    private float _livePaletteStrength = -1f;

    public float LivePixelSize => _isFading ? _livePixelSize : pixelSize;
    public float LivePaletteStrength => _isFading ? _livePaletteStrength : paletteStrength;
}