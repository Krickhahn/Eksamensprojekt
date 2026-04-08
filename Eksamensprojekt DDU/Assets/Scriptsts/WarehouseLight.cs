using System.Collections;
using UnityEngine;

/// <summary>
/// Warehouse Light — drives two child lights in sync:
///   • Spotlight  — illuminates objects below the fixture.
///   • Point Light — illuminates the inside of the lamp housing.
///
/// Plays a constant hum tone, and randomly triggers flicker bursts
/// with an accompanying crackle sound. All parameters are Inspector-tunable.
///
/// Setup
/// ──────
/// 1. Attach this script to your root lamp GameObject.
/// 2. Drag the child Spotlight into "Spotlight" and the child Point Light
///    into "Point Light" in the Inspector.
/// 3. Assign your hum and flicker AudioClips.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WarehouseLight : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector — Lights
    // ─────────────────────────────────────────────────────────────
    [Header("Lights")]
    [Tooltip("The Spotlight child that casts light downward onto the floor/objects.")]
    public Light spotlight;

    [Tooltip("The Point Light child that lights up the interior of the lamp housing.")]
    public Light pointLight;

    // ─────────────────────────────────────────────────────────────
    //  Inspector — Hum
    // ─────────────────────────────────────────────────────────────
    [Header("Hum")]
    [Tooltip("Looping audio clip for the constant electrical hum.")]
    public AudioClip humClip;

    [Tooltip("Volume of the constant hum (0–1).")]
    [Range(0f, 1f)]
    public float humVolume = 0.25f;

    // ─────────────────────────────────────────────────────────────
    //  Inspector — Flicker Rarity
    // ─────────────────────────────────────────────────────────────
    [Header("Flicker Rarity")]
    [Tooltip("Minimum seconds between flicker events.")]
    public float minTimeBetweenFlickers = 3f;

    [Tooltip("Maximum seconds between flicker events.")]
    public float maxTimeBetweenFlickers = 12f;

    // ─────────────────────────────────────────────────────────────
    //  Inspector — Flicker Amount / Intensity
    // ─────────────────────────────────────────────────────────────
    [Header("Flicker Amount")]
    [Tooltip("Minimum number of on/off blinks per flicker event.")]
    [Range(1, 20)]
    public int minBlinks = 2;

    [Tooltip("Maximum number of on/off blinks per flicker event.")]
    [Range(1, 20)]
    public int maxBlinks = 8;

    [Tooltip("Minimum duration (seconds) of each individual blink step.")]
    public float minBlinkDuration = 0.02f;

    [Tooltip("Maximum duration (seconds) of each individual blink step.")]
    public float maxBlinkDuration = 0.12f;

    [Tooltip("How dark the lights get during a flicker (0 = fully off, 1 = no change).")]
    [Range(0f, 1f)]
    public float minIntensityMultiplier = 0f;

    // ─────────────────────────────────────────────────────────────
    //  Inspector — Flicker Sound
    // ─────────────────────────────────────────────────────────────
    [Header("Flicker Sound")]
    [Tooltip("One of these clips is chosen at random for each flicker event (buzz, crackle, etc.). Add as many as you like.")]
    public AudioClip[] flickerClips;

    [Tooltip("Volume of the flicker sound (0–1).")]
    [Range(0f, 1f)]
    public float flickerVolume = 0.6f;

    // ─────────────────────────────────────────────────────────────
    //  Private state
    // ─────────────────────────────────────────────────────────────
    private AudioSource _humSource;
    private AudioSource _flickerSource;

    private float _baseSpotIntensity;
    private float _basePointIntensity;

    private bool _isFlickering;

    // ─────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Cache base intensities so flicker always returns to the
        // values set in the Inspector, regardless of when we read them.
        if (spotlight != null) _baseSpotIntensity = spotlight.intensity;
        if (pointLight != null) _basePointIntensity = pointLight.intensity;

        // Two AudioSources: one loops the hum, the other plays one-shot flicker sounds.
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length < 2)
        {
            gameObject.AddComponent<AudioSource>();
            sources = GetComponents<AudioSource>();
        }

        _humSource = sources[0];
        _flickerSource = sources[1];

        SetupHumSource();
        SetupFlickerSource();
    }

    private void Start()
    {
        StartCoroutine(FlickerScheduler());
    }

    // ─────────────────────────────────────────────────────────────
    //  Audio helpers
    // ─────────────────────────────────────────────────────────────
    private void SetupHumSource()
    {
        _humSource.clip = humClip;
        _humSource.loop = true;
        _humSource.volume = humVolume;
        _humSource.spatialBlend = 1f;   // 3-D; set to 0 for 2-D
        _humSource.playOnAwake = false;

        if (humClip != null)
            _humSource.Play();
    }

    private void SetupFlickerSource()
    {
        _flickerSource.loop = false;
        _flickerSource.volume = flickerVolume;
        _flickerSource.spatialBlend = 1f;
        _flickerSource.playOnAwake = false;
    }

    // ─────────────────────────────────────────────────────────────
    //  Light helpers — both lights always move together
    // ─────────────────────────────────────────────────────────────

    /// <summary>Sets both lights to a fraction of their base intensity.</summary>
    private void SetLightIntensities(float multiplier)
    {
        if (spotlight != null) spotlight.intensity = _baseSpotIntensity * multiplier;
        if (pointLight != null) pointLight.intensity = _basePointIntensity * multiplier;
    }

    /// <summary>Restores both lights to their full base intensities.</summary>
    private void RestoreLights() => SetLightIntensities(1f);

    // ─────────────────────────────────────────────────────────────
    //  Coroutines
    // ─────────────────────────────────────────────────────────────

    /// <summary>Waits a random interval then fires a flicker event, forever.</summary>
    private IEnumerator FlickerScheduler()
    {
        while (true)
        {
            float wait = Random.Range(minTimeBetweenFlickers, maxTimeBetweenFlickers);
            yield return new WaitForSeconds(wait);

            if (!_isFlickering)
                yield return StartCoroutine(DoFlicker());
        }
    }

    /// <summary>Blinks both lights together and plays the flicker sound.</summary>
    private IEnumerator DoFlicker()
    {
        _isFlickering = true;

        // Pick a random flicker clip and play it at the top of the event.
        if (flickerClips != null && flickerClips.Length > 0)
        {
            AudioClip chosen = flickerClips[Random.Range(0, flickerClips.Length)];
            if (chosen != null)
            {
                _flickerSource.volume = flickerVolume;
                _flickerSource.PlayOneShot(chosen);
            }
        }

        int blinks = Random.Range(minBlinks, maxBlinks + 1);

        for (int i = 0; i < blinks; i++)
        {
            // Dim / cut out.
            SetLightIntensities(minIntensityMultiplier);
            yield return new WaitForSeconds(Random.Range(minBlinkDuration, maxBlinkDuration));

            // Restore.
            RestoreLights();
            yield return new WaitForSeconds(Random.Range(minBlinkDuration, maxBlinkDuration));
        }

        // Always finish at full brightness.
        RestoreLights();
        _isFlickering = false;
    }

    // ─────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────

    /// <summary>Manually triggers a flicker event (e.g. from a UnityEvent or another script).</summary>
    public void TriggerFlicker()
    {
        if (!_isFlickering)
            StartCoroutine(DoFlicker());
    }

    /// <summary>Changes the hum volume at runtime.</summary>
    public void SetHumVolume(float volume)
    {
        humVolume = Mathf.Clamp01(volume);
        _humSource.volume = humVolume;
    }

    /// <summary>Changes the flicker sound volume at runtime (applies to all clips).</summary>
    public void SetFlickerVolume(float volume)
    {
        flickerVolume = Mathf.Clamp01(volume);
        _flickerSource.volume = flickerVolume;
    }
}