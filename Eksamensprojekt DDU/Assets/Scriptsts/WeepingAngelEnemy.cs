using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngelEnemy : MonoBehaviour
{
    public enum AngelState { Idle, Hunting, Frozen, Wandering }

    [Header("Referencer")]
    public Transform playerTransform;
    public Camera playerCamera;

    [Header("Line-of-Sight")]
    public float freezeAngle = 25f;
    public float unfreezeAngle = 32f;
    public int losChecksPerSecond = 20;
    public LayerMask occlusionMask = ~0;
    [Range(0, 6)]
    public int multiRayCount = 3;
    [Range(0f, 0.1f)]
    public float viewportMargin = 0.02f;

    [Header("Bevægelse (NavMesh)")]
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 180f;

    [Header("Angreb")]
    public float attackRange = 1f;

    [Header("Wandering (mens spiller er gemt)")]
    public float wanderRadius = 10f;
    public float wanderWaitMin = 1f;
    public float wanderWaitMax = 3f;
    public float wanderSpeed = 1.5f;

    [Header("Adfærd")]
    public bool returnToStartOnLightOn = true;

    [Header("Lyd")]
    public AudioClip moveSound;
    public AudioClip freezeSound;
    public AudioClip killSound;
    public AudioSource movementSource;
    public AudioSource sfxSource;

    // ── Runtime ───────────────────────────────────────────────────
    private AngelState _state = AngelState.Idle;
    private float _losTimer;
    private float _losInterval;
    private bool _playerLooking;
    private bool _playingFreezeSound;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Rigidbody _rb;
    private NavMeshAgent _agent;
    private bool _playerIsHiding;
    private AngelState _stateBeforeHide = AngelState.Idle;
    private Coroutine _wanderCoroutine;

    public AngelState CurrentState => _state;

    private static readonly float[] BodyOffsets = { 0f, 0.8f, 1.4f, 1.8f, 0.4f, 1.1f, 0.2f };

    // ── Init ──────────────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb = GetComponent<Rigidbody>();

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
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

    void OnEnable()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
    }

    void OnDisable()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    // ── Update ────────────────────────────────────────────────────
    void Update()
    {
        if (_state == AngelState.Idle) return;

        // LOS-tjek kører altid (også fra kassen — spilleren kan fryse englen fra kassens åbning)
        _losTimer += Time.deltaTime;
        if (_losTimer >= _losInterval)
        {
            _losTimer = 0f;
            bool wasLooking = _playerLooking;
            _playerLooking = CheckLineOfSight();

            if (_playerLooking)
            {
                if (_state == AngelState.Hunting || _state == AngelState.Wandering)
                    EnterFrozen(wasLooking);
            }
            else
            {
                if (_state == AngelState.Frozen)
                {
                    // Ikke længere set — hvad skal englen gøre nu?
                    if (_playerIsHiding)
                        EnterWandering();
                    else
                        EnterHunting();
                }
            }
        }

        // Tilstandsspecifik opdatering
        switch (_state)
        {
            case AngelState.Hunting:
                if (!_playerIsHiding && playerTransform != null)
                {
                    _agent.isStopped = false;
                    _agent.SetDestination(playerTransform.position);
                    CheckAttackRange();
                }
                // Bemærk: hvis _playerIsHiding bliver true mens vi er i Hunting,
                // håndteres skiftet til Wandering af OnPlayerHidingChanged — ikke her
                UpdateMoveSound();
                break;

            case AngelState.Wandering:
                UpdateMoveSound();
                break;

            default:
                StopMoveSound();
                break;
        }
    }

    // ── Hiding-event ──────────────────────────────────────────────
    void OnPlayerHidingChanged(bool isHiding)
    {
        _playerIsHiding = isHiding;

        if (isHiding)
        {
            _stateBeforeHide = _state;

            switch (_state)
            {
                case AngelState.Hunting:
                    EnterWandering();
                    break;
                case AngelState.Frozen:
                    // Englen var frosset — spilleren gemte sig mens den stod stille.
                    // Start vandring da der ikke længere er nogen at fryse ved.
                    EnterWandering();
                    break;
            }
        }
        else
        {
            // Spilleren trådte ud af kassen
            StopWandering();
            _playerLooking = CheckLineOfSight();
            if (_playerLooking)
                EnterFrozen(false);
            else
                EnterHunting();
        }
    }

    // ── Lys-events (kaldes af WarehouseLightController) ───────────
    public void OnLightOff()
    {
        if (_playerIsHiding)
            EnterWandering();
        else
            EnterHunting();
    }
    public void OnLightOn() => EnterIdle();

    // ── Tilstandsskift ────────────────────────────────────────────
    void EnterIdle()
    {
        StopWandering();
        _state = AngelState.Idle;
        _agent.isStopped = true;
        StopMoveSound();

        if (returnToStartOnLightOn)
            StartCoroutine(ReturnToStartWhenUnwatched());
    }

    void EnterHunting()
    {
        StopWandering();
        _state = AngelState.Hunting;
        _agent.speed = moveSpeed;
        _agent.isStopped = false;
        StartMoveSound();
    }

    void EnterFrozen(bool wasAlreadyLooking)
    {
        StopWandering();
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

    void EnterWandering()
    {
        _state = AngelState.Wandering;
        _agent.speed = wanderSpeed;
        _agent.isStopped = false;

        StopWandering();
        _wanderCoroutine = StartCoroutine(WanderCoroutine());
    }

    void StopWandering()
    {
        if (_wanderCoroutine != null)
        {
            StopCoroutine(_wanderCoroutine);
            _wanderCoroutine = null;
        }
    }

    // ── Vandre-coroutine ──────────────────────────────────────────
    IEnumerator WanderCoroutine()
    {
        while (true)
        {
            Vector3 randomPoint = _startPosition + Random.insideUnitSphere * wanderRadius;
            randomPoint.y = _startPosition.y;

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);

            float timeout = 10f;
            while (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance)
            {
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
                yield return null;
            }

            yield return new WaitForSeconds(Random.Range(wanderWaitMin, wanderWaitMax));
        }
    }

    // ── Hjælpe-coroutines ─────────────────────────────────────────
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

        if (_agent != null) _agent.isStopped = true;
        transform.position = _startPosition;
        transform.rotation = _startRotation;
    }

    // ── Line-of-Sight ─────────────────────────────────────────────
    bool CheckLineOfSight()
    {
        if (playerCamera == null || playerTransform == null) return false;

        int totalRays = 1 + Mathf.Clamp(multiRayCount, 0, BodyOffsets.Length - 1);
        for (int i = 0; i < totalRays; i++)
        {
            if (IsSinglePointVisible(transform.position + Vector3.up * BodyOffsets[i]))
                return true;
        }
        return false;
    }

    bool IsSinglePointVisible(Vector3 worldTarget)
    {
        Transform cam = playerCamera.transform;
        Vector3 eyePos = cam.position;

        Vector3 vp = playerCamera.WorldToViewportPoint(worldTarget);
        bool onScreen = vp.z > 0f
                     && vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin
                     && vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin;
        if (!onScreen) return false;

        Vector3 dirToTarget = (worldTarget - eyePos).normalized;
        float angle = Vector3.Angle(cam.forward, dirToTarget);
        float threshold = (_state == AngelState.Frozen) ? unfreezeAngle : freezeAngle;
        if (angle > threshold) return false;

        float dist = Vector3.Distance(eyePos, worldTarget);
        if (Physics.Raycast(eyePos, dirToTarget, out RaycastHit hit, dist + 0.1f, occlusionMask))
        {
            bool hitAngel = hit.transform == transform || hit.transform.IsChildOf(transform);
            if (!hitAngel) return false;
        }

#if UNITY_EDITOR
        Debug.DrawLine(eyePos, worldTarget, Color.green);
#endif
        return true;
    }

    // ── Angreb ────────────────────────────────────────────────────
    void CheckAttackRange()
    {
        if (playerTransform == null || _playerIsHiding) return;

        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            EnterIdle();
            PlayOneShot(killSound);
            GameOverManager.Instance?.TriggerGameOver("Du blev fanget af englen...");
        }
    }

    // ── Lyd ───────────────────────────────────────────────────────
    void StartMoveSound()
    {
        if (movementSource == null || moveSound == null) return;
        if (movementSource.clip != moveSound) movementSource.clip = moveSound;
        movementSource.loop = true;
        if (!movementSource.isPlaying) movementSource.Play();
    }

    void StopMoveSound()
    {
        if (movementSource != null && movementSource.isPlaying) movementSource.Stop();
    }

    void UpdateMoveSound()
    {
        if (movementSource == null || moveSound == null) return;
        bool moving = _agent.velocity.magnitude > 0.05f;
        if (moving && !movementSource.isPlaying) StartMoveSound();
        if (!moving && movementSource.isPlaying) StopMoveSound();
    }

    void PlayOneShot(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawWireSphere(Application.isPlaying ? _startPosition : transform.position, wanderRadius);
        Gizmos.color = Color.cyan;
        int totalRays = 1 + Mathf.Clamp(multiRayCount, 0, BodyOffsets.Length - 1);
        for (int i = 0; i < totalRays; i++)
            Gizmos.DrawSphere(transform.position + Vector3.up * BodyOffsets[i], 0.06f);
    }
#endif
}