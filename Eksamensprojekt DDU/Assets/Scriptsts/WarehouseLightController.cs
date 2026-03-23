using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Styrer alle lys i varehuset og aktiverer WeepingAngel når lyset slukker.
/// Lyset slukker tilfældigt baseret på et interval.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et tomt GameObject i scenen.
///   2. Træk ALLE lys-objekter i varehuset ind i warehouseLights listen.
///   3. Træk WeepingAngelEnemy ind i angel feltet.
///   4. Træk AngelPowerSwitch ind i powerSwitch feltet.
///   5. Justér minTimeBetweenFailures og maxTimeBetweenFailures.
/// </summary>
public class WarehouseLightController : MonoBehaviour
{
    public static WarehouseLightController Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Lys")]
    [Tooltip("Alle lys-komponenter i varehuset der slukkes ved strømsvigt.")]
    public List<Light> warehouseLights = new List<Light>();

    [Tooltip("Hvor hurtigt lyset fader ud ved strømsvigt (sekunder).")]
    public float fadeOutDuration = 0.3f;

    [Tooltip("Hvor hurtigt lyset fader ind når strøm genopstår (sekunder).")]
    public float fadeInDuration = 1.5f;

    [Header("Tilfældig strømsvigt")]
    [Tooltip("Minimum sekunder mellem strømsvigt.")]
    public float minTimeBetweenFailures = 30f;

    [Tooltip("Maksimum sekunder mellem strømsvigt.")]
    public float maxTimeBetweenFailures = 90f;

    [Tooltip("Sekunder inden første mulige strømsvigt efter spilstart.")]
    public float initialDelay = 20f;

    [Header("Referencer")]
    [Tooltip("Weeping Angel fjenden der aktiveres ved strømsvigt.")]
    public WeepingAngelEnemy angel;

    [Tooltip("Stikkontakten spilleren aktiverer for at gendanne strøm.")]
    public AngelPowerSwitch powerSwitch;

    [Header("Events (valgfrit)")]
    public UnityEvent onLightOff;
    public UnityEvent onLightOn;

    // ── Runtime state ──────────────────────────────────────────────
    private bool _isPowerOn = true;
    private List<float> _originalIntensities = new List<float>();

    public bool IsPowerOn => _isPowerOn;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Gem original lysstyrke for hvert lys
        foreach (Light l in warehouseLights)
            _originalIntensities.Add(l != null ? l.intensity : 1f);

        powerSwitch?.SetSwitchAvailable(false);

        StartCoroutine(RandomFailureLoop());
    }

    // ── Tilfældig strømsvigt ───────────────────────────────────────

    IEnumerator RandomFailureLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float waitTime = Random.Range(minTimeBetweenFailures, maxTimeBetweenFailures);
            yield return new WaitForSeconds(waitTime);

            if (_isPowerOn)
                TriggerPowerFailure();
        }
    }

    // ── Strømsvigt og genopretning ─────────────────────────────────

    /// <summary>Slukker alle lys og aktiverer englen.</summary>
    public void TriggerPowerFailure()
    {
        if (!_isPowerOn) return;
        _isPowerOn = false;

        Debug.Log("[LightController] Strømsvigt!");

        StartCoroutine(FadeAllLights(0f, fadeOutDuration, () =>
        {
            angel?.Activate();
            powerSwitch?.SetSwitchAvailable(true);
            onLightOff?.Invoke();
        }));
    }

    /// <summary>Tænder alle lys og deaktiverer englen. Kaldes af AngelPowerSwitch.</summary>
    public void RestorePower()
    {
        if (_isPowerOn) return;
        _isPowerOn = true;

        Debug.Log("[LightController] Strøm genoprettet!");

        angel?.Deactivate();
        powerSwitch?.SetSwitchAvailable(false);

        StartCoroutine(FadeAllLights(1f, fadeInDuration, () =>
        {
            onLightOn?.Invoke();
        }));
    }

    // ── Fade alle lys ──────────────────────────────────────────────

    IEnumerator FadeAllLights(float targetFraction, float duration, System.Action onComplete = null)
    {
        float elapsed = 0f;

        // Gem startværdier
        List<float> startIntensities = new List<float>();
        for (int i = 0; i < warehouseLights.Count; i++)
        {
            Light l = warehouseLights[i];
            startIntensities.Add(l != null ? l.intensity : 0f);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < warehouseLights.Count; i++)
            {
                if (warehouseLights[i] == null) continue;
                float target = _originalIntensities[i] * targetFraction;
                warehouseLights[i].intensity = Mathf.Lerp(startIntensities[i], target, t);
            }

            yield return null;
        }

        // Sæt præcise slutværdier
        for (int i = 0; i < warehouseLights.Count; i++)
        {
            if (warehouseLights[i] == null) continue;
            warehouseLights[i].intensity = _originalIntensities[i] * targetFraction;
        }

        onComplete?.Invoke();
    }

    // ── Manuelt sluk til test (tryk F i spillet) ───────────────────
#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (_isPowerOn) TriggerPowerFailure();
            else RestorePower();
        }
    }
#endif
}