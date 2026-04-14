using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Styrer arbejdsskiftets timer og point-pres.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Sæt Shift Duration til skiftets længde i sekunder (f.eks. 300 = 5 min).
///   3. Forbind events til lys, lyd og andre systemer.
///
/// GAMEPLAY-LOGIK:
///   Skiftet har tre faser:
///     Normal    (0–60%)  — fuld score-multiplikator
///     Pressure  (60–85%) — reduceret multiplikator, visuelt signal
///     Overtime  (85–100%)— kraftigt reduceret multiplikator, stærkt visuelt signal
///   Når timeren løber ud afsluttes skiftet automatisk — spilleren kan stadig
///   bruge udgangen og se sin score, men timer-bonussen er væk.
///
///   POINT-STRAF FOR VENTEN:
///   Ordrer der stadig er aktive når skiftet slutter giver 0 point.
///   Multiplikatoren reduceres gradvist i Pressure- og Overtime-faserne
///   så det altid kan betale sig at aflevere hurtigt.
///
/// LYD:
///   Tilføj en AudioSource-komponent til dette GameObject (Play On Awake: OFF, Loop: OFF).
///   Fyld Pressure-, Overtime- og Shift Ended-arrays med dine clips i Inspector.
///   Scriptet vælger én tilfældig clip og afspiller den én gang ved faseskift.
/// </summary>
public class ShiftTimer : MonoBehaviour
{
    public static ShiftTimer Instance { get; private set; }

    [Header("Timer")]
    [Tooltip("Skiftets samlede varighed i sekunder.")]
    public float shiftDuration = 300f;

    [Tooltip("Procent af skiftet hvor Pressure-fasen starter (0–1).")]
    [Range(0f, 1f)]
    public float pressureThreshold = 0.6f;

    [Tooltip("Procent af skiftet hvor Overtime-fasen starter (0–1).")]
    [Range(0f, 1f)]
    public float overtimeThreshold = 0.85f;

    [Header("Score-multiplikatorer")]
    [Tooltip("Multiplikator i Normal-fasen.")]
    public float normalMultiplier = 1.0f;

    [Tooltip("Multiplikator i Pressure-fasen.")]
    public float pressureMultiplier = 0.6f;

    [Tooltip("Multiplikator i Overtime-fasen.")]
    public float overtimeMultiplier = 0.25f;

    [Header("Events")]
    [Tooltip("Kaldes hvert sekund med (elapsed, total, progress 0-1).")]
    public UnityEvent<float, float, float> onTimerTick;

    [Tooltip("Kaldes når Pressure-fasen starter.")]
    public UnityEvent onPressureStart;

    [Tooltip("Kaldes når Overtime-fasen starter.")]
    public UnityEvent onOvertimeStart;

    [Tooltip("Kaldes når timeren løber ud — skiftet er slut.")]
    public UnityEvent onShiftEnd;

    // ── Lyd ───────────────────────────────────────────────────────

    [Header("Lyd — Pressure-fase")]
    [Tooltip("Lyde der afspilles når Pressure-fasen starter. Én vælges tilfældigt.\n" +
             "Brug fx en stressende lyd eller en anspændt jingle.\n" +
             "Kræver en AudioSource-komponent på dette GameObject.")]
    public AudioClip[] pressureSounds;

    [Tooltip("Lydstyrke for Pressure-lyde (0–1).")]
    [Range(0f, 1f)]
    public float pressureVolume = 1f;

    [Header("Lyd — Overtime-fase")]
    [Tooltip("Lyde der afspilles når Overtime-fasen starter. Én vælges tilfældigt.\n" +
             "Brug fx en alarm eller en mere intens lyd end Pressure.")]
    public AudioClip[] overtimeSounds;

    [Tooltip("Lydstyrke for Overtime-lyde (0–1).")]
    [Range(0f, 1f)]
    public float overtimeVolume = 1f;

    [Header("Lyd — Skiftet er slut")]
    [Tooltip("Lyde der afspilles når timeren løber ud. Én vælges tilfældigt.\n" +
             "Brug fx en afslutningsjingle eller en slutfanfare.")]
    public AudioClip[] shiftEndedSounds;

    [Tooltip("Lydstyrke for skift-slut-lyde (0–1).")]
    [Range(0f, 1f)]
    public float shiftEndedVolume = 1f;

    // ── Runtime ────────────────────────────────────────────────────
    public float Elapsed { get; private set; }
    public float Progress => Mathf.Clamp01(Elapsed / shiftDuration);
    public bool IsRunning { get; private set; }
    public bool ShiftEnded { get; private set; }

    public enum ShiftPhase { Normal, Pressure, Overtime, Ended }
    public ShiftPhase Phase { get; private set; } = ShiftPhase.Normal;

    /// <summary>Aktuel score-multiplikator baseret på nuværende fase.</summary>
    public float ScoreMultiplier
    {
        get
        {
            switch (Phase)
            {
                case ShiftPhase.Pressure: return pressureMultiplier;
                case ShiftPhase.Overtime: return overtimeMultiplier;
                case ShiftPhase.Ended: return 0f;
                default: return normalMultiplier;
            }
        }
    }

    private float _lastTickSecond;
    private bool _pressureFired;
    private bool _overtimeFired;
    private AudioSource _audio;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.spatialBlend = 0f; // 2D-lyd — fase-lyde hører typisk til UI/ambient
        }
    }

    void Start()
    {
        IsRunning = true;
    }

    void Update()
    {
        if (!IsRunning || ShiftEnded) return;

        Elapsed += Time.deltaTime;
        float progress = Progress;

        // Sekund-tick til UI
        if (Mathf.FloorToInt(Elapsed) > Mathf.FloorToInt(_lastTickSecond))
        {
            _lastTickSecond = Elapsed;
            onTimerTick?.Invoke(Elapsed, shiftDuration, progress);
        }

        // Faseskift
        if (!_pressureFired && progress >= pressureThreshold)
        {
            _pressureFired = true;
            Phase = ShiftPhase.Pressure;
            onPressureStart?.Invoke();
            PlayRandomSound(pressureSounds, pressureVolume);
            Debug.Log("[ShiftTimer] Pressure-fase starter!");
        }

        if (!_overtimeFired && progress >= overtimeThreshold)
        {
            _overtimeFired = true;
            Phase = ShiftPhase.Overtime;
            onOvertimeStart?.Invoke();
            PlayRandomSound(overtimeSounds, overtimeVolume);
            Debug.Log("[ShiftTimer] Overtime-fase starter!");
        }

        // Timer løber ud
        if (Elapsed >= shiftDuration)
        {
            Elapsed = shiftDuration;
            IsRunning = false;
            ShiftEnded = true;
            Phase = ShiftPhase.Ended;
            onShiftEnd?.Invoke();
            PlayRandomSound(shiftEndedSounds, shiftEndedVolume);
            Debug.Log("[ShiftTimer] Skiftet er slut!");
        }
    }

    // ── Lyd-hjælper ───────────────────────────────────────────────

    /// <summary>Spiller en tilfældig clip fra arrayet, hvis det ikke er tomt.</summary>
    void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (_audio == null || clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
            _audio.PlayOneShot(clip, volume);
    }

    // ── Offentlige metoder ─────────────────────────────────────────

    /// <summary>
    /// Formaterer den resterende tid som MM:SS.
    /// Bruges til at vise timeren på skannerdisplayet.
    /// </summary>
    public string GetTimeRemainingFormatted()
    {
        float remaining = Mathf.Max(0f, shiftDuration - Elapsed);
        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>Stopper timeren — kaldes af ExitDoor når spilleren forlader.</summary>
    public void StopTimer()
    {
        IsRunning = false;
    }
}