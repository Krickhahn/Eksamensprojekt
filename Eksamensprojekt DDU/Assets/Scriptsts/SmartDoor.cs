using System.Collections;
using UnityEngine;

/// <summary>
/// SmartDoor — Proximity-based automatic door that swings inward or outward
/// depending on which side the player approaches from.
///
/// Setup:
///   1. Attach this script to your Door GameObject (the rotating part).
///   2. The door pivot should be at the hinge edge (not center).
///   3. At rest the door's local Y rotation = -90°  (closedAngle).
///   4. Assign the Player transform and the four AudioClips in the Inspector.
///   5. Add an AudioSource component to this GameObject (or it is added automatically).
///
/// How it works:
///   • A raycast is fired every frame from the player toward the door.
///   • If the player is within detectionRange AND the raycast hits this door,
///     the door figures out which side the player is on by comparing the dot
///     product of the approach vector with the door's local forward.
///   • From the front  → swings inward  (openAngle = closedAngle + swingAngle)
///   • From the back   → swings outward (openAngle = closedAngle - swingAngle)
///   • The target rotation is lerped smoothly based on proximity (closer = more open).
///   • Opening / closing sounds are triggered per direction whenever the door
///     crosses the soundTriggerThreshold in either direction.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SmartDoor : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Inspector Fields
    // ─────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The player's Transform. Assign in the Inspector.")]
    public Transform player;

    [Header("Door Settings")]
    [Tooltip("Local Y rotation (degrees) when the door is fully closed.")]
    public float closedAngle = -90f;

    [Tooltip("How many degrees the door swings open from its closed position.")]
    public float swingAngle = 90f;

    [Header("Detection — Front (swings inward)")]
    [Tooltip("Distance from the front at which the door begins to react.")]
    public float frontDetectionRange = 3.5f;

    [Tooltip("Distance from the front at which the door is fully open.")]
    public float frontFullOpenRange = 0.8f;

    [Header("Detection — Back (swings outward)")]
    [Tooltip("Distance from the back at which the door begins to react.")]
    public float backDetectionRange = 2.0f;

    [Tooltip("Distance from the back at which the door is fully open.")]
    public float backFullOpenRange = 0.6f;

    [Tooltip("Speed at which the door lerps toward its target angle.")]
    public float lerpSpeed = 6f;

    [Tooltip("Layer mask for the raycast. Include the layer your door is on.")]
    public LayerMask doorLayerMask = ~0;   // default: everything

    [Header("Sound — Front approach (swings inward)")]
    [Tooltip("Played when the door starts opening from the front.")]
    public AudioClip frontOpenSound;

    [Tooltip("Played when the door starts closing from the front.")]
    public AudioClip frontCloseSound;

    [Header("Sound — Back approach (swings outward)")]
    [Tooltip("Played when the door starts opening from the back.")]
    public AudioClip backOpenSound;

    [Tooltip("Played when the door starts closing from the back.")]
    public AudioClip backCloseSound;

    [Tooltip("Volume for all door sounds.")]
    [Range(0f, 1f)]
    public float soundVolume = 1f;

    [Tooltip("How open the door must be (0-1) before the close-sound triggers on exit.")]
    [Range(0.05f, 0.5f)]
    public float soundTriggerThreshold = 0.15f;

    // ─────────────────────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────────────────────

    private AudioSource _audioSource;
    private Collider _doorCollider;
    private Vector3 _doorCenter;   // geometric centre of the door mesh (world-space offset from pivot)

    // Current and target open amount  [0 = closed, 1 = fully open]
    private float _currentOpenAmount = 0f;
    private float _targetOpenAmount = 0f;

    // Which side last triggered the door: +1 = front, -1 = back, 0 = none
    private int _lastSide = 0;

    // Sound state flags to prevent re-triggering
    private bool _openSoundPlayed = false;
    private bool _closeSoundPlayed = true;   // start true so we don't fire on load

    // The actual target Y angle we lerp toward
    private float _targetAngle;

    // ─────────────────────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;   // 3-D sound

        _doorCollider = GetComponent<Collider>();

        // Cache the door's geometric centre relative to its pivot.
        // Using the collider bounds gives us the actual mesh midpoint even
        // when the pivot is at the hinge edge, which keeps the raycast
        // aimed at a stable point and eliminates proximity wiggle.
        if (_doorCollider != null)
            _doorCenter = transform.InverseTransformPoint(_doorCollider.bounds.center);
        else
            _doorCenter = Vector3.zero;

        // Snap to closed state
        _targetAngle = closedAngle;
        SetDoorAngle(closedAngle);
    }

    private void Update()
    {
        if (player == null) return;

        bool playerNearby = TryGetProximity(out float proximity, out int side);

        // ── Determine target open amount ──────────────────────────
        if (playerNearby)
        {
            _targetOpenAmount = Mathf.Clamp01(proximity);

            // Remember which side is driving the door
            if (side != 0) _lastSide = side;
        }
        else
        {
            _targetOpenAmount = 0f;
        }

        // ── Resolve target angle based on active side ─────────────
        int activeSide = playerNearby ? side : _lastSide;
        float openAngle = (activeSide >= 0)
            ? closedAngle + swingAngle    // front → inward
            : closedAngle - swingAngle;   // back  → outward

        _targetAngle = Mathf.LerpUnclamped(closedAngle, openAngle, _targetOpenAmount);

        // ── Lerp current open amount (for sound logic) ────────────
        _currentOpenAmount = Mathf.Lerp(
            _currentOpenAmount,
            _targetOpenAmount,
            Time.deltaTime * lerpSpeed
        );

        // ── Apply rotation ────────────────────────────────────────
        float currentAngle = Mathf.LerpAngle(
            GetDoorAngle(),
            _targetAngle,
            Time.deltaTime * lerpSpeed
        );
        SetDoorAngle(currentAngle);

        // ── Sound triggers ────────────────────────────────────────
        HandleSounds(playerNearby, activeSide);
    }

    // ─────────────────────────────────────────────────────────────
    //  Core Logic
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires a raycast from the player to this door.
    /// Returns true if the door is detected within range.
    /// proximity: 0 (at detectionRange) → 1 (at fullOpenRange or closer)
    /// side:      +1 = front, -1 = back
    /// </summary>
    /// <summary>Returns the door's geometric centre in world space (follows rotation).</summary>
    private Vector3 DoorCenter() => transform.TransformPoint(_doorCenter);

    private bool TryGetProximity(out float proximity, out int side)
    {
        proximity = 0f;
        side = 0;

        // Aim at the geometric centre of the door, not the pivot/hinge.
        // This gives a stable target that doesn't shift as the door rotates,
        // preventing the proximity value from flickering and causing wiggle.
        Vector3 center = DoorCenter();
        Vector3 towardDoor = center - player.position;
        float distance = towardDoor.magnitude;

        // Determine side FIRST so we can pick the correct range per direction.
        // Use door's local forward vs the player->center vector.
        float dot = Vector3.Dot(transform.forward, towardDoor.normalized);
        side = (dot >= 0f) ? 1 : -1;

        float activeDetectionRange = (side >= 0) ? frontDetectionRange : backDetectionRange;
        float activeFullOpenRange = (side >= 0) ? frontFullOpenRange : backFullOpenRange;

        if (distance > activeDetectionRange)
            return false;

        // Raycast from the player straight to the door's geometric centre.
        Ray ray = new Ray(player.position, towardDoor.normalized);

        if (!Physics.Raycast(ray, out RaycastHit hit, activeDetectionRange, doorLayerMask))
            return false;

        // Make sure the ray actually hit THIS door (not another collider)
        if (_doorCollider != null && hit.collider != _doorCollider)
            return false;

        // Proximity: 1 when at fullOpenRange or closer, 0 at detectionRange
        proximity = 1f - Mathf.InverseLerp(activeFullOpenRange, activeDetectionRange, distance);
        proximity = Mathf.Clamp01(proximity);

        return true;
    }

    // ─────────────────────────────────────────────────────────────
    //  Sound Logic
    // ─────────────────────────────────────────────────────────────

    private void HandleSounds(bool playerNearby, int side)
    {
        bool isOpen = _currentOpenAmount > soundTriggerThreshold;

        // Opening — play once when door crosses threshold going up
        if (playerNearby && isOpen && !_openSoundPlayed)
        {
            PlaySound(side >= 0 ? frontOpenSound : backOpenSound);
            _openSoundPlayed = true;
            _closeSoundPlayed = false;
        }

        // Closing — play once when door crosses threshold going down
        if (!playerNearby && !isOpen && !_closeSoundPlayed && _openSoundPlayed)
        {
            PlaySound(_lastSide >= 0 ? frontCloseSound : backCloseSound);
            _closeSoundPlayed = true;
            _openSoundPlayed = false;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(clip, soundVolume);
    }

    // ─────────────────────────────────────────────────────────────
    //  Rotation Helpers
    // ─────────────────────────────────────────────────────────────

    private float GetDoorAngle()
    {
        return transform.localEulerAngles.y;
    }

    private void SetDoorAngle(float yAngle)
    {
        Vector3 e = transform.localEulerAngles;
        e.y = yAngle;
        transform.localEulerAngles = e;
    }

    // ─────────────────────────────────────────────────────────────
    //  Gizmos (Scene View Debug)
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Front detection range (cyan)
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, frontDetectionRange);

        // Back detection range (purple)
        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, backDetectionRange);

        // Front full-open range (orange)
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, frontFullOpenRange);

        // Back full-open range (pink)
        Gizmos.color = new Color(1f, 0.3f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, backFullOpenRange);

        // Geometric centre marker
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.TransformPoint(_doorCenter), 0.07f);

        // Door forward arrow (green = front side)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 1.4f);

        // Door backward arrow (red = back side)
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, -transform.forward * 1.4f);

        // Ray to player
        if (player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(player.position, transform.TransformPoint(_doorCenter));
        }
    }
#endif
}