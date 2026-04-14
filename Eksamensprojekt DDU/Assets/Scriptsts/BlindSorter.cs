using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// ═══════════════════════════════════════════════════════════════════
//  BlindSorter — blind fjende der reagerer udelukkende på lyd
//
//  TILSTANDE:
//    Patrolling   → går tilfældigt mellem patrol points, intet hørt
//    Investigating → har hørt en lyd, går derhen og søger rundt
//    Chasing      → hører spilleren aktivt, følger spilleren
//    Attacking    → inden for angrebsafstand, spiller attack-animation
//
//  ANIMATOR-PARAMETRE (sæt navnene i Inspector):
//    Speed  (Float)   → 0 = idle, > 0 = blend tree walk/run
//    Attack (Trigger) → udløser attack-animation én gang
//
//  KRÆVER på Player-objektet:
//    • Tag sat til "Player"
//    • PlayerMovement script med public float GetCurrentSpeed()
// ═══════════════════════════════════════════════════════════════════

[RequireComponent(typeof(NavMeshAgent))]
public class BlindSorter : MonoBehaviour
{
    public enum State { Patrolling, Investigating, Chasing, Attacking }
    public State CurrentState { get; private set; } = State.Patrolling;

    // ─────────────────────────────────────────────────────────────
    //  HØRELSE
    // ─────────────────────────────────────────────────────────────
    [Header("Hørelse")]
    [Tooltip("Høreradius når spilleren går normalt")]
    public float hearingWalk = 8f;
    [Tooltip("Høreradius når spilleren løber/sprinter")]
    public float hearingSprint = 20f;
    [Tooltip("Høreradius når spilleren croucher (langsom gang)")]
    public float hearingCrouch = 2f;
    [Tooltip("Spillerhastighed ≥ denne tæller som sprint")]
    public float speedSprint = 5f;
    [Tooltip("Spillerhastighed ≤ denne tæller som crouch")]
    public float speedCrouch = 1.5f;
    [Tooltip("Spillerhastighed under denne = stille, ingen lyd")]
    public float speedSilent = 0.1f;
    [Tooltip("Sekunder med vedvarende gang-lyd under undersøgelse inden fjenden skifter til jagt")]
    public float investigateToChaseTime = 2.5f;

    // ─────────────────────────────────────────────────────────────
    //  BEVÆGELSE
    // ─────────────────────────────────────────────────────────────
    [Header("Bevægelse")]
    public float speedPatrol = 1.8f;
    public float speedInvestigate = 2.5f;
    public float speedChase = 6f;

    // ─────────────────────────────────────────────────────────────
    //  PATROL
    // ─────────────────────────────────────────────────────────────
    [Header("Patrol")]
    [Tooltip("Waypoints fjenden patruljerer imellem. Lad stå tomme for auto-søgning via tag.")]
    public Transform[] patrolPoints;
    [Tooltip("Tag der bruges til automatisk at finde patrol points")]
    public string patrolTag = "PatrolPoint";
    [Tooltip("Sekunder fjenden venter idle ved hvert patrol point")]
    public float patrolWait = 2f;

    // ─────────────────────────────────────────────────────────────
    //  UNDERSØGELSE
    // ─────────────────────────────────────────────────────────────
    [Header("Undersøgelse")]
    [Tooltip("Sekunder fjenden søger rundt inden den giver op")]
    public float investigateDuration = 10f;
    [Tooltip("Radius fjenden vandrer rundt i under undersøgelse")]
    public float investigateRadius = 5f;
    [Tooltip("Sekunder fjenden holder idle ved undersøgelsespunktet inden den vandrer rundt")]
    public float investigateIdlePause = 1.5f;

    // ─────────────────────────────────────────────────────────────
    //  JAGT
    // ─────────────────────────────────────────────────────────────
    [Header("Jagt")]
    [Tooltip("Sekunder fjenden fortsætter jagten efter sidst hørt lyd")]
    public float chaseLinger = 3f;

    // ─────────────────────────────────────────────────────────────
    //  ANGREB
    // ─────────────────────────────────────────────────────────────
    [Header("Angreb")]
    [Tooltip("Afstand hvorfra fjenden angriber")]
    public float attackRange = 1.5f;
    [Tooltip("Sekunder attack-animationen varer — sæt til animationens faktiske længde")]
    public float attackDuration = 1.2f;
    [Tooltip("Kort pause efter angrebet inden fjenden bestemmer næste handling")]
    public float attackPause = 0.4f;
    [Tooltip("Sekunder fra animationen starter til skade og stun sker — sæt til tidspunktet hvor slaget rammer (typisk halvvejs i animationen)")]
    public float attackHitMoment = 0.5f;
    [Tooltip("Antal hit spilleren kan tage inden death")]
    public int hitsToKill = 2;
    [Tooltip("Sekunder uden angreb inden spillerens hit-tæller nulstilles (recovery)")]
    public float damageRecoverTime = 6f;

    // ─────────────────────────────────────────────────────────────
    //  ANIMATION
    // ─────────────────────────────────────────────────────────────
    [Header("Animation")]
    [Tooltip("Animator på fjende-modellen. Finder selv i børn hvis tom.")]
    public Animator anim;
    [Tooltip("Float-parameter til blend tree (0=idle, højere=hurtigere)")]
    public string paramSpeed = "Speed";
    [Tooltip("Trigger-parameter til attack-animation")]
    public string paramAttack = "Attack";
    [Tooltip("Bool-parameter der sættes true når fjenden står stille — bruges til Idle1 animation")]
    public string paramIdle = "IsIdle";
    [Tooltip("Skalerer animationens afspilningshastighed ift. agentens fart. 1 = ingen skalering. Juster hvis walk/run-animation ikke matcher bevægelseshastigheden.")]
    public float animSpeedScale = 1f;

    // ─────────────────────────────────────────────────────────────
    //  LYD
    // ─────────────────────────────────────────────────────────────
    [Header("Lyd — kilder")]
    [Tooltip("AudioSource til fjende-stemmelyde (investigate, alert, give up). Må ikke deles med sfxSource.")]
    public AudioSource voiceSource;
    [Tooltip("AudioSource til angreb og dødslyd")]
    public AudioSource sfxSource;
    [Tooltip("AudioSource til footsteps — bør have Spatial Blend = 1 (3D)")]
    public AudioSource footstepSource;
    [Tooltip("AudioSource til ambient loop-lyd under patrulje")]
    public AudioSource loopSource;

    [Header("Lyd — stemme")]
    [Tooltip("Spilles når fjenden begynder at undersøge")]
    public AudioClip soundInvestigate;
    [Tooltip("Spilles når fjenden begynder at jage")]
    public AudioClip soundAlert;
    [Tooltip("Spilles ved angreb")]
    public AudioClip soundAttack;
    [Tooltip("Spilles når spilleren dør")]
    public AudioClip soundKill;
    [Tooltip("Spilles når fjenden giver op og vender tilbage til patrulje")]
    public AudioClip soundGiveUp;

    [Header("Lyd — patrol loop")]
    [Tooltip("Loop-lyd under patrulje")]
    public AudioClip soundPatrol;

    [Header("Lyd — footsteps")]
    [Tooltip("Footstep-clips til normal gang — vælges tilfældigt")]
    public AudioClip[] footstepsWalk;
    [Tooltip("Footstep-clips til løb/jagt — vælges tilfældigt")]
    public AudioClip[] footstepsRun;
    [Tooltip("Sekunder mellem footstep-lyde ved normal gang")]
    public float footstepIntervalWalk = 0.5f;
    [Tooltip("Sekunder mellem footstep-lyde ved løb")]
    public float footstepIntervalRun = 0.3f;
    [Tooltip("Minimum agent-hastighed før footsteps spiller")]
    public float footstepMinSpeed = 0.3f;
    [Tooltip("Volumen på footstep-lyde (0–1)")]
    [Range(0f, 1f)]
    public float footstepVolume = 0.6f;
    [Tooltip("Sekunder voice-lyden fader ud inden en ny starter")]
    public float voiceFadeTime = 0.15f;

    // ─────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────
    NavMeshAgent _agent;
    Transform _player;
    PlayerMovement _pm;

    bool _playerIsHiding;
    int _playerHitCount;
    int _patrolIndex;

    // Hørelses-resultater — opdateres hver 0.2s af RunHearing()
    bool _heardSound;   // hørbar lyd denne tick (gang eller sprint)
    bool _heardSprint;  // sprint specifikt hørt denne tick
    bool _heardWalk;    // gang (ikke sprint) hørt denne tick
    Vector3 _lastSoundPos; // sidst kendte lydposition

    float _hearingTick;
    float _continuousSoundTimer;
    float _footstepTimer;
    Coroutine _voiceFadeCoroutine;

    // Coroutine-styring
    Coroutine _mainLoop;
    bool _attackInProgress;
    float _attackCooldownTimer;
    float _damageRecoverTimer;

    // ─────────────────────────────────────────────────────────────
    //  START
    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.stoppingDistance = 0.2f;

        // Find spiller og PlayerMovement
        GameObject playerGO = GameObject.FindWithTag("Player");
        if (playerGO != null)
        {
            _player = playerGO.transform;
            _pm = playerGO.GetComponent<PlayerMovement>();
            if (_pm == null)
                _pm = playerGO.GetComponentInChildren<PlayerMovement>();
        }

        if (_player == null)
            Debug.LogError("[BlindSorter] Ingen GameObject med tag 'Player' fundet!");
        if (_pm == null)
            Debug.LogError("[BlindSorter] PlayerMovement ikke fundet på Player — GetCurrentSpeed() kan ikke kaldes!");

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        // Rens null-slots fra Inspector
        if (patrolPoints != null && patrolPoints.Length > 0)
            patrolPoints = System.Array.FindAll(patrolPoints, p => p != null);

        if (patrolPoints == null || patrolPoints.Length == 0)
            FindPatrolPointsByTag();

        // HidingManager
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnHidingChanged;
        StartCoroutine(LateRegisterHiding());

        GoToState_Patrol();
    }

    void OnDestroy()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnHidingChanged;
    }

    IEnumerator LateRegisterHiding()
    {
        yield return null;
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnHidingChanged;
    }

    void OnHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;
        if (hiding && (CurrentState == State.Chasing || CurrentState == State.Attacking))
            GoToState_Investigate(_lastSoundPos);
    }

    // ─────────────────────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (_attackCooldownTimer > 0f)
            _attackCooldownTimer -= Time.deltaTime;

        // Recovery: nulstil hit-tæller hvis der er gået tilstrækkelig tid siden sidst angreb
        if (_damageRecoverTimer > 0f)
        {
            _damageRecoverTimer -= Time.deltaTime;
            if (_damageRecoverTimer <= 0f)
            {
                _playerHitCount = 0;
                if (_pm != null) _pm.ResetDamage();
            }
        }

        // Hørelses-tjek hvert 0.2 sekund
        _hearingTick += Time.deltaTime;
        if (_hearingTick >= 0.2f)
        {
            _hearingTick = 0f;
            RunHearing();
        }

        // Animator speed hvert frame
        if (anim != null && !string.IsNullOrEmpty(paramSpeed))
        {
            float spd = _agent.velocity.magnitude;
            // animSpeedScale justerer afspilningshastigheden så animation matcher bevægelse
            anim.SetFloat(paramSpeed, spd * animSpeedScale);

            // IsIdle bool — true når agenten er stoppet (patrol-wait, investigate-pause osv.)
            if (!string.IsNullOrEmpty(paramIdle))
                anim.SetBool(paramIdle, spd < 0.1f && _agent.isStopped);
        }

        UpdateFootsteps();
    }

    // ─────────────────────────────────────────────────────────────
    //  HØRELSE
    //  Beregner _heardSound, _heardSprint, _lastSoundPos.
    //  Trigrer tilstandsskift for Patrolling og Investigating direkte.
    //  Chasing og Attacking læser flagene fra deres egne loops.
    // ─────────────────────────────────────────────────────────────
    void RunHearing()
    {
        _heardSound = false;
        _heardSprint = false;
        _heardWalk = false;

        // Skjult spiller laver ingen lyd
        if (_playerIsHiding || _player == null || _pm == null) return;

        float pSpeed = _pm.GetCurrentSpeed();

        // Fuldstændig stille — nulstil alt
        if (pSpeed <= speedSilent)
        {
            _continuousSoundTimer = 0f;
            return;
        }

        // Crouch-hastighed: meget lille radius
        float radius;
        if (pSpeed >= speedSprint) radius = hearingSprint;
        else if (pSpeed <= speedCrouch) radius = hearingCrouch;
        else radius = hearingWalk;

        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist > radius)
        {
            _continuousSoundTimer = 0f;
            return;
        }

        // ── Lyd hørt ────────────────────────────────────────────
        _heardSound = true;
        _heardSprint = pSpeed >= speedSprint;
        _heardWalk = !_heardSprint;
        _lastSoundPos = _player.position;

        // Under Attacking: AttackOnce-coroutinen reagerer selv
        if (CurrentState == State.Attacking) return;

        if (_heardSprint)
        {
            // Sprint → jag straks uanset nuværende tilstand
            if (CurrentState != State.Chasing)
                GoToState_Chase();
            return;
        }

        if (CurrentState == State.Patrolling)
        {
            // Gang hørt under patrulje → undersøg
            GoToState_Investigate(_lastSoundPos);
            return;
        }

        if (CurrentState == State.Investigating)
        {
            // Vedvarende gang under undersøgelse → tæl op mod jagt
            _continuousSoundTimer += 0.2f;
            if (_continuousSoundTimer >= investigateToChaseTime)
            {
                _continuousSoundTimer = 0f;
                GoToState_Chase();
            }
        }
        // Chasing: ChaseLoop håndterer selv via _heardSound/_heardSprint
    }

    // ─────────────────────────────────────────────────────────────
    //  EKSTERN LYD (Rat.cs kalder denne når rotten skriger)
    // ─────────────────────────────────────────────────────────────
    public void MakeNoise(Vector3 position, float volume = 1f)
    {
        if (CurrentState == State.Attacking) return;

        float dist = Vector3.Distance(transform.position, position);
        if (dist > hearingSprint * Mathf.Clamp01(volume)) return;

        _lastSoundPos = position;

        if (volume >= 0.8f)
            GoToState_Chase();
        else if (CurrentState != State.Chasing)
            GoToState_Investigate(position);
    }

    // ═════════════════════════════════════════════════════════════
    //  TILSTANDSSKIFT
    //  Hver GoToState: stop gammel loop, sæt tilstand, start ny loop
    // ═════════════════════════════════════════════════════════════

    void GoToState_Patrol()
    {
        StopMainLoop();
        CurrentState = State.Patrolling;
        _agent.speed = speedPatrol;
        _continuousSoundTimer = 0f;
        PlayLoop(soundPatrol);
        _mainLoop = StartCoroutine(PatrolLoop());
    }

    void GoToState_Investigate(Vector3 soundPos)
    {
        StopMainLoop();
        CurrentState = State.Investigating;
        _agent.speed = speedInvestigate;
        _continuousSoundTimer = 0f;
        PlayVoice(soundInvestigate);
        StopLoopAudio();
        _mainLoop = StartCoroutine(InvestigateLoop(soundPos));
    }

    void GoToState_Chase()
    {
        if (CurrentState == State.Chasing) return;
        StopMainLoop();
        CurrentState = State.Chasing;
        _agent.speed = speedChase;
        _continuousSoundTimer = 0f;
        PlayVoice(soundAlert);
        StopLoopAudio();
        _mainLoop = StartCoroutine(ChaseLoop());
    }

    void StopMainLoop()
    {
        if (_mainLoop != null)
        {
            StopCoroutine(_mainLoop);
            _mainLoop = null;
        }
        _agent.isStopped = false;
        _attackInProgress = false;
        SetIdle(false); // sørg for idle-animation aldrig sidder fast
        // Nulstil IKKE _attackCooldownTimer her — den skal stadig løbe
    }

    // ═════════════════════════════════════════════════════════════
    //  PATROL LOOP
    // ═════════════════════════════════════════════════════════════
    IEnumerator PatrolLoop()
    {
        while (true)
        {
            // Vælg destination
            Vector3 dest = PickPatrolDest();
            _agent.isStopped = false;
            _agent.SetDestination(dest);

            // Gå derhen
            yield return WaitArrived(25f);

            // Idle ved waypoint — IsIdle=true → Idle1 animation
            _agent.isStopped = true;
            SetIdle(true);
            yield return new WaitForSeconds(patrolWait);
            SetIdle(false);
            _agent.isStopped = false;
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  INVESTIGATE LOOP
    // ═════════════════════════════════════════════════════════════
    IEnumerator InvestigateLoop(Vector3 center)
    {
        // 1) Gå til lydpositionen
        _agent.isStopped = false;
        _agent.SetDestination(center);
        yield return WaitArrived(12f);

        // 2) Idle-pause ved positionen — IsIdle=true → Idle1 animation
        _agent.isStopped = true;
        SetIdle(true);
        yield return new WaitForSeconds(investigateIdlePause);
        SetIdle(false);
        _agent.isStopped = false;

        // 3) Søg rundt i radius
        float elapsed = 0f;
        while (elapsed < investigateDuration)
        {
            elapsed += Time.deltaTime;

            // Angrib hvis spilleren er inden for rækkevidde og cooldown er udløbet
            if (!_attackInProgress && _attackCooldownTimer <= 0f && !_playerIsHiding && PlayerInRange(attackRange))
            {
                yield return AttackOnce();
                // Efter angrebet: bestem hvad der sker
                if (CurrentState != State.Investigating) yield break;
                // Ellers fortsæt undersøgelse — nulstil timer
                elapsed = 0f;
            }

            // Vælg nyt vandrepunkt når fremme
            if (!_agent.pathPending && _agent.remainingDistance < 0.4f)
            {
                Vector3 wp = RandomNavPoint(center, investigateRadius);
                if (wp != Vector3.zero)
                    _agent.SetDestination(wp);
            }

            yield return null;
        }

        // 4) Gav op
        PlayVoice(soundGiveUp);
        GoToState_Patrol();
    }

    // ═════════════════════════════════════════════════════════════
    //  CHASE LOOP
    // ═════════════════════════════════════════════════════════════
    IEnumerator ChaseLoop()
    {
        float lingerTimer = chaseLinger;

        while (true)
        {
            // Opdater kun destination når lyd høres aktivt
            // Ingen lyd = gå mod sidst kendte position, ikke spillerens aktuelle
            if (_player != null && !_playerIsHiding)
            {
                if (_heardSound)
                    _agent.SetDestination(_player.position);
                else
                    _agent.SetDestination(_lastSoundPos);
            }

            // Sprint nulstiller timer helt — gang bremser den (halv rate) — stille = fuld nedtælling
            if (_heardSprint)
                lingerTimer = chaseLinger;
            else if (_heardWalk)
                lingerTimer -= Time.deltaTime * 0.4f;
            else
                lingerTimer -= Time.deltaTime;

            // Delvis sti (spilleren er ikke navigerbar) → hurtigere opgiven
            if (_agent.pathStatus == NavMeshPathStatus.PathPartial ||
                _agent.pathStatus == NavMeshPathStatus.PathInvalid)
                lingerTimer -= Time.deltaTime * 3f;

            // Angrib hvis inden for rækkevidde og cooldown er udløbet
            if (!_attackInProgress && _attackCooldownTimer <= 0f && !_playerIsHiding && PlayerInRange(attackRange))
            {
                yield return AttackOnce();
                if (CurrentState != State.Chasing) yield break;
                lingerTimer = chaseLinger;
                continue;
            }

            // Spilleren er stille for længe eller ude af rækkevidde → undersøg
            if (lingerTimer <= 0f)
            {
                GoToState_Investigate(_lastSoundPos);
                yield break;
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  ATTACK ONCE
    //  Nested coroutine kaldt fra InvestigateLoop og ChaseLoop.
    //  Spiller animationen præcis én gang.
    //  Sætter næste tilstand efter angrebet.
    // ═════════════════════════════════════════════════════════════
    IEnumerator AttackOnce()
    {
        _attackInProgress = true;
        _agent.isStopped = true;
        CurrentState = State.Attacking;

        // Vend mod spilleren
        if (_player != null)
        {
            Vector3 dir = _player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        // Attack-animation og lyd — ResetTrigger først så køen er tom,
        // derefter SetTrigger præcis én gang. Forhindrer at Unity
        // genafspiller animationen hvis triggeren er akkumuleret i køen.
        if (anim != null && !string.IsNullOrEmpty(paramAttack))
        {
            anim.ResetTrigger(paramAttack);
            anim.SetTrigger(paramAttack);
        }
        PlaySFX(soundAttack);

        // Vent til slagets ramme-tidspunkt
        float hitMoment = Mathf.Clamp(attackHitMoment, 0f, attackDuration);
        yield return new WaitForSeconds(hitMoment);

        // Giv skade og stun præcis her — spilleren låses med det samme
        if (_pm != null) _pm.TakeDamage();
        _playerHitCount++;
        _damageRecoverTimer = damageRecoverTime;

        // Vent resten af animationen færdig
        float remainder = attackDuration - hitMoment;
        if (remainder > 0f)
            yield return new WaitForSeconds(remainder);

        // Spilleren død?
        if (_playerHitCount >= hitsToKill)
        {
            PlaySFX(soundKill);
            yield return new WaitForSeconds(0.8f);
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerGameOver("Den blinde pakkesorter fandt dig...");
            yield break;
        }

        // Pause efter angrebet
        yield return new WaitForSeconds(attackPause);

        _attackInProgress = false;
        _agent.isStopped = false;

        // Sæt cooldown — forhindrer at loopet straks angriber igen
        _attackCooldownTimer = attackPause + 0.3f;

        // ── Næste tilstand ───────────────────────────────────────
        // "Angrib igen"-casen er fjernet — cooldown og loop håndterer det
        if (_heardSprint)
        {
            // Spilleren sprinter → jagt
            GoToState_Chase();
        }
        else if (_heardWalk)
        {
            // Spilleren laver gang-lyd men er ikke ved at sprinte → undersøg
            GoToState_Investigate(_lastSoundPos);
        }
        else
        {
            // Spilleren er stille/crouch → undersøg sidst kendte position
            GoToState_Investigate(_lastSoundPos);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  HJÆLPEMETODER
    // ─────────────────────────────────────────────────────────────

    IEnumerator WaitArrived(float timeout)
    {
        float t = 0f;
        while (t < timeout)
        {
            t += Time.deltaTime;
            if (!_agent.pathPending && _agent.remainingDistance < 0.4f)
                yield break;
            yield return null;
        }
    }

    bool PlayerInRange(float range)
    {
        if (_player == null || _playerIsHiding) return false;
        return Vector3.Distance(transform.position, _player.position) <= range;
    }

    Vector3 PickPatrolDest()
    {
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform pt = patrolPoints[_patrolIndex % patrolPoints.Length];
            _patrolIndex++;
            if (pt != null) return pt.position;
        }
        Vector3 rnd = RandomNavPoint(transform.position, 12f);
        return rnd != Vector3.zero ? rnd : transform.position;
    }

    Vector3 RandomNavPoint(Vector3 origin, float radius)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 c = origin + Random.insideUnitSphere * radius;
            c.y = origin.y;
            if (NavMesh.SamplePosition(c, out NavMeshHit hit, radius, NavMesh.AllAreas))
                return hit.position;
        }
        return Vector3.zero;
    }

    void FindPatrolPointsByTag()
    {
        if (string.IsNullOrEmpty(patrolTag)) return;
        var found = GameObject.FindGameObjectsWithTag(patrolTag);
        if (found.Length == 0)
        {
            Debug.LogWarning($"[BlindSorter] Ingen patrol points med tag '{patrolTag}' — bruger tilfældig vandring.");
            return;
        }
        System.Array.Sort(found, (a, b) =>
            string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        patrolPoints = new Transform[found.Length];
        for (int i = 0; i < found.Length; i++)
            patrolPoints[i] = found[i].transform;
        Debug.Log($"[BlindSorter] Fandt {patrolPoints.Length} patrol points via tag '{patrolTag}'.");
    }

    // ─────────────────────────────────────────────────────────────
    //  LYD
    // ─────────────────────────────────────────────────────────────
    void SetIdle(bool idle)
    {
        if (anim == null || string.IsNullOrEmpty(paramIdle)) return;
        anim.SetBool(paramIdle, idle);
    }

    // Spiller en stemmelyd med fade-out af den forrige så der ikke sker et hårdt cut
    void PlayVoice(AudioClip clip)
    {
        if (voiceSource == null || clip == null) return;
        if (_voiceFadeCoroutine != null) StopCoroutine(_voiceFadeCoroutine);
        _voiceFadeCoroutine = StartCoroutine(FadeAndPlayVoice(clip));
    }

    IEnumerator FadeAndPlayVoice(AudioClip clip)
    {
        // Fade ud hvis der allerede spiller en lyd
        if (voiceSource.isPlaying && voiceFadeTime > 0f)
        {
            float startVol = voiceSource.volume;
            float t = 0f;
            while (t < voiceFadeTime)
            {
                t += Time.deltaTime;
                voiceSource.volume = Mathf.Lerp(startVol, 0f, t / voiceFadeTime);
                yield return null;
            }
            voiceSource.Stop();
        }

        // Spil ny lyd ved fuld volumen
        voiceSource.volume = 1f;
        voiceSource.PlayOneShot(clip);
        _voiceFadeCoroutine = null;
    }

    void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

    void PlayLoop(AudioClip clip)
    {
        if (loopSource == null || clip == null) return;
        if (loopSource.clip == clip && loopSource.isPlaying) return;
        loopSource.clip = clip;
        loopSource.loop = true;
        loopSource.Play();
    }

    void StopLoopAudio()
    {
        if (loopSource != null) loopSource.Stop();
    }

    // Footsteps — affyres baseret på agent-hastighed og en interval-timer
    void UpdateFootsteps()
    {
        if (footstepSource == null) return;

        float spd = _agent.velocity.magnitude;
        if (spd < footstepMinSpeed || _agent.isStopped)
        {
            // Stående stille — nulstil timer så første skridt kommer hurtigt
            _footstepTimer = 0f;
            return;
        }

        bool isRunning = spd >= speedSprint * 0.8f; // lidt under sprint-grænsen for at fange chase-speed
        float interval = isRunning ? footstepIntervalRun : footstepIntervalWalk;

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            _footstepTimer = interval;

            AudioClip[] clips = isRunning ? footstepsRun : footstepsWalk;

            // Brug walk-clips som fallback hvis run-clips ikke er sat
            if ((clips == null || clips.Length == 0) && isRunning)
                clips = footstepsWalk;

            if (clips != null && clips.Length > 0)
            {
                AudioClip step = clips[Random.Range(0, clips.Length)];
                if (step != null)
                    footstepSource.PlayOneShot(step, footstepVolume);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.12f);
        Gizmos.DrawWireSphere(transform.position, hearingWalk);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.08f);
        Gizmos.DrawWireSphere(transform.position, hearingSprint);
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, hearingCrouch);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}