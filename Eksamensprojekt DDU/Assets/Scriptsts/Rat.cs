using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Rat : MonoBehaviour
{
    public enum RatState { Idle, Fleeing, Hiding, Sabotage }

    [Header("Flugt")]
    [Tooltip("Afstand til spilleren der udløser flugt")]
    public float fleeTriggerRadius = 5f;
    [Tooltip("Afstand rotten løber væk fra spilleren")]
    public float fleeDistance = 12f;
    [Tooltip("Hvor langt rotten søger efter et skjulested fra sin nuværende position")]
    public float hideSearchRadius = 18f;

    [Header("Bevægelse")]
    public float idleSpeed = 1.2f;
    public float fleeSpeed = 5f;
    public float idleWanderRadius = 6f;
    public float idleWaitTime = 2f;

    [Header("Genoptag")]
    [Tooltip("Afstand til spilleren hvor rotten tør komme ud igen")]
    public float resumeRadius = 15f;
    [Tooltip("Sekunder rotten venter skjult inden den tjekker om den kan gå ud")]
    public float hideCheckInterval = 2f;

    [Header("Lyd")]
    public AudioSource sfxSource;
    public AudioClip squeakClip;

    [Header("BlindSorter integration")]
    [Tooltip("Lydstyrke sendt til BlindSorter når rotten piber (0–1)")]
    [Range(0f, 1f)]
    public float squeakNoiseVolume = 0.6f;
    [Tooltip("Interval i sekunder mellem opslag efter BlindSorter i scenen")]
    public float blindSorterSearchInterval = 2f;

    [Header("Pakkesabotage")]
    [Tooltip("Afstand rotten holder sig inden for når den bevæger sig mod pakken.")]
    public float packageApproachRadius = 2f;
    [Tooltip("Afstand spilleren skal have til pakken for at rotten ikke gider løbe mod den.\n" +
             "Hvis spilleren er tættere end dette på pakken, flygter rotten i stedet.")]
    public float playerNearPackageRadius = 4f;
    [Tooltip("Hvor tit rotten tjekker om den skal gå mod pakken (sekunder).")]
    public float packageCheckInterval = 3f;

    // ── Private state ─────────────────────────────────────────────
    private RatState _state = RatState.Idle;
    private NavMeshAgent _agent;
    private Coroutine _behaviourCoroutine;

    private Transform _playerTransform;
    private BlindSorter _blindSorter;

    public RatState CurrentState => _state;

    // ── Init ──────────────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = idleSpeed;

        FindPlayer();
        StartCoroutine(FindBlindSorterLoop());

        _behaviourCoroutine = StartCoroutine(IdleCoroutine());
        StartCoroutine(PackageSabotageLoop());
    }

    // ── Dynamisk opslag ───────────────────────────────────────────
    void FindPlayer()
    {
        var p = GameObject.FindWithTag("Player");
        if (p != null) _playerTransform = p.transform;
    }

    /// <summary>
    /// Kører hele tiden i baggrunden og opsamler BlindSorter
    /// så snart den dukker op i scenen — og glemmer den igen hvis den forsvinder.
    /// </summary>
    IEnumerator FindBlindSorterLoop()
    {
        while (true)
        {
            // Tjek om den gemte reference stadig er i live
            if (_blindSorter == null)
                _blindSorter = FindFirstObjectByType<BlindSorter>();

            yield return new WaitForSeconds(blindSorterSearchInterval);
        }
    }

    // ── Update ────────────────────────────────────────────────────
    void Update()
    {
        if (_playerTransform == null)
        {
            FindPlayer();
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

        if (distToPlayer <= fleeTriggerRadius && _state != RatState.Fleeing)
            EnterFlee();
    }

    // ── Pakkesabotage-loop ────────────────────────────────────────
    /// <summary>
    /// Kører i baggrunden og bestemmer om rotten skal gå mod spillerens aktive pakke.
    /// Rotten bevæger sig kun mod pakken hvis:
    ///   1. Der er en aktiv ordre med en pakke.
    ///   2. Spilleren ikke er tæt på pakken.
    ///   3. Rotten ikke allerede er ved at flygte eller gemme sig.
    /// </summary>
    IEnumerator PackageSabotageLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(packageCheckInterval);

            // Undlad at sabotere hvis rotten flygter eller gemmer sig
            if (_state == RatState.Fleeing || _state == RatState.Hiding)
                continue;

            Transform packageTarget = GetActivePackageTransform();

            if (packageTarget == null)
            {
                // Ingen aktiv ordre — gå tilbage til idle hvis rotten saboterede
                if (_state == RatState.Sabotage)
                    EnterIdle();
                continue;
            }

            // Tjek om spilleren allerede er tæt på pakken
            float playerDistToPkg = _playerTransform != null
                ? Vector3.Distance(_playerTransform.position, packageTarget.position)
                : float.MaxValue;

            if (playerDistToPkg <= playerNearPackageRadius)
            {
                // Spilleren er tæt på pakken — sabotage er ikke relevant
                if (_state == RatState.Sabotage)
                    EnterIdle();
                continue;
            }

            // Bevæg rotten mod pakken
            EnterSabotage(packageTarget.position);
        }
    }

    /// <summary>
    /// Henter transform på den aktive ordres målpakke via OrderManager.
    /// Returnerer null hvis der ingen aktiv ordre er.
    /// </summary>
    Transform GetActivePackageTransform()
    {
        if (OrderManager.Instance == null) return null;

        Order order = OrderManager.Instance.CurrentOrder;
        if (order == null || order.targetPackage == null) return null;

        return order.targetPackage.transform;
    }

    // ── Tilstandsskift ────────────────────────────────────────────
    void EnterFlee()
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = RatState.Fleeing;
        _agent.speed = fleeSpeed;
        _agent.isStopped = false;

        Squeak();
        _behaviourCoroutine = StartCoroutine(FleeCoroutine());
    }

    void EnterHide(Vector3 hideSpot)
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = RatState.Hiding;
        _agent.speed = fleeSpeed;
        _agent.isStopped = false;
        _agent.SetDestination(hideSpot);

        _behaviourCoroutine = StartCoroutine(HideCoroutine());
    }

    void EnterSabotage(Vector3 packagePosition)
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = RatState.Sabotage;
        _agent.speed = idleSpeed; // Rotten slentre roligt mod pakken — ikke fuld flugtfart
        _agent.isStopped = false;

        if (NavMesh.SamplePosition(packagePosition, out NavMeshHit hit, packageApproachRadius * 2f, NavMesh.AllAreas))
            _agent.SetDestination(hit.position);

        _behaviourCoroutine = StartCoroutine(SabotageCoroutine(packagePosition));
    }

    IEnumerator SabotageCoroutine(Vector3 packagePosition)
    {
        float timeout = 15f;
        while (_state == RatState.Sabotage &&
               (_agent.pathPending || _agent.remainingDistance > packageApproachRadius))
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f) break;
            yield return null;
        }

        // Rotten nåede pakken — vent og snuser til den
        if (_state == RatState.Sabotage)
        {
            _agent.isStopped = true;
            yield return new WaitForSeconds(idleWaitTime);
            _agent.isStopped = false;
        }

        if (_state == RatState.Sabotage)
            EnterIdle();
    }

    void EnterIdle()
    {
        if (_behaviourCoroutine != null) StopCoroutine(_behaviourCoroutine);

        _state = RatState.Idle;
        _agent.speed = idleSpeed;
        _agent.isStopped = false;

        _behaviourCoroutine = StartCoroutine(IdleCoroutine());
    }

    // ── Coroutines ────────────────────────────────────────────────
    IEnumerator IdleCoroutine()
    {
        while (_state == RatState.Idle)
        {
            Vector3 rand = transform.position + Random.insideUnitSphere * idleWanderRadius;
            rand.y = transform.position.y;

            if (NavMesh.SamplePosition(rand, out NavMeshHit hit, idleWanderRadius, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);

            float timeout = 10f;
            while (_state == RatState.Idle &&
                   (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance))
            {
                timeout -= Time.deltaTime;
                if (timeout <= 0f) break;
                yield return null;
            }

            if (_state == RatState.Idle)
                yield return new WaitForSeconds(idleWaitTime);
        }
    }

    IEnumerator FleeCoroutine()
    {
        Vector3 fleeDir = (transform.position - _playerTransform.position).normalized;
        Vector3 fleeTarget = transform.position + fleeDir * fleeDistance;

        if (NavMesh.SamplePosition(fleeTarget, out NavMeshHit fleeHit, fleeDistance, NavMesh.AllAreas))
            _agent.SetDestination(fleeHit.position);

        float timeout = 8f;
        while (_state == RatState.Fleeing &&
               (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance))
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f) break;
            yield return null;
        }

        Vector3 hideSpot = FindHideSpot();
        if (hideSpot != Vector3.zero)
            EnterHide(hideSpot);
        else
            EnterIdle();
    }

    IEnumerator HideCoroutine()
    {
        float timeout = 10f;
        while (_state == RatState.Hiding &&
               (_agent.pathPending || _agent.remainingDistance > _agent.stoppingDistance))
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f) break;
            yield return null;
        }

        _agent.isStopped = true;

        while (_state == RatState.Hiding)
        {
            yield return new WaitForSeconds(hideCheckInterval);

            if (_playerTransform == null) break;

            float dist = Vector3.Distance(transform.position, _playerTransform.position);

            if (dist <= fleeTriggerRadius)
            {
                // Spilleren er stadig for tæt på — flygt igen i stedet for at fryse
                EnterFlee();
                yield break;
            }

            if (dist >= resumeRadius)
                break;
        }

        if (_state == RatState.Hiding)
            EnterIdle();
    }

    // ── Hjælpefunktioner ─────────────────────────────────────────
    Vector3 FindHideSpot()
    {
        Vector3 best = Vector3.zero;
        float bestDist = 0f;

        for (int i = 0; i < 10; i++)
        {
            Vector3 rand = transform.position + Random.insideUnitSphere * hideSearchRadius;
            rand.y = transform.position.y;

            if (!NavMesh.SamplePosition(rand, out NavMeshHit hit, hideSearchRadius, NavMesh.AllAreas))
                continue;

            float d = Vector3.Distance(hit.position, _playerTransform.position);
            if (d > bestDist)
            {
                bestDist = d;
                best = hit.position;
            }
        }

        return best;
    }

    void Squeak()
    {
        if (sfxSource != null && squeakClip != null)
            sfxSource.PlayOneShot(squeakClip);

        // Send støj til BlindSorter hvis den findes i scenen
        if (_blindSorter != null)
            _blindSorter.MakeNoise(transform.position, squeakNoiseVolume);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, fleeTriggerRadius);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, fleeDistance);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, hideSearchRadius);
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, resumeRadius);
        Gizmos.color = new Color(1f, 0f, 0.5f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, playerNearPackageRadius);
    }
#endif
}