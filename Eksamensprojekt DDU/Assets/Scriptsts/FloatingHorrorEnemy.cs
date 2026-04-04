using System.Collections;
using UnityEngine;

/// <summary>
/// FloatingHorrorEnemy — Horror floating enemy.
/// Fixes: legs bend correctly (negative X = backward), float stays just above
/// the floor, enemy actually wanders while searching.
/// </summary>
public class FloatingHorrorEnemy : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  LIMB TRANSFORMS
    // ─────────────────────────────────────────────
    [Header("── Limb Transforms ──")]
    public Transform hips;
    public Transform leftLeg;
    public Transform rightLeg;
    public Transform leftForearm;
    public Transform rightForearm;

    // ─────────────────────────────────────────────
    //  IDLE / FLOAT ANIMATION
    // ─────────────────────────────────────────────
    [Header("── Idle / Float Animation ──")]
    [Tooltip("Vertical bob amplitude (metres)")] public float floatAmplitude = 0.12f;
    [Tooltip("Vertical bob speed (Hz)")] public float floatSpeed = 0.8f;
    [Tooltip("Hip side-rock amplitude (degrees)")] public float hipRockAmplitude = 5f;
    [Tooltip("Hip rock speed (Hz)")] public float hipRockSpeed = 0.6f;

    [Header("── Leg Bend ──")]
    [Tooltip("Angle to fold legs BACKWARD. Negative X bends the knee back in most rigs. " +
             "If legs still go the wrong way, flip the sign here.")]
    public float idleLegAngle = -50f;
    [Tooltip("Axis the leg bends on (tweak if your rig uses Z instead of X)")]
    public Vector3 legBendAxis = new Vector3(1f, 0f, 0f);

    [Tooltip("Idle forearm droop angle (degrees)")] public float idleForearmAngle = 25f;
    [Tooltip("Animation lerp speed")] public float animLerpSpeed = 5f;

    // ─────────────────────────────────────────────
    //  CHASE ANIMATION
    // ─────────────────────────────────────────────
    [Header("── Chase Animation ──")]
    public float chaseBobAmplitude = 0.22f;
    public float chaseBobSpeed = 2.0f;
    public float chaseLegFlailAmplitude = 15f;
    public float chaseLegFlailSpeed = 3.0f;
    [Tooltip("Forearm angle while chasing (reach forward = more negative)")]
    public float chaseForearmAngle = -20f;

    // ─────────────────────────────────────────────
    //  FLOAT HEIGHT
    // ─────────────────────────────────────────────
    [Header("── Float Height ──")]
    [Tooltip("How high above the floor the enemy hovers (metres). Keep small, e.g. 0.3–1.0.")]
    public float floatHeight = 0.5f;
    [Tooltip("How fast the enemy smoothly corrects its height")]
    public float heightLerpSpeed = 6f;
    [Tooltip("Layer mask for the floor (leave empty to hit everything)")]
    public LayerMask floorMask;

    // ─────────────────────────────────────────────
    //  WANDER (SEARCH MOVEMENT)
    // ─────────────────────────────────────────────
    [Header("── Wander (Search Movement) ──")]
    [Tooltip("Speed while wandering (m/s)")] public float searchMoveSpeed = 1.8f;
    [Tooltip("How far a wander waypoint can be placed")] public float wanderRadius = 8f;
    [Tooltip("How long to wait at each waypoint")] public float wanderPauseTime = 1.5f;
    [Tooltip("Turn speed while wandering (deg/s)")] public float wanderRotateSpeed = 80f;

    // ─────────────────────────────────────────────
    //  CHASE MOVEMENT
    // ─────────────────────────────────────────────
    [Header("── Chase Movement ──")]
    public float chaseMoveSpeed = 5.5f;
    public float chaseRotateSpeed = 200f;

    [Header("── Model Orientation ──")]
    [Tooltip("Set to 180 if your mesh faces backward (default). 0 if it already faces forward.")]
    public float modelForwardOffset = 180f;

    // ─────────────────────────────────────────────
    //  EYE LIGHT
    // ─────────────────────────────────────────────
    [Header("── Eye Light ──")]
    public Light eyeLight;
    public Color searchLightColor = Color.white;
    public Color chaseLightColor = Color.red;
    [Tooltip("Scan arc half-angle (degrees)")] public float scanArcAngle = 40f;
    [Tooltip("Scan sweep speed (deg/s)")] public float scanSpeed = 35f;
    [Tooltip("Transform the light rotates around (head bone / enemy root)")]
    public Transform eyePivot;

    // ─────────────────────────────────────────────
    //  DETECTION
    // ─────────────────────────────────────────────
    [Header("── Detection ──")]
    public string playerTag = "Player";
    public float detectionAngle = 35f;
    public float detectionRange = 16f;
    public float loseRange = 20f;
    public LayerMask sightBlockMask;

    // ─────────────────────────────────────────────
    //  OBSTACLE AVOIDANCE
    // ─────────────────────────────────────────────
    [Header("── Obstacle Avoidance (Warehouse Racks) ──")]
    public BoxCollider[] rackColliders;
    public float avoidanceStrength = 10f;
    public float avoidanceRadius = 2.2f;

    // ─────────────────────────────────────────────
    //  EXISTENCE TIMER
    // ─────────────────────────────────────────────
    [Header("── Existence Timer ──")]
    public float totalExistenceTime = 45f;
    public float chaseTimerMultiplier = 2.5f;

    // ─────────────────────────────────────────────
    //  SOUNDS
    // ─────────────────────────────────────────────
    [Header("── Sounds: Search ──")]
    public AudioSource audioSource;
    public AudioClip[] searchSounds;
    public float searchSoundMinDelay = 4f;
    public float searchSoundMaxDelay = 12f;

    [Header("── Sounds: Chase ──")]
    public AudioClip spottedSound1;
    public AudioClip spottedSound2;
    public AudioClip chaseLoopSound;
    public float chaseLoopInterval = 3.5f;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    enum State { Searching, Chasing, Despawning }
    State _state = State.Searching;

    Transform _player;
    float _timeRemaining;
    float _animTime;

    // Height
    float _targetY;

    // Eye scan
    float _scanAngle;
    int _scanDir = 1;

    // Wander
    Vector3 _wanderTarget;
    bool _wanderPausing;
    float _wanderPauseTimer;

    // Sound
    float _nextSearchSoundTime;
    float _nextChaseLoopTime;
    bool _spottedSoundPlayed;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────
    void Start()
    {
        _player = GameObject.FindGameObjectWithTag(playerTag)?.transform;
        _timeRemaining = totalExistenceTime;

        if (eyeLight != null) { eyeLight.color = searchLightColor; eyeLight.type = LightType.Spot; }
        if (eyePivot == null) eyePivot = transform;

        // Snap to correct float height immediately
        _targetY = GetFloorY() + floatHeight;
        Vector3 p = transform.position;
        p.y = _targetY;
        transform.position = p;

        PickNewWanderTarget();
        ScheduleNextSearchSound();
    }

    void Update()
    {
        if (_state == State.Despawning) return;

        float dt = Time.deltaTime;
        _animTime += dt;

        // Timer drain
        float mult = (_state == State.Chasing) ? chaseTimerMultiplier : 1f;
        _timeRemaining -= dt * mult;
        if (_timeRemaining <= 0f) { StartCoroutine(DespawnSequence()); return; }

        // Continuously track floor height
        _targetY = GetFloorY() + floatHeight;

        switch (_state)
        {
            case State.Searching: UpdateSearching(dt); break;
            case State.Chasing: UpdateChasing(dt); break;
        }

        AnimateLimbs(dt);
    }

    // ─────────────────────────────────────────────
    //  FLOOR DETECTION — cast downward from just above self
    // ─────────────────────────────────────────────
    float GetFloorY()
    {
        // Start the ray 1 m above current position to avoid starting inside geometry
        Vector3 origin = new Vector3(transform.position.x, transform.position.y + 1f, transform.position.z);
        int mask = (floorMask == 0) ? ~0 : (int)floorMask;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, mask))
            return hit.point.y;
        // Fallback: keep current height
        return transform.position.y - floatHeight;
    }

    // ─────────────────────────────────────────────
    //  STATE: SEARCHING
    // ─────────────────────────────────────────────
    void UpdateSearching(float dt)
    {
        // Eye sweep left/right
        if (eyeLight != null)
        {
            eyeLight.color = searchLightColor;
            _scanAngle += _scanDir * scanSpeed * dt;
            if (Mathf.Abs(_scanAngle) >= scanArcAngle) _scanDir *= -1;
            eyePivot.localRotation = Quaternion.Euler(0f, _scanAngle, 0f);
        }

        // Wander movement
        if (_wanderPausing)
        {
            _wanderPauseTimer -= dt;
            if (_wanderPauseTimer <= 0f) { _wanderPausing = false; PickNewWanderTarget(); }
        }
        else
        {
            MoveToward(_wanderTarget, searchMoveSpeed, wanderRotateSpeed, dt);
            if (HorizontalDist(transform.position, _wanderTarget) < 0.4f)
            {
                _wanderPausing = true;
                _wanderPauseTimer = wanderPauseTime;
            }
        }

        ApplyAvoidance(dt);

        // Random ambient sounds
        if (Time.time >= _nextSearchSoundTime && searchSounds != null && searchSounds.Length > 0)
        {
            PlayRandom(searchSounds);
            ScheduleNextSearchSound();
        }

        if (CanSeePlayer()) TransitionToChase();
    }

    // ─────────────────────────────────────────────
    //  STATE: CHASING
    // ─────────────────────────────────────────────
    void UpdateChasing(float dt)
    {
        if (_player == null) { TransitionToSearch(); return; }

        // Eye locked on player
        if (eyeLight != null)
        {
            eyeLight.color = chaseLightColor;
            Vector3 toP = (_player.position - eyePivot.position).normalized;
            if (toP != Vector3.zero) eyePivot.rotation = Quaternion.LookRotation(toP);
        }

        MoveToward(_player.position, chaseMoveSpeed, chaseRotateSpeed, dt);
        ApplyAvoidance(dt);

        // Chase loop sound
        if (Time.time >= _nextChaseLoopTime && chaseLoopSound != null)
        {
            audioSource?.PlayOneShot(chaseLoopSound);
            _nextChaseLoopTime = Time.time + chaseLoopInterval;
        }

        // Lost player?
        if (!CanSeePlayer() && HorizontalDist(transform.position, _player.position) > loseRange)
            TransitionToSearch();
    }

    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    void MoveToward(Vector3 worldTarget, float speed, float rotSpeed, float dt)
    {
        // Flatten target so we only move on XZ
        Vector3 flat = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
        Vector3 dir = flat - transform.position;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized) * Quaternion.Euler(0f, modelForwardOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * dt);
        transform.position += dir.normalized * speed * dt;

        // Smoothly maintain float height after horizontal move
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, _targetY, heightLerpSpeed * dt);
        transform.position = pos;
    }

    // ─────────────────────────────────────────────
    //  WANDER TARGET
    // ─────────────────────────────────────────────
    void PickNewWanderTarget()
    {
        Vector2 rnd = Random.insideUnitCircle * wanderRadius;
        _wanderTarget = transform.position + new Vector3(rnd.x, 0f, rnd.y);
    }

    // ─────────────────────────────────────────────
    //  LIMB ANIMATION
    // ─────────────────────────────────────────────
    void AnimateLimbs(float dt)
    {
        bool chasing = (_state == State.Chasing);

        // ── Vertical bob ──
        float amp = chasing ? chaseBobAmplitude : floatAmplitude;
        float speed = chasing ? chaseBobSpeed : floatSpeed;
        float bob = Mathf.Sin(_animTime * speed * Mathf.PI * 2f) * amp;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, _targetY + bob, heightLerpSpeed * dt);
        transform.position = pos;

        // ── Hip rock ──
        if (hips != null)
        {
            float rock = Mathf.Sin(_animTime * hipRockSpeed * Mathf.PI * 2f) * hipRockAmplitude;
            hips.localRotation = Quaternion.Lerp(hips.localRotation,
                Quaternion.Euler(0f, 0f, rock), dt * animLerpSpeed);
        }

        // ── Legs bent BACKWARD ──
        // idleLegAngle defaults to -50 degrees on the X axis → knees fold back.
        // If your rig bends the opposite way, set idleLegAngle = +50 in the Inspector.
        if (!chasing)
        {
            SetLimbRot(leftLeg, legBendAxis * idleLegAngle);
            SetLimbRot(rightLeg, legBendAxis * idleLegAngle);
        }
        else
        {
            float flail = Mathf.Sin(_animTime * chaseLegFlailSpeed * Mathf.PI * 2f) * chaseLegFlailAmplitude;
            SetLimbRot(leftLeg, legBendAxis * (idleLegAngle + flail));
            SetLimbRot(rightLeg, legBendAxis * (idleLegAngle - flail));
        }

        // ── Forearms ──
        float fAngle = chasing ? chaseForearmAngle : idleForearmAngle;
        SetLimbRot(leftForearm, new Vector3(fAngle, 0f, 12f));
        SetLimbRot(rightForearm, new Vector3(fAngle, 0f, -12f));
    }

    void SetLimbRot(Transform limb, Vector3 euler)
    {
        if (limb == null) return;
        limb.localRotation = Quaternion.Lerp(limb.localRotation,
            Quaternion.Euler(euler), Time.deltaTime * animLerpSpeed);
    }

    // ─────────────────────────────────────────────
    //  OBSTACLE AVOIDANCE
    // ─────────────────────────────────────────────
    void ApplyAvoidance(float dt)
    {
        if (rackColliders == null || rackColliders.Length == 0) return;
        Vector3 push = Vector3.zero;
        foreach (var rack in rackColliders)
        {
            if (rack == null) continue;
            Vector3 closest = rack.ClosestPoint(transform.position);
            Vector3 away = transform.position - closest;
            float dist = away.magnitude;
            if (dist < avoidanceRadius && dist > 0.001f)
                push += away.normalized * ((1f - dist / avoidanceRadius) * avoidanceStrength);
        }
        if (push.sqrMagnitude > 0.001f)
        {
            push.y = 0f;
            transform.position += push * dt;
        }
    }

    // ─────────────────────────────────────────────
    //  DETECTION
    // ─────────────────────────────────────────────
    bool CanSeePlayer()
    {
        if (_player == null) return false;
        Vector3 toPlayer = _player.position - transform.position;
        if (toPlayer.magnitude > detectionRange) return false;
        if (Vector3.Angle(transform.forward, toPlayer) > detectionAngle) return false;
        if (sightBlockMask != 0 &&
            Physics.Raycast(transform.position, toPlayer.normalized, toPlayer.magnitude, sightBlockMask))
            return false;
        return true;
    }

    // ─────────────────────────────────────────────
    //  TRANSITIONS
    // ─────────────────────────────────────────────
    void TransitionToChase()
    {
        _state = State.Chasing;
        if (!_spottedSoundPlayed)
        {
            if (spottedSound1 != null) audioSource?.PlayOneShot(spottedSound1);
            if (spottedSound2 != null) audioSource?.PlayOneShot(spottedSound2);
            _spottedSoundPlayed = true;
        }
        _nextChaseLoopTime = Time.time + 0.5f;
    }

    void TransitionToSearch()
    {
        _state = State.Searching;
        _spottedSoundPlayed = false;
        PickNewWanderTarget();
        ScheduleNextSearchSound();
    }

    // ─────────────────────────────────────────────
    //  SOUND HELPERS
    // ─────────────────────────────────────────────
    void PlayRandom(AudioClip[] clips)
    {
        if (audioSource == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    void ScheduleNextSearchSound() =>
        _nextSearchSoundTime = Time.time + Random.Range(searchSoundMinDelay, searchSoundMaxDelay);

    // ─────────────────────────────────────────────
    //  UTIL
    // ─────────────────────────────────────────────
    float HorizontalDist(Vector3 a, Vector3 b)
    {
        a.y = 0; b.y = 0;
        return Vector3.Distance(a, b);
    }

    // ─────────────────────────────────────────────
    //  DESPAWN
    // ─────────────────────────────────────────────
    IEnumerator DespawnSequence()
    {
        _state = State.Despawning;
        float t = 0f;
        float startI = eyeLight != null ? eyeLight.intensity : 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            if (eyeLight != null) eyeLight.intensity = Mathf.Lerp(startI, 0f, t);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Vector3 l = Quaternion.Euler(0, -detectionAngle, 0) * transform.forward * detectionRange;
        Vector3 r = Quaternion.Euler(0, detectionAngle, 0) * transform.forward * detectionRange;
        Gizmos.DrawLine(transform.position, transform.position + l);
        Gizmos.DrawLine(transform.position, transform.position + r);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, loseRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, avoidanceRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_wanderTarget, 0.2f);
            Gizmos.DrawLine(transform.position, _wanderTarget);
        }

        // Float height indicator
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * floatHeight);
    }
}