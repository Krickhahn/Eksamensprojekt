using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngelEnemy : MonoBehaviour
{
    public enum AngelState { Idle, Hunting, Frozen }

    [Header("Referencer")]
    public Transform playerTransform;
    public Camera playerCamera;

    [Header("Line-of-Sight")]
    [Tooltip("Vinkel i grader fra kameraets forward-vektor der tæller som 'set'")]
    public float freezeAngle = 25f;
    [Tooltip("Skal være større end freezeAngle – hysteresis forhindrer flimmer ved grænsen")]
    public float unfreezeAngle = 32f;
    [Tooltip("Antal LOS-checks per sekund")]
    public int losChecksPerSecond = 20;
    [Tooltip("Lag der kan blokere sigtelinjen")]
    public LayerMask occlusionMask = ~0;
    [Tooltip("Antal ekstra rays spredt over angel-kroppen (0 = kun én center-ray)")]
    [Range(0, 6)]
    public int multiRayCount = 3;
    [Tooltip("Margin inden for viewport-kanten der stadig tæller som 'på skærm' (0–0.1)")]
    [Range(0f, 0.1f)]
    public float viewportMargin = 0.02f;

    [Header("Bevægelse (NavMesh)")]
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 180f;

    [Header("Angreb")]
    public float attackRange = 1f;

    [Header("Adfærd")]
    public bool returnToStartOnLightOn = true;

    [Header("Lyd")]
    public AudioClip moveSound;
    public AudioClip freezeSound;
    public AudioClip killSound;
    public AudioSource movementSource;   // loop-kilde til bevægelyd
    public AudioSource sfxSource;        // one-shot kilde til freeze/kill

    // ── Runtime ──────────────────────────────────────────────
    private AngelState _state = AngelState.Idle;
    private float _losTimer;
    private float _losInterval;
    private bool _playerLooking;
    private bool _playingFreezeSound;

    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private Rigidbody _rb;
    private NavMeshAgent _agent;

    public AngelState CurrentState => _state;

    // Offset-punkter på angel-kroppen der raycastes mod (lokale Y-offsets)
    private static readonly float[] BodyOffsets = { 0f, 0.8f, 1.4f, 1.8f, 0.4f, 1.1f, 0.2f };

    // ── Unity Lifecycle ──────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.freezeRotation = true;

        _agent.speed = moveSpeed;
        _agent.angularSpeed = rotationSpeed;
        _agent.updateRotation = true;
        _agent.updatePosition = true;

        _losInterval = 1f / Mathf.Max(1, losChecksPerSecond);

        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    void Update()
    {
        if (_state == AngelState.Idle) return;

        // ── LOS-check med fast interval ───────────────────────
        _losTimer += Time.deltaTime;
        if (_losTimer >= _losInterval)
        {
            _losTimer = 0f;
            bool wasLooking = _playerLooking;
            _playerLooking = CheckLineOfSight();

            if (_playerLooking && _state == AngelState.Hunting)
                EnterFrozen(wasLooking);
            else if (!_playerLooking && _state == AngelState.Frozen)
                EnterHunting();
        }

        // ── Jagt-logik ────────────────────────────────────────
        if (_state == AngelState.Hunting && playerTransform != null)
        {
            _agent.SetDestination(playerTransform.position);
            CheckAttackRange();
            UpdateMoveSound();
        }
        else
        {
            StopMoveSound();
        }
    }

    // ── Lys-events ───────────────────────────────────────────
    public void OnLightOff() => EnterHunting();
    public void OnLightOn() => EnterIdle();

    // ── Tilstandsskift ────────────────────────────────────────
    void EnterIdle()
    {
        _state = AngelState.Idle;
        _agent.isStopped = true;
        StopMoveSound();

        if (returnToStartOnLightOn)
            StartCoroutine(ReturnToStartWhenUnwatched());
    }

    void EnterHunting()
    {
        _state = AngelState.Hunting;
        _agent.isStopped = false;
        StartMoveSound();
    }

    void EnterFrozen(bool wasAlreadyLooking)
    {
        _state = AngelState.Frozen;
        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.velocity = Vector3.zero;
        StopMoveSound();

        if (!wasAlreadyLooking && freezeSound != null && sfxSource != null)
        {
            _playingFreezeSound = true;
            sfxSource.PlayOneShot(freezeSound);
            StartCoroutine(ResetFreezeSoundFlag(freezeSound.length));
        }
    }

    IEnumerator ResetFreezeSoundFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        _playingFreezeSound = false;
    }

    IEnumerator ReturnToStartWhenUnwatched()
    {
        float unwatchedTime = 0f;
        while (unwatchedTime < 1f)
        {
            unwatchedTime = CheckLineOfSight() ? 0f : unwatchedTime + Time.deltaTime;
            yield return null;
        }

        if (_agent != null)
            _agent.isStopped = true;

        transform.position = _startPosition;
        transform.rotation = _startRotation;
    }

    // ── Line-of-Sight ─────────────────────────────────────────
    bool CheckLineOfSight()
    {
        if (playerCamera == null || playerTransform == null) return false;

        int totalRays = 1 + Mathf.Clamp(multiRayCount, 0, BodyOffsets.Length - 1);

        for (int i = 0; i < totalRays; i++)
        {
            Vector3 target = transform.position + Vector3.up * BodyOffsets[i];
            if (IsSinglePointVisible(target))
                return true;
        }

        return false;
    }

    bool IsSinglePointVisible(Vector3 worldTarget)
    {
        Transform cam = playerCamera.transform;
        Vector3 eyePos = cam.position;

        // 1. Viewport-check
        Vector3 vp = playerCamera.WorldToViewportPoint(worldTarget);
        bool onScreen = vp.z > 0f
                     && vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin
                     && vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin;
        if (!onScreen) return false;

        // 2. Vinkelcheck (hysteresis)
        Vector3 dirToTarget = (worldTarget - eyePos).normalized;
        float angle = Vector3.Angle(cam.forward, dirToTarget);
        float threshold = (_state == AngelState.Frozen) ? unfreezeAngle : freezeAngle;
        if (angle > threshold) return false;

        // 3. Occlusion raycast
        float distance = Vector3.Distance(eyePos, worldTarget);
        if (Physics.Raycast(eyePos, dirToTarget, out RaycastHit hit, distance + 0.1f, occlusionMask))
        {
            bool hitAngel = hit.transform == transform
                         || hit.transform.IsChildOf(transform);
            if (!hitAngel) return false;
        }

#if UNITY_EDITOR
        Debug.DrawLine(eyePos, worldTarget, Color.green);
#endif
        return true;
    }

    // ── Attack ───────────────────────────────────────────────
    void CheckAttackRange()
    {
        if (playerTransform == null) return;

        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            EnterIdle();
            PlayOneShot(killSound);
            GameOverManager.Instance?.TriggerGameOver();
        }
    }

    // ── Lyd ──────────────────────────────────────────────────
    void StartMoveSound()
    {
        if (movementSource == null || moveSound == null) return;
        if (movementSource.clip != moveSound) movementSource.clip = moveSound;
        movementSource.loop = true;
        if (!movementSource.isPlaying) movementSource.Play();
    }

    void StopMoveSound()
    {
        if (movementSource != null && movementSource.isPlaying)
            movementSource.Stop();
    }

    void UpdateMoveSound()
    {
        if (movementSource == null || moveSound == null) return;
        bool moving = _agent.velocity.magnitude > 0.05f;
        if (moving && !movementSource.isPlaying) StartMoveSound();
        else if (!moving && movementSource.isPlaying) StopMoveSound();
    }

    void PlayOneShot(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.cyan;
        int totalRays = 1 + Mathf.Clamp(multiRayCount, 0, BodyOffsets.Length - 1);
        for (int i = 0; i < totalRays; i++)
            Gizmos.DrawSphere(transform.position + Vector3.up * BodyOffsets[i], 0.06f);
    }
#endif
}