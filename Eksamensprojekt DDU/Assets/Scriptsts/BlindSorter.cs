using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BlindSorter : MonoBehaviour
{
    public enum SorterState { Patrolling, Investigating, Chasing, Attacking }

    // ─────────────────────────────────────────────
    //  REFERENCER
    // ─────────────────────────────────────────────
    [Header("Referencer")]
    public Transform playerTransform;

    // ─────────────────────────────────────────────
    //  HØRELSE
    // ─────────────────────────────────────────────
    [Header("Hørelse")]
    [Tooltip("Afstand fjenden kan høre normal gang")]
    public float hearingRangeWalk = 8f;
    [Tooltip("Afstand fjenden kan høre sprint")]
    public float hearingRangeSprint = 20f;
    [Tooltip("Afstand fjenden kan høre crouch-gang")]
    public float hearingRangeCrouch = 2f;
    [Tooltip("Hastighed der tæller som sprint")]
    public float sprintThreshold = 8f;
    [Tooltip("Hastighed under denne tæller som crouch/stille")]
    public float crouchThreshold = 3f;
    [Tooltip("Interval i sekunder mellem lyd-tjek")]
    public float hearingCheckInterval = 0.2f;
    [Tooltip("Antal sekunder med vedvarende lyd under undersøgelse inden fjenden skifter til jagt")]
    public float investigateToChaseTime = 3f;

    // ─────────────────────────────────────────────
    //  BEVÆGELSE
    // ─────────────────────────────────────────────
    [Header("Bevægelse")]
    public float patrolSpeed = 1.8f;
    public float investigateSpeed = 2.5f;
    public float chaseSpeed = 6f;

    // ─────────────────────────────────────────────
    //  PATROL
    // ─────────────────────────────────────────────
    [Header("Patrol")]
    [Tooltip("Waypoints. Lad stå tomme for automatisk søgning via tag.")]
    public Transform[] patrolPoints;
    [Tooltip("Tag til automatisk at finde patrol points")]
    public string patrolPointTag = "PatrolPoint";
    [Tooltip("Sekunder fjenden holder idle ved hvert patrolpunkt")]
    public float patrolWaitTime = 2f;

    // ─────────────────────────────────────────────
    //  UNDERSØGELSE
    // ─────────────────────────────────────────────
    [Header("Undersøgelse")]
    [Tooltip("Sekunder fjenden undersøger inden den giver op")]
    public float investigateDuration = 8f;
    [Tooltip("Radius fjenden vandrer rundt i ved undersøgelse")]
    public float investigateRadius = 4f;
    [Tooltip("Sekunder fjenden holder idle ved undersøgelsespunktet inden den vandrer rundt")]
    public float investigateIdleTime = 1.5f;

    // ─────────────────────────────────────────────
    //  JAGT
    // ─────────────────────────────────────────────
    [Header("Jagt")]
    [Tooltip("Sekunder fjenden fortsætter jagten EFTER spilleren er stille og ude af høreradius")]
    public float chasePersistence = 3f;

    // ─────────────────────────────────────────────
    //  ANGREB
    // ─────────────────────────────────────────────
    [Header("Angreb")]
    [Tooltip("Afstand hvorfra fjenden kan angribe")]
    public float attackRange = 1.5f;
    [Tooltip("Længden på attack-animationen i sekunder")]
    public float attackAnimationDuration = 1.2f;
    [Tooltip("Pause på stedet efter angrebet inden fjenden reagerer igen")]
    public float postAttackWait = 1f;
    [Tooltip("Minimum sekunder mellem angreb")]
    public float attackCooldown = 3f;

    // ─────────────────────────────────────────────
    //  ANIMATION
    // ─────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("Animator på fjende-modellen. Finder selv i børn hvis tom.")]
    public Animator animator;
    [Tooltip("Float parameter til blend tree — styrer walk/run/idle")]
    public string animParamSpeed = "Speed";
    [Tooltip("Trigger parameter til angreb")]
    public string animParamAttack = "Attack";
    [Tooltip("Bool parameter der sættes true under spawn-animationen")]
    public string animParamSpawn = "Spawn";

    // ─────────────────────────────────────────────
    //  LYD
    // ─────────────────────────────────────────────
    [Header("Lyd")]
    public AudioSource movementSource;
    public AudioSource sfxSource;
    [Tooltip("Passiv lyd der looper mens fjenden patruljerer (inkl. idle ved waypoints)")]
    public AudioClip patrolSound;
    [Tooltip("Spilles når fjenden opdager en lyd og begynder at undersøge")]
    public AudioClip investigateSound;
    [Tooltip("Spilles når fjenden begynder at jagte")]
    public AudioClip alertSound;
    [Tooltip("Spilles idet fjenden slår")]
    public AudioClip attackSound;
    [Tooltip("Spilles når spilleren dør")]
    public AudioClip killSound;
    [Tooltip("Spilles når fjenden giver op og vender tilbage til patrulje")]
    public AudioClip losePlayerSound;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    private SorterState _state = SorterState.Patrolling;
    private NavMeshAgent _agent;
    private PlayerMovement _playerMovement;

    private float _hearingTimer;
    private float _attackCooldownTimer;
    private bool _playerIsHiding;
    private Vector3 _lastHeardPosition;

    // Lyd-flag sat af CheckHearing, læst af coroutines
    private bool _soundHeardThisCheck;   // spilleren laver en hvilken som helst lyd
    private bool _sprintHeardThisCheck;  // spilleren sprinter specifikt

    // Vedvarende-lyd-timer bruges til investigate→chase
    private float _continuousSoundTimer;

    private Coroutine _behaviourCoroutine;
    private int _patrolIndex;

    public SorterState CurrentState => _state;

    // ─────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.stoppingDistance = 0f;

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                _playerMovement = p.GetComponent<PlayerMovement>();
            }
        }

        if (patrolPoints != null && patrolPoints.Length > 0)
            patrolPoints = System.Array.FindAll(patrolPoints, p => p != null);

        if (patrolPoints == null || patrolPoints.Length == 0)
            FindPatrolPointsByTag();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;

        StartCoroutine(RegisterHidingListener());

        _agent.speed = patrolSpeed;
        StartMovementAudio(patrolSound);
        _behaviourCoroutine = StartCoroutine(PatrolCoroutine());
    }

    IEnumerator RegisterHidingListener()
    {
        yield return null;
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
    }

    void OnDestroy()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    // ─────────────────────────────────────────────
    //  HIDING EVENT
    // ─────────────────────────────────────────────
    void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;

        if (hiding && _state == SorterState.Chasing)
            EnterInvestigate(_lastHeardPosition);
    }

    // ─────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────
    void Update()
    {
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        UpdateAnimator();

        _hearingTimer += Time.deltaTime;
        if (_hearingTimer >= hearingCheckInterval)
        {
            _hearingTimer = 0f;
            CheckHearing();
        }
    }

    // ─────────────────────────────────────────────
    //  HØRELSES-LOGIK
    // ─────────────────────────────────────────────
    void CheckHearing()
    {
        _soundHeardThisCheck = false;
        _sprintHeardThisCheck = false;

        if (_playerIsHiding || playerTransform == null) return;
        if (_state == SorterState.Attacking) return;

        float playerSpeed = _playerMovement != null ? _playerMovement.GetCurrentSpeed() : 0f;

        // Under crouchThreshold = stille — nulstil vedvarende-lyd-timer
        if (playerSpeed < crouchThreshold)
        {
            _continuousSoundTimer = 0f;
            return;
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        bool isSprinting = playerSpeed >= sprintThreshold;
        float hearingRange = isSprinting ? hearingRangeSprint : hearingRangeWalk;

        if (dist > hearingRange)
        {
            _continuousSoundTimer = 0f;
            return;
        }

        // Lyd hørt
        _lastHeardPosition = playerTransform.position;
        _soundHeardThisCheck = true;
        _sprintHeardThisCheck = isSprinting;

        // Sprint → jagt med det samme (uanset tilstand)
        if (isSprinting && _state != SorterState.Chasing)
        {
            EnterChase();
            return;
        }

        // Normal gang under patrulje → undersøg
        if (_state == SorterState.Patrolling)
        {
            EnterInvestigate(_lastHeardPosition);
            return;
        }

        // Normal gang under undersøgelse → tæl op mod jagt
        if (_state == SorterState.Investigating)
        {
            _continuousSoundTimer += hearingCheckInterval;
            if (_continuousSoundTimer >= investigateToChaseTime)
            {
                _continuousSoundTimer = 0f;
                EnterChase();
            }
        }
    }

    // ─────────────────────────────────────────────
    //  EKSTERN LYD-TRIGGER (bruges af Rat.cs)
    // ─────────────────────────────────────────────
    public void MakeNoise(Vector3 position, float volume = 1f)
    {
        if (_playerIsHiding || _state == SorterState.Attacking) return;

        float dist = Vector3.Distance(transform.position, position);
        if (dist > hearingRangeSprint * volume) return;

        _lastHeardPosition = position;

        if (volume >= 0.8f) EnterChase();
        else if (_state != SorterState.Chasing) EnterInvestigate(position);
    }

    // ─────────────────────────────────────────────
    //  TILSTANDSSKIFT
    // ─────────────────────────────────────────────
    void EnterPatrol()
    {
        StopBehaviour();
        _state = SorterState.Patrolling;
        _agent.speed = patrolSpeed;
        _agent.isStopped = false;
        _continuousSoundTimer = 0f;
        StartMovementAudio(patrolSound);
        _behaviourCoroutine = StartCoroutine(PatrolCoroutine());
    }

    void EnterInvestigate(Vector3 position)
    {
        StopBehaviour();
        _state = SorterState.Investigating;
        _agent.speed = investigateSpeed;
        _agent.isStopped = false;
        _continuousSoundTimer = 0f;
        PlaySFX(investigateSound);
        StopMovementAudio();
        _behaviourCoroutine = StartCoroutine(InvestigateCoroutine(position));
    }

    void EnterChase()
    {
        if (_state == SorterState.Chasing) return;
        StopBehaviour();
        _state = SorterState.Chasing;
        _agent.speed = chaseSpeed;
        _agent.isStopped = false;
        _continuousSoundTimer = 0f;
        PlaySFX(alertSound);
        StopMovementAudio();
        _behaviourCoroutine = StartCoroutine(ChaseCoroutine());
    }

    void StopBehaviour()
    {
        if (_behaviourCoroutine != null)
        {
            StopCoroutine(_behaviourCoroutine);
            _behaviourCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────
    //  PATROL COROUTINE
    //  Går til hvert waypoint → stopper og spiller idle → næste punkt
    //  Patrol-lyden kører hele vejen igennem inkl. idle-pauser
    // ─────────────────────────────────────────────
    IEnumerator PatrolCoroutine()
    {
        while (true)
        {
            // Vælg næste destination
            Vector3 dest;
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                Transform pt = patrolPoints[_patrolIndex % patrolPoints.Length];
                _patrolIndex++;
                if (pt == null) { yield return null; continue; }
                dest = pt.position;
            }
            else
            {
                // Ingen waypoints — vandre tilfældigt
                Vector3 rand = transform.position + Random.insideUnitSphere * 10f;
                rand.y = transform.position.y;
                if (!NavMesh.SamplePosition(rand, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                { yield return new WaitForSeconds(1f); continue; }
                dest = hit.position;
            }

            _agent.isStopped = false;
            _agent.SetDestination(dest);

            // Gå til destinationen
            float timeout = 20f;
            while (_agent.pathPending || _agent.remainingDistance > 0.3f)
            {
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
                yield return null;
            }

            // Fremme — stop og spil idle (Speed → 0 via animator)
            _agent.isStopped = true;

            float waited = 0f;
            while (waited < patrolWaitTime)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            _agent.isStopped = false;
        }
    }

    // ─────────────────────────────────────────────
    //  INVESTIGATE COROUTINE
    //  1) Gå til positionen
    //  2) Idle et øjeblik
    //  3) Vandre rundt i investigateRadius i investigateDuration sekunder
    //  4) Giv op → patrulje
    //  Angriber hvis spilleren støder ind undervejs
    //  Skifter til chase hvis sprint høres (håndteret af CheckHearing)
    // ─────────────────────────────────────────────
    IEnumerator InvestigateCoroutine(Vector3 targetPos)
    {
        // 1) Gå til lyd-positionen
        _agent.SetDestination(targetPos);

        float timeout = 12f;
        while (_agent.pathPending || _agent.remainingDistance > 0.3f)
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f) break;

            if (PlayerWithinAttackRange())
            {
                yield return StartCoroutine(AttackCoroutine());
                if (_state != SorterState.Investigating) yield break;
            }

            yield return null;
        }

        // 2) Idle ved positionen
        _agent.isStopped = true;

        float idleElapsed = 0f;
        while (idleElapsed < investigateIdleTime)
        {
            idleElapsed += Time.deltaTime;

            if (PlayerWithinAttackRange())
            {
                _agent.isStopped = false;
                yield return StartCoroutine(AttackCoroutine());
                if (_state != SorterState.Investigating) yield break;
                _agent.isStopped = true;
                idleElapsed = 0f;
            }

            yield return null;
        }

        _agent.isStopped = false;

        // 3) Vandre rundt i radius
        float searchElapsed = 0f;
        while (searchElapsed < investigateDuration)
        {
            searchElapsed += Time.deltaTime;

            if (PlayerWithinAttackRange())
            {
                yield return StartCoroutine(AttackCoroutine());
                if (_state != SorterState.Investigating) yield break;
                searchElapsed = 0f; // nulstil søgetimer
            }

            // Sæt nyt vandrepunkt når vi er fremme
            if (!_agent.pathPending && _agent.remainingDistance <= 0.3f)
            {
                Vector3 rand = targetPos + Random.insideUnitSphere * investigateRadius;
                rand.y = targetPos.y;
                if (NavMesh.SamplePosition(rand, out NavMeshHit hit, investigateRadius, NavMesh.AllAreas))
                    _agent.SetDestination(hit.position);
            }

            yield return null;
        }

        // 4) Gav op
        PlaySFX(losePlayerSound);
        EnterPatrol();
    }

    // ─────────────────────────────────────────────
    //  CHASE COROUTINE
    //  Jager spilleren løbende
    //  Skifter til investigate hvis spilleren er stille for længe
    //  Angriber hvis spilleren er inden for rækkevidde
    // ─────────────────────────────────────────────
    IEnumerator ChaseCoroutine()
    {
        float persistenceTimer = chasePersistence;

        while (true)
        {
            // Følg spilleren
            if (!_playerIsHiding && playerTransform != null)
                _agent.SetDestination(playerTransform.position);

            // Nulstil persistence-timer når lyd høres
            if (_soundHeardThisCheck)
                persistenceTimer = chasePersistence;
            else
                persistenceTimer -= Time.deltaTime;

            // Angrib hvis spilleren er inden for rækkevidde
            if (PlayerWithinAttackRange())
            {
                yield return StartCoroutine(AttackCoroutine());
                if (_state != SorterState.Chasing) yield break;
                persistenceTimer = chasePersistence;
            }

            // Spilleren er stille for længe — undersøg sidst kendte position
            if (persistenceTimer <= 0f)
            {
                EnterInvestigate(_lastHeardPosition);
                yield break;
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // ─────────────────────────────────────────────
    //  ATTACK COROUTINE
    //  Blokerer den kaldende coroutine indtil angrebet er afsluttet
    //  Bestemmer næste tilstand baseret på hvad der sker efter angrebet
    // ─────────────────────────────────────────────
    IEnumerator AttackCoroutine()
    {
        if (_attackCooldownTimer > 0f) yield break;

        _state = SorterState.Attacking;
        _agent.isStopped = true;

        // Vend mod spilleren
        if (playerTransform != null)
        {
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // Start attack-animation og lyd
        if (animator != null && !string.IsNullOrEmpty(animParamAttack))
            animator.SetTrigger(animParamAttack);
        PlaySFX(attackSound);

        // Vent til animationen er færdig
        yield return new WaitForSeconds(attackAnimationDuration);

        // Giv spilleren skade
        PlayerMovement pm = playerTransform != null
            ? playerTransform.GetComponent<PlayerMovement>() : null;

        if (pm != null) pm.TakeDamage();

        // Er spilleren død?
        if (pm != null && pm.IsDead)
        {
            PlaySFX(killSound);
            yield return new WaitForSeconds(0.8f);
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerGameOver("Den blinde pakkesorter fandt dig...");
            else
                Debug.LogWarning("[BlindSorter] GameOverManager.Instance er null!");
            yield break;
        }

        // Vent lidt efter angrebet
        yield return new WaitForSeconds(postAttackWait);

        _attackCooldownTimer = attackCooldown;
        _agent.isStopped = false;

        // ── Bestem næste tilstand ──────────────────────────────
        // Sprint hørt → jagt straks
        if (_sprintHeardThisCheck)
        {
            _state = SorterState.Chasing;
            _agent.speed = chaseSpeed;
            yield break;
        }

        // Spilleren er stadig inden for angrebsafstand → fortsæt med at undersøge
        // (næste iteration af den kaldende coroutine vil angribe igen)
        if (PlayerWithinAttackRange())
        {
            _state = SorterState.Investigating;
            _agent.speed = investigateSpeed;
            yield break;
        }

        // Spilleren laver lyd men er løbet lidt væk → undersøg
        if (_soundHeardThisCheck)
        {
            _state = SorterState.Investigating;
            _agent.speed = investigateSpeed;
            yield break;
        }

        // Spilleren er stille og væk → undersøg sidst kendte position
        _state = SorterState.Investigating;
        _agent.speed = investigateSpeed;
    }

    // ─────────────────────────────────────────────
    //  HJÆLPEFUNKTIONER
    // ─────────────────────────────────────────────
    bool PlayerWithinAttackRange()
    {
        if (_playerIsHiding || playerTransform == null) return false;
        return Vector3.Distance(transform.position, playerTransform.position) <= attackRange;
    }

    void FindPatrolPointsByTag()
    {
        if (string.IsNullOrEmpty(patrolPointTag)) return;

        GameObject[] found = GameObject.FindGameObjectsWithTag(patrolPointTag);

        if (found.Length == 0)
        {
            Debug.LogWarning($"[BlindSorter] Ingen GameObjects med tag '{patrolPointTag}' fundet. Skifter til tilfældig vandring.");
            return;
        }

        System.Array.Sort(found, (a, b) =>
            string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        patrolPoints = new Transform[found.Length];
        for (int i = 0; i < found.Length; i++)
            patrolPoints[i] = found[i].transform;

        Debug.Log($"[BlindSorter] Fandt {patrolPoints.Length} patrol points via tag '{patrolPointTag}'.");
    }

    // ─────────────────────────────────────────────
    //  ANIMATOR
    //  Speed-parameteren styrer blend tree:
    //    0        → idle animation
    //    patrolSpeed  → walk animation
    //    chaseSpeed   → run animation
    // ─────────────────────────────────────────────
    void UpdateAnimator()
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(animParamSpeed))
            animator.SetFloat(animParamSpeed, _agent.velocity.magnitude);
    }

    // ─────────────────────────────────────────────
    //  LYD
    // ─────────────────────────────────────────────
    void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

    void StartMovementAudio(AudioClip clip)
    {
        if (movementSource == null || clip == null) return;
        if (movementSource.clip == clip && movementSource.isPlaying) return;
        movementSource.clip = clip;
        movementSource.loop = true;
        movementSource.Play();
    }

    void StopMovementAudio()
    {
        if (movementSource != null && movementSource.isPlaying)
            movementSource.Stop();
    }

    // ─────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────
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