using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class DrunkGrinderEnemy : MonoBehaviour
{
    private enum State
    {
        Search,
        Chase
    }

    // ─────────────────────────────────────────────────────────────
    // Inspector: Rig/Animation Targets
    // ─────────────────────────────────────────────────────────────
    [Header("Rig Transforms (assign in Inspector)")]
    [SerializeField] private Transform leftHip;
    [SerializeField] private Transform rightHip;
    [SerializeField] private Transform leftLowerLeg;
    [SerializeField] private Transform rightLowerLeg;
    [SerializeField] private Transform leftForearm;
    [SerializeField] private Transform rightForearm;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Player / GameOver
    // ─────────────────────────────────────────────────────────────
    [Header("Player")]
    [Tooltip("Hvis tom, finder fjenden automatisk Player via tag 'Player'.")]
    [SerializeField] private Transform player;

    [Tooltip("Hvor ofte fjenden forsøger at finde Player hvis reference mangler.")]
    [SerializeField] private float playerSearchInterval = 1.0f;

    [Header("Game Over")]
    [SerializeField] private string gameOverSubtitle = "Du blev savet i stykker...";
    [Tooltip("Hvis spilleren bliver set gemme sig, flyttes spilleren til dette offset fra fjenden (lokal space).")]
    [SerializeField] private Vector3 pullOutOffset = new Vector3(0.6f, 0f, 0.8f);

    // ─────────────────────────────────────────────────────────────
    // Inspector: Movement
    // ─────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float searchSpeed = 0.8f;
    [SerializeField] private float chaseSpeed = 2.4f;

    [Tooltip("Hvor langt fjenden bevæger sig frem mod et wander-mål i Search state.")]
    [SerializeField] private float wanderStep = 2.0f;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Detection
    // ─────────────────────────────────────────────────────────────
    [Header("Detection (Distance)")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float loseSightRange = 14f;

    [Header("Detection (Vision / FOV / LOS)")]
    [Range(1f, 180f)] [SerializeField] private float viewAngle = 95f;

    [Tooltip("Raycast-lag for syn (Everything er fint; fjern evt. Enemy-lag).")]
    [SerializeField] private LayerMask visionMask = ~0;

    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float playerTargetHeight = 1.2f;

    [Header("Lose Sight Timing")]
    [SerializeField] private float loseSightTime = 2.5f;

    // ─────────────────────────────────────────────────────────────
    // Inspector: Animation
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
    // Runtime State
    // ─────────────────────────────────────────────────────────────
    private State _state = State.Search;
    private NavMeshAgent _agent;

    private float _animTime;
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
    }

    private void OnEnable()
    {
        if (HidingManager.Instance != null)
        {
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
            _playerIsHiding = HidingManager.Instance.IsPlayerHiding;
        }
    }

    private void Start()
    {
        TryFindPlayer(true);
    }

    private void OnDisable()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    private void Update()
    {
        if (_hasTriggeredGameOver || Time.timeScale == 0f) return;

        TryFindPlayer(false);
        if (player == null) return;

        TickState();
        AnimateBody();
        TickMovement();
        TickGrinderSound();

        if (_state == State.Chase && IsWithinAttackRange() && CanSeePlayer())
            TriggerGameOverAndPullOut();
    }

    // ─────────────────────────────────────────────────────────────
    // Player detection / auto-find
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
    // State Logic
    // ─────────────────────────────────────────────────────────────
    private void TickState()
    {
        _lastCanSeePlayer = CanSeePlayer();
        float dist = Vector3.Distance(transform.position, player.position);

        if (_state == State.Search)
        {
            if (_lastCanSeePlayer && dist <= detectionRange)
                EnterChase();
        }
        else
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
        if (_agent != null) _agent.ResetPath();
    }

    private void EnterChase()
    {
        _state = State.Chase;
        _lostSightTimer = 0f;
    }

    // ─────────────────────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────────────────────
    private void TickMovement()
    {
        if (player == null) return;
        if (IsWithinAttackRange()) return;

        float speed = (_state == State.Chase) ? chaseSpeed : searchSpeed;

        if (_agent != null && _agent.enabled)
        {
            _agent.speed = speed;

            if (_state == State.Chase)
            {
                _agent.SetDestination(player.position);
            }
            else
            {
                float angle = Mathf.Sin(Time.time * 0.3f) * 45f;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                dir.y = 0f;
                _agent.SetDestination(transform.position + dir.normalized * wanderStep);
            }

            Vector3 vel = _agent.velocity; vel.y = 0f;
            if (vel.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(vel.normalized) * Quaternion.Euler(0f, 180f, 0f);
                transform.rotation = Quaternion.Lerp(transform.rotation, look, Time.deltaTime * (speed * 1.5f));
            }

            return;
        }

        Vector3 fallbackDir;

        if (_state == State.Chase)
            fallbackDir = (player.position - transform.position).normalized;
        else
            fallbackDir = Quaternion.Euler(0, Mathf.Sin(Time.time * 0.3f) * 45f, 0) * transform.forward;

        fallbackDir.y = 0f;

        transform.position += fallbackDir * speed * Time.deltaTime;

        if (fallbackDir.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(fallbackDir) * Quaternion.Euler(0f, 180f, 0f);
            transform.rotation = Quaternion.Lerp(transform.rotation, look, Time.deltaTime * (speed * 1.5f));
        }
    }

    private bool IsWithinAttackRange()
    {
        float dist = Vector3.Distance(transform.position, player.position);
        return dist <= attackRange;
    }

    // ─────────────────────────────────────────────────────────────
    // Vision
    // ─────────────────────────────────────────────────────────────
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = player.position + Vector3.up * playerTargetHeight;

        Vector3 toTarget = targetPos - eyePos;
        float dist = toTarget.magnitude;
        if (dist > loseSightRange) return false;

        Vector3 dir = toTarget / dist;

        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, dist, visionMask, QueryTriggerInteraction.Ignore))
            return hit.collider.CompareTag("Player");

        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // Hiding handling
    // ─────────────────────────────────────────────────────────────
    private void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;

        if (_state != State.Chase || _hasTriggeredGameOver) return;

        if (hiding)
        {
            if (CanSeePlayer())
                TriggerGameOverAndPullOut();
            else
                EnterSearch();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Game Over + Pull Out
    // ─────────────────────────────────────────────────────────────
    private void TriggerGameOverAndPullOut()
    {
        if (_hasTriggeredGameOver) return;
        _hasTriggeredGameOver = true;

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm != null)
            pm.SetHiding(false);

        if (HidingManager.Instance != null)
            HidingManager.Instance.SetPlayerHiding(false);

        Renderer[] rends = player.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) r.enabled = true;

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
    // Animation
    // ─────────────────────────────────────────────────────────────
    private void AnimateBody()
    {
        float freq = (_state == State.Chase) ? chaseLegFreq : searchLegFreq;
        float sway = (_state == State.Chase)
            ? Mathf.Sin(Time.time * chaseSwayFreq) * chaseSwayMag
            : Mathf.Sin(Time.time * searchSwayFreq) * searchSwayMag;

        _animTime += Time.deltaTime * freq;
        float t = Mathf.Sin(_animTime);

        float legDir = invertLegs ? -1f : 1f;
        float armDir = invertArms ? -1f : 1f;

        float leftHipX = -90f + legDir * t * legSwingAngle + sway;
        float rightHipX = -90f + legDir * -t * legSwingAngle + sway;

        float lowerX = -90f + lowerLegBend;

        float rightArmX = -90f + armDir * t * armSwingAngle;
        float leftArmX = -90f + armDir * -t * beerArmAngle;

        SetXRotation(leftHip, leftHipX);
        SetXRotation(rightHip, rightHipX);
        SetXRotation(leftLowerLeg, lowerX);
        SetXRotation(rightLowerLeg, lowerX);
        SetXRotation(leftForearm, leftArmX);
        SetXRotation(rightForearm, rightArmX);
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
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * transform.forward;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
        Gizmos.DrawLine(eye, eye + leftDir * detectionRange);
        Gizmos.DrawLine(eye, eye + rightDir * detectionRange);

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