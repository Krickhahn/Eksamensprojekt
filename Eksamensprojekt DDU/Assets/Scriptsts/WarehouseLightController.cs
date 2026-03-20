using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Styrer et rums lys og kommunikerer med WeepingAngelEnemy.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et GameObject i rummet.
///   2. Træk rummets Light-komponent ind i roomLight.
///   3. Træk WeepingAngelEnemy ind i angel.
///   4. Træk AngelPowerSwitch ind i powerSwitch.
///   5. Kald TriggerPowerFailure() fra din sabotør-fjende når den slukker lyset.
///
/// FLOW:
///   TriggerPowerFailure() → lyset slukkes → statuen aktiveres
///   AngelPowerSwitch kalder RestorePower() → cooldown → lyset tændes → statuen fryser
/// </summary>
public class WarehouseLightController : MonoBehaviour
{
    // ── Singleton (per rum) ────────────────────────────────────────
    public static WarehouseLightController Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Referencer")]
    [Tooltip("Light-komponenten der repræsenterer rummets lys.")]
    public Light roomLight;

    [Tooltip("Statuen der aktiveres når lyset slukkes.")]
    public WeepingAngelEnemy angel;

    [Tooltip("Stikkontakten spilleren skal aktivere for at gendanne lyset.")]
    public AngelPowerSwitch powerSwitch;

    [Header("Lys-indstillinger")]
    [Tooltip("Lysstyrke når lyset er tændt.")]
    public float lightOnIntensity = 1f;

    [Tooltip("Lysstyrke når lyset er slukket (0 = helt mørkt).")]
    public float lightOffIntensity = 0f;

    [Tooltip("Sekunder det tager at fade lyset ud når det slukkes.")]
    public float fadeOutDuration = 0.5f;

    [Tooltip("Sekunder det tager at fade lyset ind når det tændes.")]
    public float fadeInDuration = 1.5f;

    [Header("Events (valgfrit)")]
    public UnityEvent onLightOff;
    public UnityEvent onLightOn;

    // ── Runtime state ──────────────────────────────────────────────
    private bool _isPowerOn = true;
    private Coroutine _fadeRoutine;

    public bool IsPowerOn => _isPowerOn;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (roomLight != null)
            roomLight.intensity = lightOnIntensity;

        // Sørg for at stikkontakten er deaktiveret ved start
        powerSwitch?.SetSwitchAvailable(false);
    }

    // ── Offentlige metoder ─────────────────────────────────────────

    /// <summary>
    /// Slukker lyset og aktiverer statuen.
    /// Kaldes af sabotør-fjenden eller et andet event.
    /// </summary>
    public void TriggerPowerFailure()
    {
        if (!_isPowerOn) return;

        _isPowerOn = false;
        Debug.Log("[LightController] Strømsvigt! Lyset slukker.");

        StartFade(lightOffIntensity, fadeOutDuration, () =>
        {
            angel?.Activate();
            powerSwitch?.SetSwitchAvailable(true);
            onLightOff?.Invoke();
        });
    }

    /// <summary>
    /// Gendanner lyset og deaktiverer statuen permanent.
    /// Kaldes af AngelPowerSwitch når cooldown er overstået.
    /// </summary>
    public void RestorePower()
    {
        if (_isPowerOn) return;

        _isPowerOn = true;
        Debug.Log("[LightController] Strøm genoprettet!");

        angel?.Deactivate();
        powerSwitch?.SetSwitchAvailable(false);

        StartFade(lightOnIntensity, fadeInDuration, () =>
        {
            onLightOn?.Invoke();
        });
    }

    // ── Fade-logik ─────────────────────────────────────────────────

    void StartFade(float targetIntensity, float duration, System.Action onComplete = null)
    {
        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeLight(targetIntensity, duration, onComplete));
    }

    IEnumerator FadeLight(float targetIntensity, float duration, System.Action onComplete)
    {
        if (roomLight == null) { onComplete?.Invoke(); yield break; }

        float startIntensity = roomLight.intensity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            roomLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, elapsed / duration);
            yield return null;
        }

        roomLight.intensity = targetIntensity;
        onComplete?.Invoke();
    }
}