using System.Collections;
using UnityEngine;

/// <summary>
/// ScareEvent: When the player walks within range, a box falls in front of them
/// accompanied by sound effects. Triggers only once.
///
/// SETUP:
///   1. Attach this script to an empty GameObject (the "trigger zone").
///   2. Assign a Rigidbody-equipped box GameObject to 'fallingBox'.
///   3. Set the box's Rigidbody to isKinematic = true in the Inspector (script handles it).
///   4. Assign 1–4 AudioClips to 'scareSounds' (randomly chosen on trigger).
///   5. Optionally assign 'impactSounds' for when the box hits the floor.
///   6. Tag your Player GameObject as "Player" (or change 'playerTag' below).
/// </summary>
public class ScareEvent : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The box that will fall. Must have a Rigidbody component.")]
    public GameObject fallingBox;

    [Tooltip("AudioSource used to play the scare and impact sounds.")]
    public AudioSource audioSource;

    // ──────────────────────────────────────────────────────────────
    [Header("Trigger Settings")]
    [Tooltip("How close (metres) the player must be to trigger the event.")]
    [Range(0.5f, 20f)]
    public float triggerRadius = 5f;

    [Tooltip("Tag used to identify the player GameObject.")]
    public string playerTag = "Player";

    [Tooltip("Draw the trigger radius as a wire sphere in the Scene view.")]
    public bool showGizmo = true;

    // ──────────────────────────────────────────────────────────────
    [Header("Box Behaviour")]
    [Tooltip("Seconds to wait after player enters range before the box falls.")]
    [Range(0f, 3f)]
    public float fallDelay = 0.1f;

    [Tooltip("Extra downward force applied to the box when it is released (N).")]
    [Range(0f, 500f)]
    public float extraDownForce = 100f;

    [Tooltip("Random torque range (±) applied to the box for a natural tumble.")]
    [Range(0f, 300f)]
    public float tumbleForce = 80f;

    // ──────────────────────────────────────────────────────────────
    [Header("Scare Sounds (one is chosen at random)")]
    [Tooltip("Sound clips that play when the box is released. Add 1–4 clips.")]
    public AudioClip[] scareSounds;

    [Tooltip("Volume for the scare sound.")]
    [Range(0f, 1f)]
    public float scareVolume = 1f;

    [Tooltip("Pitch for the scare sound (1 = normal).")]
    [Range(0.5f, 2f)]
    public float scarePitch = 1f;

    [Tooltip("Random pitch variance (± added to scarePitch).")]
    [Range(0f, 0.3f)]
    public float pitchVariance = 0.05f;

    // ──────────────────────────────────────────────────────────────
    [Header("Impact Sounds (one is chosen at random)")]
    [Tooltip("Sound clips that play when the box hits the floor. Optional.")]
    public AudioClip[] impactSounds;

    [Tooltip("Minimum collision impulse magnitude needed to play impact sound.")]
    [Range(0f, 20f)]
    public float impactThreshold = 2f;

    [Tooltip("Volume for the impact sound.")]
    [Range(0f, 1f)]
    public float impactVolume = 0.85f;

    // ──────────────────────────────────────────────────────────────
    // Private state
    private bool _triggered = false;
    private Transform _playerTransform;
    private Rigidbody _boxRigidbody;
    private BoxImpactListener _impactListener;

    // ──────────────────────────────────────────────────────────────

    void Start()
    {
        // Cache player transform
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
            _playerTransform = player.transform;
        else
            Debug.LogWarning($"[ScareEvent] No GameObject found with tag '{playerTag}'. " +
                             "Check the playerTag field or your Player's tag.");

        // Cache Rigidbody and ensure box starts kinematic (frozen in place)
        if (fallingBox != null)
        {
            _boxRigidbody = fallingBox.GetComponent<Rigidbody>();
            if (_boxRigidbody == null)
            {
                Debug.LogError("[ScareEvent] 'fallingBox' has no Rigidbody component!");
            }
            else
            {
                _boxRigidbody.isKinematic = true;

                // Attach a helper component that reports collisions back for impact sounds
                _impactListener = fallingBox.AddComponent<BoxImpactListener>();
                _impactListener.Init(this);
            }
        }
        else
        {
            Debug.LogError("[ScareEvent] 'fallingBox' is not assigned in the Inspector!");
        }

        // Auto-create AudioSource if none is assigned
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    // ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (_triggered || _playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        if (distance <= triggerRadius)
        {
            _triggered = true;
            StartCoroutine(TriggerScare());
        }
    }

    // ──────────────────────────────────────────────────────────────

    IEnumerator TriggerScare()
    {
        yield return new WaitForSeconds(fallDelay);

        // Play scare sound
        PlayRandomClip(scareSounds, scareVolume, scarePitch, pitchVariance);

        // Release the box
        if (_boxRigidbody != null)
        {
            _boxRigidbody.isKinematic = false;

            // Push it downward
            _boxRigidbody.AddForce(Vector3.down * extraDownForce, ForceMode.Impulse);

            // Add a random spin for realism
            Vector3 randomTorque = new Vector3(
                Random.Range(-tumbleForce, tumbleForce),
                Random.Range(-tumbleForce, tumbleForce),
                Random.Range(-tumbleForce, tumbleForce)
            );
            _boxRigidbody.AddTorque(randomTorque, ForceMode.Impulse);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Called by BoxImpactListener when the box collides with something

    public void OnBoxImpact(float impulseMagnitude)
    {
        if (impulseMagnitude < impactThreshold) return;
        if (impactSounds == null || impactSounds.Length == 0) return;

        PlayRandomClip(impactSounds, impactVolume, 1f, 0.1f);
    }

    // ──────────────────────────────────────────────────────────────

    void PlayRandomClip(AudioClip[] clips, float volume, float pitch, float variance)
    {
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        audioSource.pitch = pitch + Random.Range(-variance, variance);
        audioSource.PlayOneShot(clip, volume);
    }

    // ──────────────────────────────────────────────────────────────
    // Scene-view helper

    void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}


// ════════════════════════════════════════════════════════════════
/// <summary>
/// Helper component added at runtime to the falling box.
/// Forwards OnCollisionEnter events back to ScareEvent so impact sounds
/// can be played without coupling AudioSource logic to the box prefab.
/// </summary>
[DisallowMultipleComponent]
public class BoxImpactListener : MonoBehaviour
{
    private ScareEvent _parent;
    private bool _impactPlayed = false;

    public void Init(ScareEvent parent) => _parent = parent;

    void OnCollisionEnter(Collision collision)
    {
        // Only play the impact sound for the first significant collision
        if (_impactPlayed) return;

        float impulse = collision.impulse.magnitude;
        if (impulse >= _parent.impactThreshold)
        {
            _impactPlayed = true;
            _parent.OnBoxImpact(impulse);
        }
    }
}
