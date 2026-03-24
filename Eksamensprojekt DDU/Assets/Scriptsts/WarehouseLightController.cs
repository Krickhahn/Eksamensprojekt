using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Styrer alle lys i varehuset og aktiverer WeepingAngel tilfældigt.
///
/// VED SPILSTART trækkes der et tilfældigt aktiveringstidspunkt:
///   - En chance for at englen slet ikke aktiverer i dette spil
///   - Ellers trækkes en tid inden for minActivationTime og maxActivationTime
///
/// LYSET slukker tilfældigt baseret på et interval uanset om englen aktiverer.
/// Englen jager KUN når lyset er slukket og stopper når det tændes igen.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et tomt GameObject.
///   2. Træk ALLE lys-objekter i varehuset ind i warehouseLights.
///   3. Træk AngelPowerSwitch ind i powerSwitch.
///   4. WeepingAngelEnemy findes automatisk i scenen.
/// </summary>
public class WarehouseLightController : MonoBehaviour
{
    public static WarehouseLightController Instance { get; private set; }

    [Header("Lys")]
    [Tooltip("Alle lys-komponenter i varehuset.")]
    public List<Light> warehouseLights = new List<Light>();

    public float fadeOutDuration = 0.3f;
    public float fadeInDuration = 1.5f;

    [Header("Strømsvigt")]
    [Tooltip("Minimum sekunder mellem strømsvigtsforsøg.")]
    public float minTimeBetweenFailures = 30f;

    [Tooltip("Maksimum sekunder mellem strømsvigtsforsøg.")]
    public float maxTimeBetweenFailures = 90f;

    [Tooltip("Sekunder inden første mulige strømsvigt.")]
    public float initialDelay = 20f;

    [Header("Engelaktivering")]
    [Tooltip("Chance for at englen overhovedet aktiverer i dette spil (0-1).\n" +
             "0 = aldrig aktiv, 1 = altid aktiv.")]
    [Range(0f, 1f)]
    public float chanceOfAngel = 0.75f;

    [Tooltip("Tidligste tidspunkt englen kan aktivere (sekunder efter spilstart).")]
    public float minActivationTime = 30f;

    [Tooltip("Seneste tidspunkt englen kan aktivere (sekunder efter spilstart).")]
    public float maxActivationTime = 180f;

    [Header("Stikkontakt")]
    public AngelPowerSwitch powerSwitch;

    [Header("Events (valgfrit)")]
    public UnityEvent onLightOff;
    public UnityEvent onLightOn;

    // ── Runtime state ──────────────────────────────────────────────
    private bool _isPowerOn = true;
    private WeepingAngelEnemy _angel;
    private List<float> _originalIntensities = new List<float>();
    private float _angelActivationTime = -1f; // -1 = aktiverer ikke dette spil
    private bool _angelActivated = false;

    public bool IsPowerOn => _isPowerOn;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Find englen automatisk
        _angel = FindAnyObjectByType<WeepingAngelEnemy>();
        if (_angel == null)
            Debug.LogWarning("[LightController] Ingen WeepingAngelEnemy fundet i scenen.");

        // Gem original lysstyrke
        foreach (Light l in warehouseLights)
            _originalIntensities.Add(l != null ? l.intensity : 1f);

        powerSwitch?.SetSwitchAvailable(false);

        // Træk ved spilstart: aktiverer englen dette spil?
        if (Random.value <= chanceOfAngel)
        {
            _angelActivationTime = Random.Range(minActivationTime, maxActivationTime);
            Debug.Log($"[LightController] Englen aktiverer om {_angelActivationTime:F0} sekunder.");
        }
        else
        {
            Debug.Log("[LightController] Englen aktiverer IKKE dette spil.");
        }

        StartCoroutine(PowerFailureLoop());
        StartCoroutine(AngelActivationTimer());
    }

    // ── Timere ─────────────────────────────────────────────────────

    IEnumerator AngelActivationTimer()
    {
        if (_angelActivationTime < 0f) yield break;

        yield return new WaitForSeconds(_angelActivationTime);
        _angelActivated = true;
        Debug.Log("[LightController] Englen er nu klar til at aktivere ved næste strømsvigt.");
    }

    IEnumerator PowerFailureLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            float wait = Random.Range(minTimeBetweenFailures, maxTimeBetweenFailures);
            yield return new WaitForSeconds(wait);

            if (_isPowerOn)
                TriggerPowerFailure();
        }
    }

    // ── Strømsvigt og genopretning ─────────────────────────────────

    public void TriggerPowerFailure()
    {
        if (!_isPowerOn) return;
        _isPowerOn = false;

        bool activateAngel = _angelActivated && _angel != null;
        Debug.Log($"[LightController] Strømsvigt! Engel aktiveres: {activateAngel}");

        StartCoroutine(FadeAllLights(0f, fadeOutDuration, () =>
        {
            if (activateAngel)
                _angel.OnLightOff();

            powerSwitch?.SetSwitchAvailable(true);
            onLightOff?.Invoke();
        }));
    }

    public void RestorePower()
    {
        if (_isPowerOn) return;
        _isPowerOn = true;

        Debug.Log("[LightController] Strøm genoprettet!");

        // Stop englen hvis den jager
        if (_angel != null &&
            (_angel.CurrentState == WeepingAngelEnemy.AngelState.Hunting ||
             _angel.CurrentState == WeepingAngelEnemy.AngelState.Frozen))
        {
            _angel.OnLightOn();
        }

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
        List<float> startIntensities = new List<float>();
        for (int i = 0; i < warehouseLights.Count; i++)
            startIntensities.Add(warehouseLights[i] != null ? warehouseLights[i].intensity : 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < warehouseLights.Count; i++)
            {
                if (warehouseLights[i] == null) continue;
                warehouseLights[i].intensity = Mathf.Lerp(
                    startIntensities[i], _originalIntensities[i] * targetFraction, t);
            }
            yield return null;
        }

        for (int i = 0; i < warehouseLights.Count; i++)
        {
            if (warehouseLights[i] == null) continue;
            warehouseLights[i].intensity = _originalIntensities[i] * targetFraction;
        }

        onComplete?.Invoke();
    }

    // ── Test-tast (kun i editor) ───────────────────────────────────
#if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (_isPowerOn) TriggerPowerFailure();
            else RestorePower();
        }
    }
#endif
}