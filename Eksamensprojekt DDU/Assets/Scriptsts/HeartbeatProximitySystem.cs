using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a heartbeat sound when enemies are near the player.
/// - Detects all enemies on the Enemy layer via OverlapSphere.
/// - The WeepingAngel enemy (always physically present) only counts
///   when it is in an active state (Hunting, Wandering, or Frozen while
///   lights are off) — i.e. NOT in AngelState.Idle.
/// - Heartbeat tempo is fixed (set in the Editor).
/// - Volume scales with proximity: louder = closer.
/// - A configurable "awareness delay" must pass before the heartbeat
///   starts after an enemy first enters range.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class HeartbeatProximitySystem : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Radius within which enemies trigger the heartbeat.")]
    public float detectionRadius = 15f;

    [Tooltip("Unity Layer that all enemies sit on.")]
    public LayerMask enemyLayer;

    [Tooltip("Seconds an enemy must be in range before the heartbeat starts.")]
    public float awarenessDelay = 2f;

    [Header("Heartbeat Timing")]
    [Tooltip("Seconds between each heartbeat pulse.")]
    public float beatInterval = 0.8f;

    [Header("Volume")]
    [Tooltip("Volume when an enemy is at the edge of detectionRadius.")]
    [Range(0f, 1f)]
    public float minVolume = 0.05f;

    [Tooltip("Volume when an enemy is right next to the player (closestEnemyDistance → 0).")]
    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Tooltip("Distance at which volume reaches maxVolume. " +
             "Should be less than detectionRadius.")]
    public float fullVolumeDistance = 2f;

    [Header("Audio")]
    [Tooltip("The heartbeat one-shot clip.")]
    public AudioClip heartbeatClip;

    [Tooltip("Optional: a second clip played slightly after the first " +
             "to simulate lub-DUB. Leave empty for a single beat.")]
    public AudioClip heartbeatClipSecondary;

    [Tooltip("Delay (seconds) between primary and secondary beat. " +
             "Only used when heartbeatClipSecondary is assigned.")]
    [Range(0f, 0.5f)]
    public float secondaryBeatDelay = 0.15f;

    [Header("Weeping Angel Reference")]
    [Tooltip("Assign the WeepingAngelEnemy in the scene. " +
             "Its heartbeat contribution is ignored while it is Idle " +
             "(lights on / not yet activated).")]
    public WeepingAngelEnemy weepingAngel;

    // ── Runtime ───────────────────────────────────────────────────

    private AudioSource _audioSource;

    // Time the player has continuously had at least one valid enemy in range.
    private float _enemyInRangeTimer = 0f;

    // Are we currently in heartbeat-active mode?
    private bool _heartbeatActive = false;

    // Timer that counts down to the next beat.
    private float _beatTimer = 0f;

    // Closest valid enemy distance this frame (used for volume calculation).
    private float _closestDistance = float.MaxValue;

    // Cached collider buffer to avoid per-frame allocations.
    private readonly Collider[] _hitBuffer = new Collider[32];

    // ── Init ──────────────────────────────────────────────────────

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f; // 2-D / UI sound
    }

    // ── Main Loop ─────────────────────────────────────────────────

    void Update()
    {
        bool validEnemyNear = CheckForNearbyEnemies(out _closestDistance);

        // ── Awareness timer ───────────────────────────────────────
        if (validEnemyNear)
        {
            _enemyInRangeTimer += Time.deltaTime;
        }
        else
        {
            // Enemy left range — reset immediately so the delay
            // must be satisfied again next time.
            _enemyInRangeTimer = 0f;
            _heartbeatActive = false;
            _beatTimer = 0f;
            return;
        }

        // Only activate after the awareness delay has been met.
        if (_enemyInRangeTimer >= awarenessDelay)
            _heartbeatActive = true;

        if (!_heartbeatActive) return;

        // ── Beat timer ────────────────────────────────────────────
        _beatTimer -= Time.deltaTime;
        if (_beatTimer <= 0f)
        {
            _beatTimer = beatInterval;
            PlayBeat();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns true if at least one "valid" enemy is inside detectionRadius.
    /// The WeepingAngel is only valid when it is NOT in the Idle state.
    /// Sets <paramref name="closestDist"/> to the distance to the nearest valid enemy.
    /// </summary>
    bool CheckForNearbyEnemies(out float closestDist)
    {
        closestDist = float.MaxValue;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRadius,
            _hitBuffer,
            enemyLayer);

        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null) continue;

            // ── WeepingAngel special case ─────────────────────────
            if (weepingAngel != null)
            {
                WeepingAngelEnemy angel =
                    col.GetComponent<WeepingAngelEnemy>() ??
                    col.GetComponentInParent<WeepingAngelEnemy>();

                if (angel != null && angel == weepingAngel)
                {
                    // Ignore the angel while it is idle (lights are on / not activated).
                    if (angel.CurrentState == WeepingAngelEnemy.AngelState.Idle)
                        continue;
                }
            }

            // ── Valid enemy ───────────────────────────────────────
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDist)
                closestDist = dist;

            found = true;
        }

        return found;
    }

    /// <summary>
    /// Calculates the current heartbeat volume based on closest enemy distance.
    /// </summary>
    float CalculateVolume()
    {
        if (_closestDistance <= fullVolumeDistance)
            return maxVolume;

        float t = Mathf.InverseLerp(detectionRadius, fullVolumeDistance, _closestDistance);
        return Mathf.Lerp(minVolume, maxVolume, t);
    }

    /// <summary>
    /// Fires a heartbeat pulse (optionally a lub-DUB pair).
    /// </summary>
    void PlayBeat()
    {
        if (heartbeatClip == null) return;

        float vol = CalculateVolume();
        _audioSource.PlayOneShot(heartbeatClip, vol);

        if (heartbeatClipSecondary != null)
            StartCoroutine(PlaySecondaryBeat(vol));
    }

    IEnumerator PlaySecondaryBeat(float volume)
    {
        yield return new WaitForSeconds(secondaryBeatDelay);
        if (_heartbeatActive) // Don't play if we stopped being active
            _audioSource.PlayOneShot(heartbeatClipSecondary, volume);
    }

    // ── Editor Gizmos ─────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Outer detection ring
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Full-volume ring
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, fullVolumeDistance);
    }
#endif
}
