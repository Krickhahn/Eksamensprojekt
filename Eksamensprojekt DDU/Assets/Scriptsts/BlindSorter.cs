using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BlindSorter : MonoBehaviour
{
    public enum SorterState { Patrolling, Investigating, Chasing }

    [Header("Referencer")]
    public Transform playerTransform;

    [Header("Hørelse")]
    [Tooltip("Maksimal afstand fjenden kan høre normal gang")]
    public float hearingRangeWalk = 8f;
    [Tooltip("Maksimal afstand fjenden kan høre sprint")]
    public float hearingRangeSprint = 20f;
    [Tooltip("Maksimal afstand fjenden kan høre crouch-gang")]
    public float hearingRangeCrouch = 2f;
    [Tooltip("Hastighed der tæller som sprint")]
    public float sprintThreshold = 8f;
    [Tooltip("Hastighed der tæller som crouch (under denne = stille)")]
    public float crouchThreshold = 3f;
    [Tooltip("Interval i sekunder mellem lyd-tjek")]
    public float hearingCheckInterval = 0.2f;

    [Header("Bevægelse")]
    public float patrolSpeed = 1.8f;
    public float investigateSpeed = 2.5f;
    public float chaseSpeed = 6f;
    public float attackRange = 1.2f;

    [Header("Patrol")]
    [Tooltip("Patrol-waypoints. Lad stå tomme for tilfældig vandring.")]
    public Transform[] patrolPoints;
    public float patrolWaitTime = 2f;

    [Header("Undersøgelse")]
    [Tooltip("Sekunder fjenden undersøger en lyd-position inden den giver op")]
    public float investigateDuration = 8f;
    [Tooltip("Radius fjenden søger rundt om lyd-positionen")]
    public float investigateRadius = 4f;

    [Header("Jagt")]
    [Tooltip("Sekunder fjenden fortsætter jagten EFTER spilleren er blevet stille og ude af høreradius")]
    public float chasePersistence = 6f;

    [Header("Lyd")]
    public AudioSource movementSource;
    public AudioClip patrolSound;
    public AudioClip alertSound;
    public AudioSource sfxSource;
    public AudioClip killSound;

    // ── Private state ─────────────────────────────────────────────
    private SorterState _state = SorterState.Patrolling;
    private NavMeshAgent _agent;
    private PlayerMovement _playerMovement;

    private float _hearingTimer;
    private float _chaseTimer;
    private bool _playerHeardThisFrame; // sættes af CheckHearing, læses af Update
    private Vector3 _lastHeardPosition;
    private bool _playerIsHiding;

    private Coroutine _behaviourCoroutine;
    private int _patrolIndex;

    public SorterState CurrentState => _state;

    // ── Init ──────────────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                _playerMovement = p.GetComponent<PlayerMovement>();
            }
        }

        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;

        _agent.speed = patrolSpeed;
        _behaviourCoroutine = StartCoroutine(PatrolCoroutine());
        StartCoroutine(RegisterHidingListener());
    }

    IEnumerator RegisterHidingListener()
    {
        yield return null;
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
    }

    private bool _isDead;

    void OnDestroy()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    // ── Hiding-event ──────────────────────────────────────────────
    void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;

        if (hiding && _state == SorterState.Chasing)
            EnterInvestigate(_lastHeardPosition); // Stop jagten, undersøg sidst kendte position
    }

    // ── Update ────────────────────────────────────────────────────
    void Update()
    {
        _playerHeardThisFrame = false;

        _hearingTimer += Time.deltaTime;
        if (_hearingTimer >= hearingCheckInterval)
        {
            _hearingTimer = 0f;
            CheckHearing();
        }

        switch (_state)
        {
            case SorterState.Chasing:
                // Tæl kun ned når spilleren IKKE er indenfor høreradius denne frame
                if (!_playerHeardThisFrame)
                {
                    _chaseTimer -= Time.deltaTime;
                    if (_chaseTimer <= 0f)
                        EnterInvestigate(_lastHeardPosition);
                }

                // Angreb
                if (!_playerIsHiding && !_isDead && playerTransform != null &&
                    Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
                {
                    _isDead = true;
                    Debug.Log("[BlindSorter] Angriber spilleren!");
                    if (GameOverManager.Instance != null)
                        GameOverManager.Instance.TriggerGameOver("Den blinde pakkesorter fandt dig...");
                    else
                        Debug.LogWarning("[BlindSorter] GameOverManager.Instance er null!");
                }
                break;
        }
    }

    // ── Backup angreb via collision ───────────────────────────────
    // Sørg for at fjende-prefabben har en Collider med Is Trigger = true
    void OnTriggerEnter(Collider other)
    {
        if (_isDead || _playerIsHiding || _state != SorterState.Chasing) return;
        if (!other.CompareTag("Player")) return;

        _isDead = true;
        Debug.Log("[BlindSorter] Rammer spilleren via trigger!");
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.TriggerGameOver("Den blinde pakkesorter fandt dig...");
        else
            Debug.LogWarning("[BlindSorter] GameOverManager.Instance er null!");
    }

    // ── Hørelses-logik ────────────────────────────────────────────
    void CheckHearing()
    {
        // Ignorer lyd fra en gemt spiller
        if (_playerIsHiding || playerTransform == null) return;

        float playerSpeed = _playerMovement != null ? _playerMovement.GetCurrentSpeed() : 0f;

        // Under crouchThreshold tæller vi spilleren som stille
        if (playerSpeed < crouchThreshold) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool isSprinting = playerSpeed >= sprintThreshold;
        float hearingRange = isSprinting ? hearingRangeSprint
                            : playerSpeed <= crouchThreshold ? hearingRangeCrouch
                            : hearingRangeWalk;

        if (dist > hearingRange) return;

        // Spilleren hørt
        _playerHeardThisFrame = true;
        _lastHeardPosition = playerTransform.position;

        if (isSprinting)
            EnterChase();                          // Sprint → straks jagt
        else if (_state == SorterState.Chasing)
            _chaseTimer = chasePersistence;        // Fortsat gang under jagt → nulstil timer
        else
            EnterInvestigate(_lastHeardPosition);  // Normal gang → undersøg
    }

    /// <summary>
    /// Ekstern lyd-trigger — kald fra andre scripts når spilleren laver lyd.
    /// volume 0–1 skalerer høreradius.
    /// </summary>
    public void MakeNoise(Vector3 position, float volume = 1f)
    {
        if (_playerIsHiding) return;

        float dist = Vector3.Distance(transform.position, position);
        float range = hearingRangeSprint * volume;
        if (dist > range) return;

        _lastHeardPosition = position;
        if (volume >= 0.8f) EnterChase();
        else EnterInvestigate(position);
    }

    // ── Tilstandsskift ────────────────────────────────────────────
    void EnterChase()
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = SorterState.Chasing;
        _agent.speed = chaseSpeed;
        _agent.isStopped = false;
        _chaseTimer = chasePersistence;

        PlaySFX(alertSound);
        _behaviourCoroutine = StartCoroutine(ChaseCoroutine());
    }

    void EnterInvestigate(Vector3 position)
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = SorterState.Investigating;
        _agent.speed = investigateSpeed;
        _agent.isStopped = false;

        _behaviourCoroutine = StartCoroutine(InvestigateCoroutine(position));
    }

    void EnterPatrol()
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = SorterState.Patrolling;
        _agent.speed = patrolSpeed;
        _agent.isStopped = false;

        _behaviourCoroutine = StartCoroutine(PatrolCoroutine());
    }

    // ── Coroutines ────────────────────────────────────────────────
    IEnumerator PatrolCoroutine()
    {
        while (_state == SorterState.Patrolling)
        {
            Vector3 dest;

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                dest = patrolPoints[_patrolIndex % patrolPoints.Length].position;
                _patrolIndex++;
            }
            else
            {
                Vector3 rand = transform.position + Random.insideUnitSphere * 10f;
                rand.y = transform.position.y;
                if (!NavMesh.SamplePosition(rand, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                { yield return new WaitForSeconds(1f); continue; }
                dest = hit.position;
            }

            _agent.SetDestination(dest);

            float timeout = 15f;
            while (_state == SorterState.Patrolling &&
                   (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance))
            {
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
                yield return null;
            }

            if (_state == SorterState.Patrolling)
                yield return new WaitForSeconds(patrolWaitTime);
        }
    }

    IEnumerator InvestigateCoroutine(Vector3 targetPos)
    {
        _agent.SetDestination(targetPos);

        float timeout = 10f;
        while (_state == SorterState.Investigating &&
               (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance))
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f) break;
            yield return null;
        }

        float searchElapsed = 0f;
        while (_state == SorterState.Investigating && searchElapsed < investigateDuration)
        {
            searchElapsed += Time.deltaTime;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                Vector3 rand = targetPos + Random.insideUnitSphere * investigateRadius;
                rand.y = targetPos.y;
                if (NavMesh.SamplePosition(rand, out NavMeshHit hit, investigateRadius, NavMesh.AllAreas))
                    _agent.SetDestination(hit.position);
            }

            yield return null;
        }

        if (_state == SorterState.Investigating)
            EnterPatrol();
    }

    IEnumerator ChaseCoroutine()
    {
        while (_state == SorterState.Chasing && playerTransform != null)
        {
            if (!_playerIsHiding)
                _agent.SetDestination(playerTransform.position);
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ── Lyd ───────────────────────────────────────────────────────
    void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, hearingRangeWalk);
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, hearingRangeSprint);
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, hearingRangeCrouch);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}