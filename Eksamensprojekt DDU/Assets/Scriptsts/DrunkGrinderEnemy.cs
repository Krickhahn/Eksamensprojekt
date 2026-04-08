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

    // Saw animation
    private float _sawNoiseOffset;        // unikt Perlin-seed per instans
    private float _sawSwingTime;          // akkumuleret tid til swing-kurven
    private float _sawCurrentAngleX;      // glat interpoleret nuværende vinkel X
    private float _sawCurrentAngleZ;      // glat interpoleret nuværende vinkel Z (sideværts svajer)

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

    // Hiding-jagt
    // _sawPlayerHide = true:  fjenden SAV spilleren gemme sig → fortsætter jagt mod kassen
    // _sawPlayerHide = false: spilleren gemte sig uden at blive set → fjenden giver op
    private bool _sawPlayerHide;
    private Vector3 _lastSeenPosition;   // position fjenden jager mod når spilleren er gemt
    private bool _hasLastSeenPos;

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        // Gem krop-visual's lokale start-position så vi kan animere relativt
        if (bodyVisual != null)
            _bodyVisualLocalBase = bodyVisual.localPosition;

        // Unikt Perlin-seed per fjende-instans så de ikke svinger synkront
        _sawNoiseOffset = Random.Range(0f, 100f);
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

        // Synkroniser hiding-state igen i Start – HidingManager er garanteret klar nu
        if (HidingManager.Instance != null)
            _playerIsHiding = HidingManager.Instance.IsPlayerHiding;

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

        // Game over hvis fjenden er tæt nok og:
        // - spilleren er synlig (ikke gemt), ELLER
        // - spilleren er gemt OG fjenden SAV dem gemme sig og er nået frem til kassen
        bool attackCondition = _state == State.Chase && IsWithinAttackRange()
            && (!_playerIsHiding ? CanSeePlayer() : _sawPlayerHide);

        if (attackCondition)
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
        // Mens spilleren er gemt er CanSeePlayer() altid false (mesh + collider skjult).
        // Vi bruger _sawPlayerHide til at huske om fjenden SAV dem gemme sig.
        _lastCanSeePlayer = CanSeePlayer();
        float dist = Vector3.Distance(transform.position, player.position);

        if (_state == State.Search)
        {
            // Start jagt kun hvis spilleren ER synlig og inden for range (aldrig gemt)
            if (!_playerIsHiding && _lastCanSeePlayer && dist <= detectionRange)
                EnterChase();
        }
        else // Chase
        {
            if (_playerIsHiding)
            {
                if (_sawPlayerHide)
                {
                    // Fjenden så spilleren gemme sig → løb til lastSeenPosition (kassen)
                    // Når den ankommer uden at se spilleren: giv op
                    if (_hasLastSeenPos)
                    {
                        float distToBox = Vector3.Distance(transform.position, _lastSeenPosition);
                        if (distToBox <= attackRange)
                        {
                            // Tjek om spilleren stadig er i kassen – game over
                            TriggerGameOverAndPullOut();
                            return;
                        }
                    }
                    // Ellers: bliv i Chase og TickChaseMovement bruger _lastSeenPosition
                }
                else
                {
                    // Spilleren gemte sig uden at blive set → stop jagt
                    EnterSearch();
                }
                return;
            }

            // Spilleren er ikke gemt – normal jagt-logik
            if (_lastCanSeePlayer)
            {
                _lostSightTimer = 0f;
                // Opdater last seen position løbende mens spilleren er synlig
                _lastSeenPosition = player.position;
                _hasLastSeenPos = true;
            }
            else
            {
                _lostSightTimer += Time.deltaTime;
                if (_lostSightTimer >= loseSightTime || dist > loseSightRange)
                    EnterSearch();
            }
        }
    }

    private void EnterSearch()
    {
        _state = State.Search;
        _lostSightTimer = 0f;
        _hasWanderTarget = false;
        _isWaiting = false;
        _sawPlayerHide = false;
        _hasLastSeenPos = false;
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

        // Hvis spilleren er gemt og fjenden så det: naviger mod lastSeenPosition (kassen)
        // Ellers: naviger direkte mod spilleren
        Vector3 destination = (_playerIsHiding && _sawPlayerHide && _hasLastSeenPos)
            ? _lastSeenPosition
            : player.position;

        if (_agent != null && _agent.isActiveAndEnabled)
        {
            _agent.SetDestination(destination);
        }
        else
        {
            Vector3 dir = (destination - transform.position);
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

        // Mens spilleren er gemt er de usynlige – fjenden kan ikke spotte dem via syn
        if (_playerIsHiding) return false;

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
        if (hiding)
        {
            // VIGTIGT: Tjek syn FØR _playerIsHiding sættes til true.
            // CanSeePlayer() returnerer altid false hvis _playerIsHiding allerede er true,
            // så vi skal vide om fjenden kan se spilleren mens de stadig er "synlige".
            bool canSeeRightNow = CanSeePlayer();

            _playerIsHiding = true;

            if (_state == State.Chase && canSeeRightNow)
            {
                // Fjenden SAV spilleren gemme sig – løb mod kassen
                _sawPlayerHide = true;
                _lastSeenPosition = player.position;
                _hasLastSeenPos = true;
            }
            else if (_state == State.Chase)
            {
                // Fjenden jagtede men så dem IKKE gemme sig → stop jagt
                _sawPlayerHide = false;
                EnterSearch();
            }
            // Hvis fjenden er i Search: _playerIsHiding er nu sat,
            // så CanSeePlayer() og TickState() ignorerer spilleren automatisk.
        }
        else
        {
            _playerIsHiding = false;
            _sawPlayerHide = false;
            _hasLastSeenPos = false;
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
    //
    // Bladet spinner altid (spøgelse holder altid saven kørende).
    // Pivot-swingning sker KUN under Chase og varieres med Perlin
    // noise så ingen to svingninger ligner hinanden.
    // Under Search vender pivot blødt tilbage til hvile-positionen.
    // ─────────────────────────────────────────────────────────────
    private void AnimateSaw()
    {
        // Bladet spinner altid – det ville et fuld spøgelse gøre
        if (sawBlade != null)
            sawBlade.Rotate(Vector3.right, sawSpinSpeed * Time.deltaTime, Space.Self);

        if (sawPivot == null) return;

        float targetX, targetZ;

        if (_state == State.Chase)
        {
            // Akkumuler tid til grundlæggende swing-rytme
            _sawSwingTime += Time.deltaTime * sawSwingFreq;

            // Basis frem/tilbage swing via sin
            float baseSwing = Mathf.Sin(_sawSwingTime * Mathf.PI * 2f) * sawSwingAngle;

            // Perlin noise tilføjer variation i amplitude og fase
            // – to separate noise-kanaler for X og Z så de er uafhængige
            float noiseX = Mathf.PerlinNoise(_sawSwingTime * 0.7f + _sawNoiseOffset, 0f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(0f, _sawSwingTime * 0.5f + _sawNoiseOffset + 13.7f) * 2f - 1f;

            // X = primær svingning frem/tilbage + noise-variation
            targetX = baseSwing + noiseX * sawSwingAngle * 0.45f;

            // Z = sideværts svajer – ren noise, ingen base-sin
            //     Giver fornemmelse af at fjenden hugger lidt skævt
            targetZ = noiseZ * sawSwingAngle * 0.30f;
        }
        else
        {
            // Search: saven hviler stille langs siden
            targetX = 0f;
            targetZ = 0f;
        }

        // Glat interpolation mod mål – undgår hakkende spring
        float lerpSpeed = (_state == State.Chase) ? jointSmoothSpeed * 1.2f : jointSmoothSpeed * 0.6f;
        _sawCurrentAngleX = Mathf.Lerp(_sawCurrentAngleX, targetX, Time.deltaTime * lerpSpeed);
        _sawCurrentAngleZ = Mathf.Lerp(_sawCurrentAngleZ, targetZ, Time.deltaTime * lerpSpeed);

        Vector3 euler = sawPivot.localEulerAngles;
        euler.x = _sawCurrentAngleX;
        euler.z = _sawCurrentAngleZ;
        sawPivot.localEulerAngles = euler;
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

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        float half = viewAngle * 0.5f;

        // Vis syn-kegle korrekt fremad (ingen 180° flip)
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * transform.forward;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Gizmos.DrawLine(eye, eye + leftDir * detectionRange);
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
            Gizmos.DrawSphere(p, 0.05f);
        }
    }
#endif
}