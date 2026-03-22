using System.Collections;
using UnityEngine;

/// <summary>
/// GarageDoorController
/// ─────────────────────
/// Attach this script to any empty GameObject (e.g. "GarageSystem").
///
/// HOW TO SET UP:
///  1. Assign your Door GameObject        → doorObject
///  2. Assign your Button GameObject      → buttonObject
///  3. Assign the Player GameObject       → player
///  4. Assign AudioClips in the Sound section of the Inspector.
///  5. Tweak every parameter in the Inspector.
///  6. (Optional) Add a UI Canvas TMP_Text element → promptText
///
/// AUDIO NOTES:
///  - Each sound plays from the position of its source GameObject (3D spatial audio).
///  - AudioSource components are created automatically at runtime on the Door and
///    Button GameObjects — you do NOT need to add them manually.
///  - All volume, pitch, spatial blend and rolloff settings are exposed in the Inspector.
///
/// REQUIRES: TextMeshPro package for the on-screen prompt (or swap to legacy Text).
/// </summary>
public class GarageDoorController : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("The garage door GameObject to move.")]
    public GameObject doorObject;

    [Tooltip("The button/panel GameObject the player interacts with.")]
    public GameObject buttonObject;

    [Tooltip("The player GameObject (used for distance check).")]
    public GameObject player;

    [Tooltip("(Optional) UI Text element for the interaction prompt. Leave empty to disable.")]
    public TMPro.TMP_Text promptText;

    // ─── Interaction ─────────────────────────────────────────────────────────
    [Header("Interaction")]
    [Tooltip("Key the player presses to activate the button.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("How close the player must be (world units) to press the button.")]
    public float interactionRange = 3f;

    [Tooltip("Text shown on screen when the player is in range.")]
    public string promptMessage = "Press [E] to open/close door";

    // ─── Door Settings ────────────────────────────────────────────────────────
    [Header("Door Movement")]
    [Tooltip("How far the door travels upward (local Y units).")]
    public float doorOpenDistance = 4f;

    [Tooltip("Time (seconds) for the door to fully open or close.")]
    public float doorMoveDuration = 1.5f;

    [Tooltip("Animation curve controlling door easing (leave default for smooth ease in/out).")]
    public AnimationCurve doorEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ─── Button Press Settings ────────────────────────────────────────────────
    [Header("Button Press Animation")]
    [Tooltip("How far the button moves when pressed (local units along its press axis).")]
    public float buttonPressDistance = 0.05f;

    [Tooltip("Axis the button moves along when pressed: 0=X, 1=Y, 2=Z.")]
    [Range(0, 2)]
    public int buttonPressAxis = 2;   // Z = into the wall by default

    [Tooltip("How quickly the button moves in/out (seconds).")]
    public float buttonPressDuration = 0.1f;

    [Tooltip("How long the button stays pressed before bouncing back (seconds).")]
    public float buttonHoldDuration = 0.2f;

    // ─── Button Sounds ────────────────────────────────────────────────────────
    [Header("Button Sounds  (played at the Button's world position)")]
    [Tooltip("Sound played when the button is pressed to open the door.")]
    public AudioClip buttonPressOpenClip;

    [Tooltip("Sound played when the button is pressed to close the door.")]
    public AudioClip buttonPressCloseClip;

    [Tooltip("Volume of the button press sound (0-1).")]
    [Range(0f, 1f)]
    public float buttonVolume = 1f;

    [Tooltip("Pitch of the button press sound. 1 = normal speed.")]
    [Range(0.1f, 3f)]
    public float buttonPitch = 1f;

    // ─── Door Sounds ──────────────────────────────────────────────────────────
    [Header("Door Sounds  (played at the Door's world position)")]
    [Tooltip("Sound played while the door is opening (loops until fully open).")]
    public AudioClip doorOpeningClip;

    [Tooltip("Sound played while the door is closing (loops until fully closed).")]
    public AudioClip doorClosingClip;

    [Tooltip("Short sound played when the door finishes opening.")]
    public AudioClip doorOpenFinishClip;

    [Tooltip("Short sound played when the door finishes closing.")]
    public AudioClip doorCloseFinishClip;

    [Tooltip("Volume of the looping door movement sound (0-1).")]
    [Range(0f, 1f)]
    public float doorMovingVolume = 0.8f;

    [Tooltip("Volume of the door finish (thud/clunk) sound (0-1).")]
    [Range(0f, 1f)]
    public float doorFinishVolume = 1f;

    [Tooltip("Pitch of the door sounds. 1 = normal speed.")]
    [Range(0.1f, 3f)]
    public float doorPitch = 1f;

    // ─── Spatial Audio Settings ───────────────────────────────────────────────
    [Header("Spatial Audio Settings")]
    [Tooltip("0 = fully 2D (no positional falloff), 1 = fully 3D (sound fades with distance).")]
    [Range(0f, 1f)]
    public float spatialBlend = 1f;

    [Tooltip("Distance at which the sound starts to fade.")]
    public float minDistance = 1f;

    [Tooltip("Distance at which the sound is nearly inaudible.")]
    public float maxDistance = 20f;

    [Tooltip("How sound rolls off with distance.")]
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    // ─── Private State ────────────────────────────────────────────────────────
    private Vector3 _doorClosedPos;
    private Vector3 _doorOpenPos;
    private Vector3 _buttonRestPos;
    private Vector3 _buttonPressedPos;

    private bool _isOpen          = false;
    private bool _doorMoving      = false;
    private bool _buttonAnimating = false;
    private bool _playerInRange   = false;

    // Dedicated AudioSources — created automatically at runtime
    private AudioSource _doorAudioMoving;   // looping movement sound on door
    private AudioSource _doorAudioOneShot;  // finish thud on door (one-shot)
    private AudioSource _buttonAudio;       // button click on button object

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (doorObject == null || buttonObject == null || player == null)
        {
            Debug.LogError("[GarageDoorController] Door, Button or Player reference is missing!");
            enabled = false;
            return;
        }

        // Cache starting positions
        _doorClosedPos = doorObject.transform.localPosition;
        _doorOpenPos   = _doorClosedPos + Vector3.up * doorOpenDistance;

        _buttonRestPos = buttonObject.transform.localPosition;

        Vector3 pressOffset = Vector3.zero;
        pressOffset[buttonPressAxis] = -buttonPressDistance;
        _buttonPressedPos = _buttonRestPos + pressOffset;

        // Hide prompt at start
        if (promptText != null)
            promptText.enabled = false;

        // Create AudioSources on the correct GameObjects
        _buttonAudio      = CreateAudioSource(buttonObject);
        _doorAudioMoving  = CreateAudioSource(doorObject);
        _doorAudioOneShot = CreateAudioSource(doorObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        CheckPlayerRange();
        HandleInput();
    }

    // ─── Range Check ─────────────────────────────────────────────────────────
    void CheckPlayerRange()
    {
        if (buttonObject == null || player == null) return;

        float dist = Vector3.Distance(player.transform.position,
                                      buttonObject.transform.position);
        _playerInRange = dist <= interactionRange;

        if (promptText != null)
        {
            promptText.enabled = _playerInRange && !_doorMoving;
            if (_playerInRange)
                promptText.text = promptMessage;
        }
    }

    // ─── Input ────────────────────────────────────────────────────────────────
    void HandleInput()
    {
        if (_playerInRange && !_doorMoving && !_buttonAnimating
            && Input.GetKeyDown(interactKey))
        {
            StartCoroutine(PressButton());
            StartCoroutine(MoveDoor());
        }
    }

    // ─── Button Animation + Sound ─────────────────────────────────────────────
    IEnumerator PressButton()
    {
        _buttonAnimating = true;

        // Open clip when door is currently closed, close clip when currently open
        AudioClip clip = _isOpen ? buttonPressCloseClip : buttonPressOpenClip;
        PlayOneShot(_buttonAudio, clip, buttonVolume, buttonPitch);

        // Press in
        yield return LerpLocalPosition(buttonObject.transform,
                                       _buttonRestPos, _buttonPressedPos,
                                       buttonPressDuration);
        // Hold
        yield return new WaitForSeconds(buttonHoldDuration);

        // Spring back
        yield return LerpLocalPosition(buttonObject.transform,
                                       _buttonPressedPos, _buttonRestPos,
                                       buttonPressDuration);

        _buttonAnimating = false;
    }

    // ─── Door Animation + Sound ───────────────────────────────────────────────
    IEnumerator MoveDoor()
    {
        _doorMoving = true;

        Vector3 from = _isOpen ? _doorOpenPos   : _doorClosedPos;
        Vector3 to   = _isOpen ? _doorClosedPos : _doorOpenPos;

        // Start looping movement sound (opening vs closing)
        AudioClip movingClip = _isOpen ? doorClosingClip : doorOpeningClip;
        StartLoopingSound(_doorAudioMoving, movingClip, doorMovingVolume, doorPitch);

        yield return LerpLocalPosition(doorObject.transform, from, to,
                                       doorMoveDuration, doorEaseCurve);

        // Stop looping movement sound
        _doorAudioMoving.Stop();

        // Play finish thud / clunk
        AudioClip finishClip = _isOpen ? doorCloseFinishClip : doorOpenFinishClip;
        PlayOneShot(_doorAudioOneShot, finishClip, doorFinishVolume, doorPitch);

        _isOpen     = !_isOpen;
        _doorMoving = false;
    }

    // ─── Audio Helpers ────────────────────────────────────────────────────────

    /// <summary>Creates and configures a fresh AudioSource on the target GameObject.</summary>
    AudioSource CreateAudioSource(GameObject target)
    {
        AudioSource src  = target.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.loop         = false;
        src.spatialBlend = spatialBlend;
        src.minDistance  = minDistance;
        src.maxDistance  = maxDistance;
        src.rolloffMode  = rolloffMode;
        return src;
    }

    /// <summary>Plays a one-shot clip if the clip is assigned.</summary>
    void PlayOneShot(AudioSource src, AudioClip clip, float volume, float pitch)
    {
        if (src == null || clip == null) return;
        ApplySpatialSettings(src);
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
    }

    /// <summary>Starts a looping clip on the given AudioSource.</summary>
    void StartLoopingSound(AudioSource src, AudioClip clip, float volume, float pitch)
    {
        if (src == null || clip == null) return;
        ApplySpatialSettings(src);
        src.clip   = clip;
        src.volume = volume;
        src.pitch  = pitch;
        src.loop   = true;
        src.Play();
    }

    /// <summary>Syncs spatial settings to the AudioSource (safe to call each play).</summary>
    void ApplySpatialSettings(AudioSource src)
    {
        src.spatialBlend = spatialBlend;
        src.minDistance  = minDistance;
        src.maxDistance  = maxDistance;
        src.rolloffMode  = rolloffMode;
    }

    // ─── Generic Lerp Helper ──────────────────────────────────────────────────
    IEnumerator LerpLocalPosition(Transform t, Vector3 from, Vector3 to,
                                  float duration, AnimationCurve curve = null)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float raw  = Mathf.Clamp01(elapsed / duration);
            float eval = (curve != null) ? curve.Evaluate(raw) : raw;
            t.localPosition = Vector3.LerpUnclamped(from, to, eval);
            yield return null;
        }
        t.localPosition = to;
    }

    // ─── Gizmos ───────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (buttonObject != null)
        {
            // Interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(buttonObject.transform.position, interactionRange);

            // Button audio range
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(buttonObject.transform.position, minDistance);
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.1f);
            Gizmos.DrawWireSphere(buttonObject.transform.position, maxDistance);
        }

        if (doorObject != null)
        {
            // Door open preview
            Gizmos.color = Color.cyan;
            Vector3 openPreview = doorObject.transform.position + Vector3.up * doorOpenDistance;
            Gizmos.DrawLine(doorObject.transform.position, openPreview);
            Gizmos.DrawWireCube(openPreview, doorObject.transform.localScale);

            // Door audio range
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(doorObject.transform.position, minDistance);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.1f);
            Gizmos.DrawWireSphere(doorObject.transform.position, maxDistance);
        }
    }
}
