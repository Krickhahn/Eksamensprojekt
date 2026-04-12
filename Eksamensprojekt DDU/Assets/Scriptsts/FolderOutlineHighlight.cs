using UnityEngine;

/// <summary>
/// Secondary hover highlighter — drives a separate "outline" child mesh
/// (e.g., a slightly scaled duplicate of the folder with an inverted-normal or toon-outline shader).
/// 
/// Place this on the same GameObject as FolderInteractionController, OR on the outline child itself.
/// Works independently of the emission approach in FolderInteractionController,
/// so you can use one or both techniques.
/// </summary>
public class FolderOutlineHighlight : MonoBehaviour
{
    [Header("═══ References")]
    [Tooltip("The Renderer of the outline mesh (child object, slightly bigger, facing-outward normals / outline shader).")]
    public Renderer outlineRenderer;

    [Header("═══ Outline Colors")]
    [Tooltip("Color of the outline border when NOT hovered.")]
    public Color idleColor = new Color(0.8f, 0.8f, 0.8f, 0f);   // transparent by default

    [Tooltip("Color of the outline border when hovered.")]
    public Color hoverColor = new Color(0.3f, 0.7f, 1f, 1f);

    [Header("═══ Animation")]
    [Tooltip("Speed at which outline alpha/color lerps in and out.")]
    [Range(1f, 20f)] public float transitionSpeed = 8f;

    [Tooltip("Pulse the border brightness while hovered.")]
    public bool pulseOnHover = true;

    [Tooltip("Speed of the hover pulse.")]
    [Range(0.5f, 10f)] public float pulseSpeed = 3f;

    [Tooltip("Pulse brightness range (min, max).")]
    public Vector2 pulseRange = new Vector2(0.6f, 1.2f);

    [Header("═══ Outline Shader Property")]
    [Tooltip("The shader property name for the outline color (common: _OutlineColor, _Color, _EmissionColor).")]
    public string shaderColorProperty = "_OutlineColor";

    // ─────────────────────────────────────────────────────────────

    private Material _mat;
    private bool _hovered = false;
    private Color _currentColor;
    private static readonly int ColorProp = Shader.PropertyToID("_OutlineColor");

    private void Awake()
    {
        if (outlineRenderer != null)
        {
            _mat = outlineRenderer.material;
            _currentColor = idleColor;
            ApplyColor(_currentColor);

            // Hide outline mesh at start if idle is transparent
            outlineRenderer.enabled = (idleColor.a > 0.01f);
        }
    }

    private void OnMouseEnter()
    {
        _hovered = true;
        if (outlineRenderer != null) outlineRenderer.enabled = true;
    }

    private void OnMouseExit()
    {
        _hovered = false;
    }

    private void Update()
    {
        if (_mat == null) return;

        Color target = _hovered ? hoverColor : idleColor;

        // Pulse brightness while hovered
        if (_hovered && pulseOnHover)
        {
            float brightness = Mathf.Lerp(pulseRange.x, pulseRange.y,
                (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);
            target = hoverColor * brightness;
            target.a = hoverColor.a; // preserve alpha
        }

        _currentColor = Color.Lerp(_currentColor, target, Time.deltaTime * transitionSpeed);
        ApplyColor(_currentColor);

        // Disable renderer once fully faded out
        if (!_hovered && _currentColor.a < 0.01f && outlineRenderer.enabled)
            outlineRenderer.enabled = false;
    }

    private void ApplyColor(Color c)
    {
        // Try the named property, fall back to _Color
        if (_mat.HasProperty(shaderColorProperty))
            _mat.SetColor(shaderColorProperty, c);
        else if (_mat.HasProperty("_Color"))
            _mat.SetColor("_Color", c);
    }
}
