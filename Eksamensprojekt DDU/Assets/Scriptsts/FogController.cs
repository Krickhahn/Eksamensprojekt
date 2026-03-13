using UnityEngine;

/// <summary>
/// Attach this script to any GameObject to control Unity's built-in scene fog
/// from the Inspector. Changes apply in real-time, including in Edit Mode.
/// </summary>
[ExecuteAlways]
public class FogController : MonoBehaviour
{
    [Header("Enable / Disable")]
    [Tooltip("Toggle fog on or off.")]
    public bool enableFog = true;

    [Header("Fog Mode")]
    [Tooltip("Linear: fog between Start and End distances.\n" +
             "Exponential: fog density grows exponentially.\n" +
             "Exponential Squared: denser exponential falloff.")]
    public FogMode fogMode = FogMode.ExponentialSquared;

    [Header("Color")]
    [Tooltip("Color of the fog.")]
    public Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    [Header("Exponential / Exp² Settings")]
    [Tooltip("Fog density for Exponential and ExponentialSquared modes.")]
    [Range(0f, 1f)]
    public float fogDensity = 0.05f;

    [Header("Linear Fog Settings")]
    [Tooltip("Distance at which linear fog begins (Linear mode only).")]
    public float fogStartDistance = 20f;

    [Tooltip("Distance at which linear fog is fully opaque (Linear mode only).")]
    public float fogEndDistance = 100f;

    // -----------------------------------------------------------------------

    void OnEnable()  => ApplyFog();
    void OnDisable() => RenderSettings.fog = false;

    void Update()
    {
#if UNITY_EDITOR
        // Keep updating in Edit Mode so Inspector tweaks are visible immediately.
        ApplyFog();
#endif
    }

    /// <summary>
    /// Push all public fields into Unity's RenderSettings.
    /// Call this yourself at runtime if you change values via code.
    /// </summary>
    public void ApplyFog()
    {
        RenderSettings.fog            = enableFog;
        RenderSettings.fogMode        = fogMode;
        RenderSettings.fogColor       = fogColor;
        RenderSettings.fogDensity     = fogDensity;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance   = fogEndDistance;
    }

    // -----------------------------------------------------------------------
    // Optional: runtime helpers for animation, scripting, or UI bindings

    /// <summary>Smoothly lerp fog density over <paramref name="duration"/> seconds.</summary>
    public void SetDensitySmooth(float targetDensity, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(LerpDensity(targetDensity, duration));
    }

    private System.Collections.IEnumerator LerpDensity(float target, float duration)
    {
        float start   = fogDensity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed   += Time.deltaTime;
            fogDensity = Mathf.Lerp(start, target, elapsed / duration);
            ApplyFog();
            yield return null;
        }

        fogDensity = target;
        ApplyFog();
    }

    /// <summary>Smoothly lerp fog color over <paramref name="duration"/> seconds.</summary>
    public void SetColorSmooth(Color targetColor, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(LerpColor(targetColor, duration));
    }

    private System.Collections.IEnumerator LerpColor(Color target, float duration)
    {
        Color start   = fogColor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed  += Time.deltaTime;
            fogColor  = Color.Lerp(start, target, elapsed / duration);
            ApplyFog();
            yield return null;
        }

        fogColor = target;
        ApplyFog();
    }
}
