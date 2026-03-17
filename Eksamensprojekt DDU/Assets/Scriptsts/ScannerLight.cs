using System.Collections;
using UnityEngine;

/// <summary>
/// Laver et kort rødt lys-flash når skanneren bruges.
///
/// OPSÆTNING:
///   1. Tilføj dette script til Scanner-objektet (child af kameraet).
///   2. Opret et tomt child GameObject på Scanner-objektet kaldet "ScanLight".
///   3. Tilføj en Light-komponent til ScanLight — sæt Type til Spot.
///   4. Ret ScanLight mod forsiden af skanneren (den retning der peger fremad).
///   5. Træk ScanLight ind i Scanner Light-feltet.
///   6. Træk ScannerLight ind i ScannerDisplay's Scanner Light-felt.
/// </summary>
public class ScannerLight : MonoBehaviour
{
    [Header("Lys")]
    [Tooltip("Light-komponenten der flasher når skanneren bruges.")]
    public Light scanLight;

    [Header("Flash-indstillinger")]
    [Tooltip("Lysets farve — standard rødt som en rigtig skanner.")]
    public Color lightColor = new Color(1f, 0.05f, 0.05f);

    [Tooltip("Lysets intensitet på toppen af flashet.")]
    public float peakIntensity = 3f;

    [Tooltip("Varighed af hele flashet i sekunder.")]
    public float flashDuration = 0.08f;

    // ── Private ────────────────────────────────────────────────────
    private Coroutine _flashRoutine;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (scanLight == null)
            scanLight = GetComponentInChildren<Light>();

        if (scanLight != null)
        {
            scanLight.color = lightColor;
            scanLight.intensity = 0f;
            scanLight.enabled = false;
        }
    }

    /// <summary>
    /// Afspiller et kort lys-flash.
    /// Kaldes af ScannerDisplay når spilleren scanner.
    /// </summary>
    public void Flash()
    {
        if (scanLight == null) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(DoFlash());
    }

    // ──────────────────────────────────────────────────────────────
    IEnumerator DoFlash()
    {
        scanLight.enabled = true;
        scanLight.intensity = peakIntensity;

        // Fade ud over flashDuration
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            scanLight.intensity = Mathf.Lerp(peakIntensity, 0f, t);
            yield return null;
        }

        scanLight.intensity = 0f;
        scanLight.enabled = false;
    }
}