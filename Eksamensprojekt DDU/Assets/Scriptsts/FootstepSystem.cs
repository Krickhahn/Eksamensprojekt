using System.Collections;
using UnityEngine;

/// <summary>
/// Plays randomised footstep audio based on the surface the player is standing on.
/// Attach to the Player GameObject. Assign AudioClips and Ground Layer Masks in the Inspector.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FootstepSystem : MonoBehaviour
{
    // -----------------------------------------------------------------------
    #region Surface Definition

    [System.Serializable]
    public class SurfaceFootsteps
    {
        [Tooltip("Display name for this surface (e.g. Grass, Wood, Metal).")]
        public string surfaceName = "Surface";

        [Tooltip("The tag assigned to the ground GameObject for this surface.")]
        public string groundTag = "Untagged";

        [Tooltip("Up to 8 AudioClips for this surface. One is chosen at random each step.")]
        public AudioClip[] clips = new AudioClip[8];

        [Tooltip("Volume multiplier for this surface (0–2).")]
        [Range(0f, 2f)]
        public float volumeMultiplier = 1f;

        [Tooltip("Pitch range for this surface. Adds subtle variety to each step.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Inspector Parameters

    [Header("─── Surface Profiles ───────────────────────────────")]
    [Tooltip("Define up to 3 ground surfaces and their footstep clips.")]
    public SurfaceFootsteps[] surfaces = new SurfaceFootsteps[3]
    {
        new SurfaceFootsteps { surfaceName = "Grass",  groundTag = "Ground_Grass"  },
        new SurfaceFootsteps { surfaceName = "Wood",   groundTag = "Ground_Wood"   },
        new SurfaceFootsteps { surfaceName = "Metal",  groundTag = "Ground_Metal"  }
    };

    [Header("─── Ground Detection ─────────────────────────────────")]
    [Tooltip("Layer mask used for the ground raycast. Include all walkable layers.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("How far down the ray is cast to detect the ground.")]
    [Range(0.1f, 5f)]
    public float raycastDistance = 1.5f;

    [Tooltip("Origin of the raycast. Defaults to this transform if left empty.")]
    public Transform raycastOrigin;

    [Header("─── Footstep Timing ──────────────────────────────────")]
    [Tooltip("Seconds between footstep sounds while walking.")]
    [Range(0.1f, 2f)]
    public float walkStepInterval = 0.5f;

    [Tooltip("Seconds between footstep sounds while running.")]
    [Range(0.05f, 1f)]
    public float runStepInterval = 0.3f;

    [Tooltip("Minimum player speed (magnitude) before footsteps trigger.")]
    [Range(0f, 5f)]
    public float minMoveSpeed = 0.1f;

    [Tooltip("Speed threshold above which 'run' interval is used.")]
    [Range(0f, 20f)]
    public float runSpeedThreshold = 5f;

    [Header("─── Audio Settings ───────────────────────────────────")]
    [Tooltip("Master volume for all footstep sounds (0–1).")]
    [Range(0f, 1f)]
    public float masterVolume = 0.8f;

    [Tooltip("Prevent the same clip from playing twice in a row.")]
    public bool avoidRepeat = true;

    [Tooltip("Fallback clips used when no matching surface is found.")]
    public AudioClip[] fallbackClips = new AudioClip[0];

    [Tooltip("Volume multiplier for fallback clips.")]
    [Range(0f, 2f)]
    public float fallbackVolumeMultiplier = 0.6f;

    [Header("─── Debug ────────────────────────────────────────────")]
    [Tooltip("Log detected surface name to Console each step.")]
    public bool debugLog = false;

    #endregion

    // -----------------------------------------------------------------------
    #region Private State

    private AudioSource _audioSource;
    private CharacterController _characterController;
    private Rigidbody _rigidbody;

    private float _stepTimer;
    private int _lastClipIndex = -1;
    private bool _isGrounded;
    private float _currentSpeed;

    #endregion

    // -----------------------------------------------------------------------
    #region Unity Messages

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3-D sound; set to 0 for 2-D

        _characterController = GetComponent<CharacterController>();
        _rigidbody            = GetComponent<Rigidbody>();

        if (raycastOrigin == null) raycastOrigin = transform;
    }

    void Update()
    {
        UpdateMovementState();

        if (!_isGrounded || _currentSpeed < minMoveSpeed)
        {
            _stepTimer = 0f;
            return;
        }

        float interval = (_currentSpeed >= runSpeedThreshold) ? runStepInterval : walkStepInterval;
        _stepTimer += Time.deltaTime;

        if (_stepTimer >= interval)
        {
            _stepTimer = 0f;
            PlayFootstep();
        }
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Core Logic

    void UpdateMovementState()
    {
        if (_characterController != null)
        {
            _isGrounded   = _characterController.isGrounded;
            _currentSpeed = new Vector3(_characterController.velocity.x, 0f,
                                        _characterController.velocity.z).magnitude;
        }
        else if (_rigidbody != null)
        {
            _currentSpeed = new Vector3(_rigidbody.linearVelocity.x, 0f,
                                        _rigidbody.linearVelocity.z).magnitude;
            _isGrounded   = IsGroundedByRay();
        }
        else
        {
            // Fallback: rely purely on raycast
            _isGrounded   = IsGroundedByRay();
            _currentSpeed = minMoveSpeed + 1f; // assume moving if no physics component
        }
    }

    bool IsGroundedByRay()
    {
        return Physics.Raycast(raycastOrigin.position, Vector3.down,
                               raycastDistance, groundLayers);
    }

    void PlayFootstep()
    {
        SurfaceFootsteps surface = DetectSurface(out GameObject hitObject);

        AudioClip[] pool;
        float volMult;
        Vector2 pitch;

        if (surface != null && surface.clips != null && surface.clips.Length > 0)
        {
            pool    = surface.clips;
            volMult = surface.volumeMultiplier;
            pitch   = surface.pitchRange;

            if (debugLog)
                Debug.Log($"[Footstep] Surface: {surface.surfaceName}" +
                          (hitObject ? $" | Object: {hitObject.name}" : ""));
        }
        else
        {
            if (fallbackClips == null || fallbackClips.Length == 0) return;
            pool    = fallbackClips;
            volMult = fallbackVolumeMultiplier;
            pitch   = new Vector2(0.95f, 1.05f);

            if (debugLog)
                Debug.Log("[Footstep] No matching surface — using fallback clips.");
        }

        AudioClip clip = PickClip(pool);
        if (clip == null) return;

        _audioSource.pitch  = Random.Range(pitch.x, pitch.y);
        _audioSource.PlayOneShot(clip, masterVolume * volMult);
    }

    SurfaceFootsteps DetectSurface(out GameObject hitObject)
    {
        hitObject = null;

        if (!Physics.Raycast(raycastOrigin.position, Vector3.down,
                             out RaycastHit hit, raycastDistance, groundLayers))
            return null;

        hitObject = hit.collider.gameObject;

        foreach (var surface in surfaces)
        {
            if (string.IsNullOrEmpty(surface.groundTag)) continue;

            if (hitObject.CompareTag(surface.groundTag))
                return surface;
        }

        return null;
    }

    AudioClip PickClip(AudioClip[] pool)
    {
        // Filter out null slots
        var valid = System.Array.FindAll(pool, c => c != null);
        if (valid.Length == 0) return null;
        if (valid.Length == 1) return valid[0];

        if (!avoidRepeat)
            return valid[Random.Range(0, valid.Length)];

        // Avoid repeat: pick a different index than last time
        int index;
        int attempts = 0;
        do
        {
            index = Random.Range(0, valid.Length);
            attempts++;
        } while (index == _lastClipIndex && attempts < 10);

        _lastClipIndex = index;
        return valid[index];
    }

    #endregion

    // -----------------------------------------------------------------------
    #region Public API

    /// <summary>
    /// Trigger a single footstep manually (e.g. from an Animation Event).
    /// </summary>
    public void OnFootstepAnimationEvent() => PlayFootstep();

    /// <summary>
    /// Change master volume at runtime.
    /// </summary>
    public void SetMasterVolume(float volume) =>
        masterVolume = Mathf.Clamp01(volume);

    #endregion

    // -----------------------------------------------------------------------
    #region Editor Gizmos

    void OnDrawGizmosSelected()
    {
        if (raycastOrigin == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(raycastOrigin.position,
                        raycastOrigin.position + Vector3.down * raycastDistance);
        Gizmos.DrawWireSphere(raycastOrigin.position + Vector3.down * raycastDistance, 0.05f);
    }

    #endregion
}
