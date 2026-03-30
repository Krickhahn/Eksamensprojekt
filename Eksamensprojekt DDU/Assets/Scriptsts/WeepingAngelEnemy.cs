using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngelEnemy : MonoBehaviour
{
    // Wandering er en ny tilstand der bruges mens spilleren er gemt i kassen
    public enum AngelState { Idle, Hunting, Frozen, Wandering }

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

    [Header("Wandering (mens spiller er gemt)")]
    [Tooltip("Radius englen vandrer rundt inden for fra sin startposition")]
    public float wanderRadius = 10f;
    [Tooltip("Minimum sekunder englen venter ved hvert punkt inden den finder et nyt")]
    public float wanderWaitMin = 1f;
    [Tooltip("Maksimum sekunder englen venter ved hvert punkt inden den finder et nyt")]
    public float wanderWaitMax = 3f;
    [Tooltip("Hastighed mens englen vandrer (bør være lavere end moveSpeed)")]
    public float wanderSpeed = 1.5f;

    [Header("Adfærd")]
    public bool returnToStartOnLightOn = true;

    [Header("Lyd")]
    public AudioClip moveSound;
    public AudioClip freezeSound;
    public AudioClip killSound;
    public AudioSource movementSource;
    public AudioSource sfxSource;

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

    // ── Hiding / Wandering state ──────────────────────────────
    private bool _playerIsHiding;
    private Coroutine _wanderCoroutine;

    // Tilstand inden spilleren gemte sig — bruges til at vende tilbage korrekt
    private AngelState _stateBeforeHide = AngelState.Idle;

    public AngelState CurrentState => _state;

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

    void Update()
    {
        if (_state == AngelState.Idle) return;

        // ── LOS-tjek kører altid — også fra kassen ───────────
        // Wandering-tilstanden kan fryse englen hvis spilleren kigger ud af kasseåbningen.
        _losTimer += Time.deltaTime;
        if (_losTimer >= _losInterval)
        {
            _losTimer = 0f;
            bool wasLooking = _playerLooking;
            _playerLooking = CheckLineOfSight();

            if (_playerLooking)
            {
                // Spilleren ser englen — frys uanset hvilken tilstand vi er i
                if (_state == AngelState.Hunting || _state == AngelState.Wandering)
                    EnterFrozen(wasLooking);
            }
            else
            {
                // Spilleren ser ikke englen — genoptag relevant tilstand
                if (_state == AngelState.Frozen)
                {
                    if (_playerIsHiding)
                        EnterWandering();
                    else
                        EnterHunting();
                }
            }
        }

        // ── Tilstandsspecifik opdatering ─────────────────────
        switch (_state)
        {
            case AngelState.Hunting:
                _agent.SetDestination(playerTransform.position);
                CheckAttackRange();
                UpdateMoveSound();
                break;

            case AngelState.Wandering:
                // Selve vandringen styres af WanderCoroutine — intet her
                UpdateMoveSound();
                break;

            default:
                StopMoveSound();
                break;
        }
    }

    // ── Hiding event ─────────────────────────────────────────
    void OnPlayerHidingChanged(bool isHiding)
    {
        _playerIsHiding = isHiding;

        if (isHiding)
        {
            _stateBeforeHide = _state;

            switch (_state)
            {
                case AngelState.Hunting:
                    // Spilleren gemte sig midt i jagten — gå til wandering
                    EnterWandering();
                    break;

                case AngelState.Frozen:
                    // Spilleren gemte sig mens englen var frosset.
                    // LOS-tjekket vil nu returnere false (kassen blokerer muligvis),
                    // og Update() skifter selv til Wandering næste frame.
                    // Vi starter coroutinen nu for at undgå en frames forsinkelse.
                    EnterWandering();
                    break;
            }
        }
        else
        {
            // Spilleren kom ud af kassen
            StopWandering();

            // Tjek straks om spilleren er synlig nu de er trådt ud
            _playerLooking = CheckLineOfSight();
            if (_playerLooking)
                EnterFrozen(false);
            else
                EnterHunting();

            Debug.Log($"[{name}] Spiller ude af kassen — genoptager jagt.");
        }
    }

    // ── Lys-events ───────────────────────────────────────────
    public void OnLightOff() => EnterHunting();
    public void OnLightOn() => EnterIdle();

    // ── Tilstandsskift ────────────────────────────────────────
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

        StopWandering(); // Stop eventuel eksisterende coroutine
        _wanderCoroutine = StartCoroutine(WanderCoroutine());

        Debug.Log($"[{name}] Vandrer rundt mens spiller er gemt.");
    }

    void StopWandering()
    {
        if (_wanderCoroutine != null)
        {
            StopCoroutine(_wanderCoroutine);
            _wanderCoroutine = null;
        }
    }

    // ── Vandre-coroutine ─────────────────────────────────────
    IEnumerator WanderCoroutine()
    {
        while (true)
        {
            // Find et tilfældigt punkt på NavMesh inden for wanderRadius
            Vector3 randomPoint = _startPosition + Random.insideUnitSphere * wanderRadius;
            randomPoint.y = _startPosition.y; // Hold på samme højde som startposition

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);

            // Vent til englen er nået frem (eller timeout efter 10 sekunder)
            float timeout = 10f;
            while (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance)
            {
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
                yield return null;
            }

            // Vent lidt ved punktet inden næste destination vælges
            float waitTime = Random.Range(wanderWaitMin, wanderWaitMax);
            yield return new WaitForSeconds(waitTime);
        }
    }

    // ── Hjælpe-coroutines ─────────────────────────────────────
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

        Vector3 vp = playerCamera.WorldToViewportPoint(worldTarget);
        bool onScreen = vp.z > 0f
                     && vp.x >= -viewportMargin && vp.x <= 1f + viewportMargin
                     && vp.y >= -viewportMargin && vp.y <= 1f + viewportMargin;
        if (!onScreen) return false;

        Vector3 dirToTarget = (worldTarget - eyePos).normalized;
        float angle = Vector3.Angle(cam.forward, dirToTarget);
        float threshold = (_state == AngelState.Frozen) ? unfreezeAngle : freezeAngle;
        if (angle > threshold) return false;

        float distance = Vector3.Distance(eyePos, worldTarget);
        if (Physics.Raycast(eyePos, dirToTarget, out RaycastHit hit, distance + 0.1f, occlusionMask))
        {
            bool hitAngel = hit.transform == transform || hit.transform.IsChildOf(transform);
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
        if (_playerIsHiding) return;

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

        // Vis wander-radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(Application.isPlaying ? _startPosition : transform.position, wanderRadius);
    }
#endif
}