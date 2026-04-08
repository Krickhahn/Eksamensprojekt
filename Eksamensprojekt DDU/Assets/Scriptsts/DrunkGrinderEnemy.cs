using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class DrunkGrinderEnemy : MonoBehaviour
{
    private enum State { Search, Chase }

    // ─────────────────────────────────────────────────────────────
    // Inspector: Rig / Animation Targets
    // ─────────────────────────────────────────────────────────────
    [Header("Rig Transforms (assign in Inspector)")]
    [SerializeField] private Transform leftHip;
    [SerializeField] private Transform rightHip;
    [SerializeField] private Transform leftLowerLeg;
    [SerializeField] private Transform rightLowerLeg;
    [SerializeField] private Transform leftForearm;
    [SerializeField] private Transform rightForearm;

    [Header("Saw Animation")]
    [Tooltip("Root transform der svinger rundsaven frem/tilbage (arm/skulder).")]
    [SerializeField] private Transform sawPivot;
    [Tooltip("Selve rundsavens transform der spinner konstant.")]
    [SerializeField] private Transform sawBlade;
    [Tooltip("Rundsav spin-hastighed (grader/sek).")]
    [SerializeField] private float sawSpinSpeed = 720f;
    [Tooltip("Pivot-swing vinkel (grader) – hvor vidt fjenden svinger rundsaven.")]
    [SerializeField] private float sawSwingAngle = 35f;
    [Tooltip("Swing-frekvens – lavere = langsommere svingning.")]
    [SerializeField] private float sawSwingFreq = 1.4f;

    [Header("Hover / Svæve-animation")]
    [Tooltip("Hvor højt kroppen svæver op og ned (meter).")]
    [SerializeField] private float hoverAmplitude = 0.08f;
    [Tooltip("Frekvens på svæve-bob.")]
    [SerializeField] private float hoverFrequency = 1.1f;
    [Tooltip("Kroppen svæver altid X meter over NavMesh-positionen.")]
    [SerializeField] private float hoverBaseOffset = 0.18f;
    [Tooltip("Selve 'krop'-objektet der løftes (f.eks. den visuelle root child). Lad stå tom for at bruge denne transform.")]
    [SerializeField] private Transform bodyVisual;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Player / GameOver
    // ─────────────────────────────────────────────────────────────
    [Header("Player")]
    [Tooltip("Finder automatisk Player via tag 'Player' hvis tom.")]
    [SerializeField] private Transform player;
    [SerializeField] private float playerSearchInterval = 1.0f;

    [Header("Game Over")]
    [SerializeField] private string gameOverSubtitle = "Du blev savet i stykker...";
    [SerializeField] private Vector3 pullOutOffset = new Vector3(0.6f, 0f, 0.8f);

    // ─────────────────────────────────────────────────────────────
    // Inspector: Movement
    // ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float searchSpeed = 0.8f;
    [SerializeField] private float chaseSpeed = 2.4f;

    [Header("Wander (Search-tilstand)")]
    [Tooltip("Maksimal radius hvori fjenden leder efter et nyt wander-punkt.")]
    [SerializeField] private float wanderRadius = 8f;
    [Tooltip("Minimum afstand til et nyt wander-mål (undgår at stå stille).")]
    [SerializeField] private float wanderMinDist = 2.5f;
    [Tooltip("Sekunder fjenden venter ved et wander-punkt inden den finder et nyt.")]
    [SerializeField] private float wanderWaitTime = 1.8f;
    [Tooltip("Sekunder fjenden bruger på at finde en ny NavMesh-position inden den prøver igen.")]
    [SerializeField] private float wanderRetryInterval = 0.5f;
    [Tooltip("Max afstand fra NavMeshSamplePosition for at acceptere et punkt.")]
    [SerializeField] private float wanderNavSampleDist = 2.0f;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Detection
    // ─────────────────────────────────────────────────────────────
    [Header("Detection (Distance)")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float loseSightRange = 14f;

    [Header("Detection (Vision / FOV / LOS)")]
    [Range(1f, 180f)]
    [SerializeField] private float viewAngle = 95f;
    [Tooltip("Lag-mask til syn-raycast. Sæt til 'Everything' minus Enemy-lag.")]
    [SerializeField] private LayerMask visionMask = ~0;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float playerTargetHeight = 1.2f;

    [Header("Lose Sight Timing")]
    [SerializeField] private float loseSightTime = 2.5f;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Leg/Arm Animation
    // ─────────────────────────────────────────────────────────────
    [Header("Leg Animation")]
    [SerializeField] private float legSwingAngle = 30f;
    [SerializeField] private float searchLegFreq = 1.0f;
    [SerializeField] private float chaseLegFreq = 2.8f;
    [SerializeField] private float lowerLegBend = 20f;
    [SerializeField] private bool invertLegs = false;

    [Header("Arm Animation")]
    [SerializeField] private float armSwingAngle = 20f;
    [SerializeField] private float beerArmAngle = 8f;
    [SerializeField] private bool invertArms = false;

    [Header("Drunk Sway")]
    [SerializeField] private float searchSwayMag = 6f;
    [SerializeField] private float searchSwayFreq = 0.7f;
    [SerializeField] private float chaseSwayMag = 2f;
    [SerializeField] private float chaseSwayFreq = 1.3f;

    [Header("Joint Smoothing")]
    [SerializeField] private float jointSmoothSpeed = 15f;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Audio
    // ─────────────────────────────────────────────────────────────
    [Header("Grinder SFX")]
    [SerializeField] private AudioSource grinderAudio;
    [SerializeField] private float grinderSoundInterval = 6f;

    // ─────────────────────────────────────────────────────────────
    // Runtime state
    // ─────────────────────────────────────────────────────────────
    private State _state = State.Search;
    private NavMeshAgent _agent;

    // Animation
    private float _animTime;
    private float _hoverTime;
    private Vector3 _bodyVisualLocalBase;

    // Wander
    private Vector3 _wanderTarget;
    private bool _hasWanderTarget;
    private float _wanderWaitTimer;
    private float _wanderRetryTimer;
    private bool _isWaiting;

    // Detection & timers
    private float _grinderTimer;
    private float _lostSightTimer;
    private float _playerSearchTimer;
    private bool _playerIsHiding;
    private bool _hasTriggeredGameOver;
    private bool _lastCanSeePlayer;

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        // Gem krop-visual's lokale start-position så vi kan animere relativt
        if (bodyVisual != null)
            _bodyVisualLocalBase = bodyVisual.localPosition;
    }

    private void OnEnable()
    {
        if (HidingManager.Instance != null)
        {
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
            _playerIsHiding = HidingManager.Instance.IsPlayerHiding;
        }
    }

    private void OnDisable()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    private void Start()
    {
        TryFindPlayer(true);

        // Sæt NavMesh-agent til ikke at rotere selv – vi styrer rotation manuelt
        if (_agent != null)
            _agent.updateRotation = false;
    }

    private void Update()
    {
        if (_hasTriggeredGameOver || Time.timeScale == 0f) return;

        TryFindPlayer(false);
        if (player == null) return;

        TickState();
        TickMovement();
        AnimateBody();
        AnimateSaw();
        AnimateHover();
        TickGrinderSound();

        if (_state == State.Chase && IsWithinAttackRange() && CanSeePlayer())
            TriggerGameOverAndPullOut();
    }

    // ─────────────────────────────────────────────────────────────
    // Player auto-find
    // ─────────────────────────────────────────────────────────────
    private void TryFindPlayer(bool force)
    {
        if (player != null) return;

        if (!force)
        {
            _playerSearchTimer -= Time.deltaTime;
            if (_playerSearchTimer > 0f) return;
        }

        _playerSearchTimer = playerSearchInterval;
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;
    }

    // ─────────────────────────────────────────────────────────────
    // State machine
    // ─────────────────────────────────────────────────────────────
    private void TickState()
    {
        _lastCanSeePlayer = CanSeePlayer();
        float dist = Vector3.Distance(transform.position, player.position);

        if (_state == State.Search)
        {
            // Spilleren skal BÅDE være inden for range OG i sigte for at starte jagt
            if (_lastCanSeePlayer && dist <= detectionRange)
                EnterChase();
        }
        else // Chase
        {
            if (_playerIsHiding)
            {
                if (_lastCanSeePlayer)
                    TriggerGameOverAndPullOut();
                else
                    EnterSearch();
                return;
            }

            if (!_lastCanSeePlayer)
            {
                _lostSightTimer += Time.deltaTime;
                if (_lostSightTimer >= loseSightTime || dist > loseSightRange)
                    EnterSearch();
            }
            else
            {
                _lostSightTimer = 0f;
            }
        }
    }

    private void EnterSearch()
    {
        _state = State.Search;
        _lostSightTimer = 0f;
        _hasWanderTarget = false;
        _isWaiting = false;
        if (_agent != null && _agent.isActiveAndEnabled)
            _agent.ResetPath();
    }

    private void EnterChase()
    {
        _state = State.Chase;
        _lostSightTimer = 0f;
        _hasWanderTarget = false;
        _isWaiting = false;
    }

    // ─────────────────────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────────────────────
    private void TickMovement()
    {
        if (IsWithinAttackRange()) return;

        if (_state == State.Chase)
            TickChaseMovement();
        else
            TickWanderMovement();
    }

    private void TickChaseMovement()
    {
        SetAgentSpeed(chaseSpeed);
        if (_agent != null && _agent.isActiveAndEnabled)
        {
            _agent.SetDestination(player.position);
        }
        else
        {
            // Fallback uden NavMesh
            Vector3 dir = (player.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                transform.position += dir * chaseSpeed * Time.deltaTime;
                FaceDirection(dir, chaseSpeed);
            }
        }

        if (_agent != null)
            FaceAgentVelocity(chaseSpeed);
    }

    private void TickWanderMovement()
    {
        SetAgentSpeed(searchSpeed);

        // Vent ved destinationen inden ny søges
        if (_isWaiting)
        {
            _wanderWaitTimer -= Time.deltaTime;
            if (_wanderWaitTimer <= 0f)
            {
                _isWaiting = false;
                _hasWanderTarget = false;
            }
            return;
        }

        // Tjek om vi har nået målet
        if (_hasWanderTarget && _agent != null && _agent.isActiveAndEnabled)
        {
            bool arrived = !_agent.pathPending
                        && _agent.remainingDistance <= _agent.stoppingDistance + 0.15f;

            if (arrived)
            {
                _isWaiting = true;
                _wanderWaitTimer = wanderWaitTime;
                return;
            }
        }

        // Prøv at finde et nyt punkt
        if (!_hasWanderTarget)
        {
            _wanderRetryTimer -= Time.deltaTime;
            if (_wanderRetryTimer > 0f) return;

            _wanderRetryTimer = wanderRetryInterval;

            if (TryGetWanderPoint(out Vector3 point))
            {
                _wanderTarget = point;
                _hasWanderTarget = true;

                if (_agent != null && _agent.isActiveAndEnabled)
                    _agent.SetDestination(_wanderTarget);
            }
        }

        if (_agent != null)
            FaceAgentVelocity(searchSpeed);
        else if (_hasWanderTarget)
        {
            // Fallback uden NavMesh
            Vector3 dir = (_wanderTarget - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.2f) { _hasWanderTarget = false; return; }
            dir.Normalize();
            transform.position += dir * searchSpeed * Time.deltaTime;
            FaceDirection(dir, searchSpeed);
        }
    }

    /// <summary>
    /// Finder et tilfældigt punkt på NavMesh inden for wanderRadius.
    /// Returnerer false hvis intet punkt kan samplet.
    /// </summary>
    private bool TryGetWanderPoint(out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            // Tilfældig retning + afstand
            Vector2 rand2D = Random.insideUnitCircle.normalized * Random.Range(wanderMinDist, wanderRadius);
            Vector3 candidate = transform.position + new Vector3(rand2D.x, 0f, rand2D.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, wanderNavSampleDist, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        result = transform.position;
        return false;
    }

    private void SetAgentSpeed(float speed)
    {
        if (_agent != null && _agent.isActiveAndEnabled)
            _agent.speed = speed;
    }

    /// <summary>Roter fjenden mod NavMesh-agentens faktiske hastighed.</summary>
    private void FaceAgentVelocity(float speed)
    {
        if (_agent == null) return;
        Vector3 vel = _agent.velocity;
        vel.y = 0f;
        if (vel.sqrMagnitude > 0.04f)
            FaceDirection(vel.normalized, speed);
    }

    private void FaceDirection(Vector3 dir, float speed)
    {
        // OBS: Ingen ekstra 180° her – modellen er sat op korrekt i Prefab.
        //      Hvis modellen peger bagud i Unity, skift til Quaternion.Euler(0,180,0) multiplikation.
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * speed * 2f);
    }

    private bool IsWithinAttackRange()
        => Vector3.Distance(transform.position, player.position) <= attackRange;

    // ─────────────────────────────────────────────────────────────
    // Vision / Detection
    //
    // FAST FIXED: transform.forward er SYNETS retning (ingen 180°
    // flip her – kun bevægelses-rotationen bruger det).
    // ─────────────────────────────────────────────────────────────
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = player.position + Vector3.up * playerTargetHeight;

        Vector3 toTarget = targetPos - eyePos;
        float dist = toTarget.magnitude;

        if (dist > loseSightRange) return false;

        // Vinkelcheck mod transform.forward (ikke inverteret)
        float angle = Vector3.Angle(transform.forward, toTarget / dist);
        if (angle > viewAngle * 0.5f) return false;

        // Line-of-sight raycast
        if (Physics.Raycast(eyePos, toTarget / dist, out RaycastHit hit, dist, visionMask, QueryTriggerInteraction.Ignore))
            return hit.collider.CompareTag("Player");

        // Intet ramte – direkte synslinje
        return true;
    }

    // ─────────────────────────────────────────────────────────────
    // Hiding
    // ─────────────────────────────────────────────────────────────
    private void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;
        if (_state != State.Chase || _hasTriggeredGameOver) return;

        if (hiding)
        {
            if (CanSeePlayer()) TriggerGameOverAndPullOut();
            else EnterSearch();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Game Over
    // ─────────────────────────────────────────────────────────────
    private void TriggerGameOverAndPullOut()
    {
        if (_hasTriggeredGameOver) return;
        _hasTriggeredGameOver = true;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null) pm.SetHiding(false);

        if (HidingManager.Instance != null)
            HidingManager.Instance.SetPlayerHiding(false);

        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        Vector3 pullPos = transform.TransformPoint(pullOutOffset);
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            player.position = pullPos;
            cc.enabled = true;
        }
        else player.position = pullPos;

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.TriggerGameOver(gameOverSubtitle);
        else
            Time.timeScale = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    // Animation – Hovering / svæve-animation
    // ─────────────────────────────────────────────────────────────
    private void AnimateHover()
    {
        _hoverTime += Time.deltaTime * hoverFrequency;

        float bobY = Mathf.Sin(_hoverTime * Mathf.PI * 2f) * hoverAmplitude + hoverBaseOffset;

        // Tilt let frem/tilbage for spøgelse-look
        float tiltX = Mathf.Sin(_hoverTime * Mathf.PI * 2f * 0.5f) * 2.5f;

        if (bodyVisual != null)
        {
            bodyVisual.localPosition = _bodyVisualLocalBase + new Vector3(0f, bobY, 0f);
            // Mild pitch-tilt for svæve-fornemmelse
            Vector3 euler = bodyVisual.localEulerAngles;
            euler.x = tiltX;
            bodyVisual.localEulerAngles = euler;
        }
        else
        {
            // Animer selve transform.position.y (virker kun uden NavMesh height-correction)
            // Anbefalet: brug bodyVisual child i stedet
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Animation – Ben og arme
    // ─────────────────────────────────────────────────────────────
    private void AnimateBody()
    {
        float freq = (_state == State.Chase) ? chaseLegFreq : searchLegFreq;
        float swayMag = (_state == State.Chase) ? chaseSwayMag : searchSwayMag;
        float swayFreq = (_state == State.Chase) ? chaseSwayFreq : searchSwayFreq;

        _animTime += Time.deltaTime * freq;
        float t = Mathf.Sin(_animTime);
        float sway = Mathf.Sin(Time.time * swayFreq) * swayMag;

        float legDir = invertLegs ? -1f : 1f;
        float armDir = invertArms ? -1f : 1f;

        // Ben – pendel-bevægelse (benene holdes stille i luft = ingen "gang" stepping)
        float leftHipX = -90f + legDir * t * legSwingAngle + sway;
        float rightHipX = -90f + legDir * -t * legSwingAngle + sway;
        float lowerX = -90f + lowerLegBend; // let bøjet, statisk

        // Arme
        float rightArmX = -90f + armDir * t * armSwingAngle;
        float leftArmX = -90f + armDir * -t * beerArmAngle;

        SetXRotation(leftHip, leftHipX);
        SetXRotation(rightHip, rightHipX);
        SetXRotation(leftLowerLeg, lowerX);
        SetXRotation(rightLowerLeg, lowerX);
        SetXRotation(leftForearm, leftArmX);
        SetXRotation(rightForearm, rightArmX);
    }

    // ─────────────────────────────────────────────────────────────
    // Animation – Rundsav
    // ─────────────────────────────────────────────────────────────
    private void AnimateSaw()
    {
        // Spin selve bladet
        if (sawBlade != null)
            sawBlade.Rotate(Vector3.right, sawSpinSpeed * Time.deltaTime, Space.Self);

        // Sving pivot'en frem og tilbage
        if (sawPivot != null)
        {
            // Hurtigere/mere aggressiv svingning under jagt
            float freq = (_state == State.Chase) ? sawSwingFreq * 1.6f : sawSwingFreq;
            float amplitude = (_state == State.Chase) ? sawSwingAngle * 1.3f : sawSwingAngle;

            float swingAngle = Mathf.Sin(Time.time * freq * Mathf.PI * 2f) * amplitude;

            Vector3 euler = sawPivot.localEulerAngles;
            // Sving i X-aksen (frem/tilbage) – juster til din rigs akse
            euler.x = swingAngle;
            sawPivot.localEulerAngles = euler;
        }
    }

    private void SetXRotation(Transform t, float xDeg)
    {
        if (t == null) return;
        Quaternion target = Quaternion.Euler(xDeg, t.localEulerAngles.y, t.localEulerAngles.z);
        t.localRotation = Quaternion.Lerp(t.localRotation, target, Time.deltaTime * jointSmoothSpeed);
    }

    // ─────────────────────────────────────────────────────────────
    // Audio
    // ─────────────────────────────────────────────────────────────
    private void TickGrinderSound()
    {
        if (grinderAudio == null) return;

        _grinderTimer -= Time.deltaTime;
        if (_grinderTimer <= 0f)
        {
            grinderAudio.Play();
            _grinderTimer = (_state == State.Chase)
                ? Random.Range(1.8f, 3.6f)
                : Random.Range(grinderSoundInterval * 0.7f, grinderSoundInterval * 1.3f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseSightRange);

        Vector3 eye  = transform.position + Vector3.up * eyeHeight;
        float   half = viewAngle * 0.5f;

        // Vis syn-kegle korrekt fremad (ingen 180° flip)
        Vector3 leftDir  = Quaternion.AngleAxis(-half, Vector3.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis( half, Vector3.up) * transform.forward;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Gizmos.DrawLine(eye, eye + leftDir  * detectionRange);
        Gizmos.DrawLine(eye, eye + rightDir * detectionRange);

        // Wander-mål
        if (_hasWanderTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_wanderTarget, 0.2f);
            Gizmos.DrawLine(transform.position, _wanderTarget);
        }

        if (Application.isPlaying && player != null)
        {
            Vector3 p = player.position + Vector3.up * playerTargetHeight;
            Gizmos.color = _lastCanSeePlayer ? Color.green : Color.magenta;
            Gizmos.DrawLine(eye, p);
            Gizmos.DrawSphere(eye, 0.05f);
            Gizmos.DrawSphere(p,   0.05f);
        }
    }
#endif
}