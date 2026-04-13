using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

/// <summary>
/// Place this on any GameObject in the NEXT scene.
///
/// Uses the PERSISTED EyeBlinkController from the previous scene (DontDestroyOnLoad).
/// BlackFill stays fully opaque through the scene switch — no flash.
/// Sequence:
///   1. Wait for scene to settle
///   2. Fade BlackFill from 1 → 0  (world becomes visible behind closed lids)
///   3. Open eyelids + sweep EQ up simultaneously
///   4. Play door sound after adjustable delay
///   5. Destroy the persisted EyeBlinkCanvas
///
/// NO separate EyeBlinkCanvas needed in the second scene.
/// </summary>
public class SceneOpenEyes : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // INSPECTOR
    // ─────────────────────────────────────────────────────────────

    [Header("═══ Local Black Fill")]
    [Tooltip("A full-screen black Image on this scene's own canvas. Set to alpha 1 by default in the editor. This takes over from the persisted BlackFill instantly when the scene loads, preventing any flash.")]
    public Image localBlackFill;

    [Header("═══ EQ Sweep")]
    [Tooltip("Optional AudioMixer with LowPassCutoff exposed. Leave null if not using EQ sweep.")]
    public AudioMixer gameMixer;

    [Tooltip("Exact name of the exposed Low Pass parameter.")]
    public string lowPassParameterName = "LowPassCutoff";

    [Tooltip("Frequency to sweep TO when eyes open. Match eqOpenFrequency in FolderInteractionController.")]
    [Range(100f, 22000f)] public float eqOpenFrequency = 22000f;

    [Tooltip("Starting frequency (closed state). Match eqClosedFrequency in FolderInteractionController.")]
    [Range(10f, 5000f)] public float eqClosedFrequency = 300f;

    [Tooltip("Duration of EQ sweep up (seconds).")]
    [Range(0.1f, 5f)] public float eqSweepUpDuration = 1.2f;

    [Tooltip("Easing curve for EQ sweep up.")]
    public AnimationCurve eqOpenCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("═══ Timing")]
    [Tooltip("Delay after scene load before starting the sequence. Lets the scene finish rendering.")]
    [Range(0f, 3f)] public float openDelay = 0.2f;

    [Tooltip("How long the BlackFill fades from 1 to 0 before the eyelids open (seconds).")]
    [Range(0f, 2f)] public float blackFillFadeOutDuration = 0.3f;

    [Header("═══ Door Sound")]
    [Tooltip("AudioSource for the door sound. Auto-created if null.")]
    public AudioSource doorAudioSource;

    [Tooltip("Sound that plays after eyes open (e.g. door closing, ambience starting).")]
    public AudioClip doorSound;

    [Tooltip("Delay after eyes are fully open before the door sound plays (seconds).")]
    [Range(0f, 10f)] public float doorSoundDelay = 1.5f;

    [Range(0f, 1f)] public float doorSoundVolume = 0.8f;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        // Ensure EQ stays at closed frequency at scene start
        SetLowPass(eqClosedFrequency);

        if (doorAudioSource == null)
        {
            doorAudioSource = gameObject.AddComponent<AudioSource>();
            doorAudioSource.playOnAwake = false;
        }

        StartCoroutine(OpenSequence());
    }

    // ─────────────────────────────────────────────────────────────
    // SEQUENCE
    // ─────────────────────────────────────────────────────────────

    private IEnumerator OpenSequence()
    {
        // 1. Find and immediately destroy the persisted EyeBlinkController
        //    The localBlackFill is already opaque so there is no flash
        EyeBlinkController eyeBlink = FindObjectOfType<EyeBlinkController>();
        if (eyeBlink != null)
            Destroy(eyeBlink.gameObject);

        // 2. Ensure local black fill is fully opaque (it should be set that way in editor)
        if (localBlackFill != null)
        {
            var c = localBlackFill.color;
            c.a = 1f;
            localBlackFill.color = c;
        }

        // 3. Let the scene finish rendering behind the local black fill
        yield return new WaitForSeconds(openDelay);

        // 4. Open eyelids on local EyeBlinkController + sweep EQ simultaneously
        EyeBlinkController localBlink = FindObjectOfType<EyeBlinkController>();

        Coroutine lidOpen = localBlink != null
            ? StartCoroutine(localBlink.OpenEye())
            : null;

        Coroutine eqUp = StartCoroutine(SweepEQ(eqClosedFrequency, eqOpenFrequency,
            eqSweepUpDuration, eqOpenCurve));

        // Also fade the local black fill out at the same time as the lids open
        Coroutine fillFade = localBlackFill != null
            ? StartCoroutine(FadeBlackFill(localBlackFill, 1f, 0f, blackFillFadeOutDuration))
            : null;

        if (lidOpen != null) yield return lidOpen;
        if (eqUp != null) yield return eqUp;
        if (fillFade != null) yield return fillFade;

        // 5. Door sound after delay
        if (doorSound != null && doorAudioSource != null)
        {
            yield return new WaitForSeconds(doorSoundDelay);
            doorAudioSource.PlayOneShot(doorSound, doorSoundVolume);
        }

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator FadeBlackFill(Image img, float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = img.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }

    private IEnumerator SweepEQ(float fromHz, float toHz, float duration, AnimationCurve curve)
    {
        if (gameMixer == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetLowPass(Mathf.Lerp(fromHz, toHz, curve.Evaluate(Mathf.Clamp01(elapsed / duration))));
            yield return null;
        }
        SetLowPass(toHz);
    }

    private void SetLowPass(float hz)
    {
        if (gameMixer != null)
            gameMixer.SetFloat(lowPassParameterName, hz);
    }
}