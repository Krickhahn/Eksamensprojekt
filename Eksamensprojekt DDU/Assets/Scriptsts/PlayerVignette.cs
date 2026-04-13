using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Håndterer rød vignette-effekt når spilleren tager skade eller dør.
/// Kræver et Canvas med et Image-element i fuld skærmstørrelse.
///
/// Opsætning i Unity:
///   1. Tilføj dette script på Player-objektet (samme som PlayerMovement).
///   2. Lav et Canvas (Screen Space - Overlay) som barn af Player-kameraet
///      — eller et separat Canvas i scenen.
///   3. Tilføj et Image-element inde i Canvas:
///      - Rect Transform: Anchor = stretch/stretch, Left/Right/Top/Bottom = 0
///      - Color: rød med alpha 0 (scriptet styrer alpha selv)
///      - Raycast Target: slået FRA
///   4. Træk Image-elementet ind i feltet "Vignette Image" i Inspector.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerVignette : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Image-elementet der bruges som rød vignette. Sæt farven til rød i Inspector.")]
    public Image vignetteImage;

    [Header("Skade-vignette")]
    [Tooltip("Hvor hurtigt vignetten fader ind når spilleren rammes.")]
    public float damageFlashSpeed = 8f;
    [Tooltip("Alpha-værdien på vignetten ved skade (0–1).")]
    [Range(0f, 1f)]
    public float damageAlpha = 0.5f;

    [Header("Døds-vignette")]
    [Tooltip("Hvor hurtigt skærmen fader til sort-rødt når spilleren dør.")]
    public float deathFadeSpeed = 1.5f;
    [Tooltip("Alpha-værdien på vignetten ved død.")]
    [Range(0f, 1f)]
    public float deathAlpha = 0.85f;

    private Coroutine _vignetteCoroutine;

    // ── Offentlige metoder kaldt af PlayerMovement ────────────────

    /// <summary>Viser rød vignette og fader den ud igen efter recoverTime sekunder.</summary>
    public void ShowDamage(float recoverTime)
    {
        if (_vignetteCoroutine != null) StopCoroutine(_vignetteCoroutine);
        _vignetteCoroutine = StartCoroutine(DamageVignetteCoroutine(recoverTime));
    }

    /// <summary>Fader skærmen permanent til mørk rød — spilleren er død.</summary>
    public void ShowDeath()
    {
        if (_vignetteCoroutine != null) StopCoroutine(_vignetteCoroutine);
        _vignetteCoroutine = StartCoroutine(DeathVignetteCoroutine());
    }

    // ── Coroutines ────────────────────────────────────────────────

    IEnumerator DamageVignetteCoroutine(float recoverTime)
    {
        if (vignetteImage == null) yield break;

        // Flash ind hurtigt
        yield return FadeVignette(damageAlpha, damageFlashSpeed);

        // Hold i et kort øjeblik
        yield return new WaitForSeconds(0.3f);

        // Fade ud langsomt over recoverTime
        float fadeOutSpeed = damageAlpha / recoverTime;
        yield return FadeVignette(0f, fadeOutSpeed);
    }

    IEnumerator DeathVignetteCoroutine()
    {
        if (vignetteImage == null) yield break;

        // Fade til mørk rød — bliver permanent
        yield return FadeVignette(deathAlpha, deathFadeSpeed);
    }

    IEnumerator FadeVignette(float targetAlpha, float speed)
    {
        if (vignetteImage == null) yield break;

        Color c = vignetteImage.color;
        while (!Mathf.Approximately(c.a, targetAlpha))
        {
            c.a = Mathf.MoveTowards(c.a, targetAlpha, speed * Time.deltaTime);
            vignetteImage.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        vignetteImage.color = c;
    }
}