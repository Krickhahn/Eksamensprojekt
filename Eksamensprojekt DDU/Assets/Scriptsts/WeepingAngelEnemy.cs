using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Weeping Angel-fjende der kun bevæger sig når spilleren IKKE kigger på den.
/// Kræver: NavMeshAgent, Collider, og at scenen har et NavMesh bagt.
///
/// OPSÆTNING:
///   1. Tilføj dette script til din statue-prefab.
///   2. Tilføj en NavMeshAgent komponent på samme GameObject.
///   3. Tilføj en Collider (Is Trigger = true) til angreb-detection.
///   4. Sæt playerTransform til spillerens GameObject i Inspector,
///      eller lad feltet stå tomt — scriptet finder spilleren automatisk via tag "Player".
///   5. Sæt lightSource til det Light-objekt der styrer rummets lys.
///   6. Kald Activate() fra dit lys-script når lyset slukkes.
///   7. Kald Deactivate() fra dit lys-script når lyset tændes igen.
///
/// TILSTANDE:
///   Idle     → Statuen er inaktiv. Står stille. Aktiveres af lys-event.
///   Hunting  → Bevæger sig mod spilleren når der ikke kigges på den.
///   Frozen   → Spilleren kigger på den — står fuldstændig stille.
///   Petrified → Lyset er tændt igen — fryser permanent på stedet.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngelEnemy : MonoBehaviour
{
    // ── Tilstande ──────────────────────────────────────────────────
    public enum AngelState { Idle, Hunting, Frozen, Petrified }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Referencer")]
    [Tooltip("Spillerens Transform. Lad stå tomt for automatisk find via tag 'Player'.")]
    public Transform playerTransform;

    [Tooltip("Kameraet der bruges til line-of-sight. Lad stå tomt for Camera.main.")]
    public Camera playerCamera;

    [Header("Line-of-Sight")]
    [Tooltip("Maks vinkel fra kameraets forwardvektor før statuen regnes som 'set' (grader).")]
    [Range(1f, 45f)]
    public float detectionAngle = 20f;

    [Tooltip("Layer mask til raycast — ekskludér spillerens eget layer.")]
    public LayerMask occlusionMask = ~0;

    [Tooltip("Hvor mange gange per sekund line-of-sight tjekkes. Lavere = bedre performance.")]
    [Range(5, 30)]
    public int losChecksPerSecond = 15;

    [Header("Bevægelse")]
    [Tooltip("Bevægelseshastighed mod spilleren.")]
    public float moveSpeed = 2.5f;

    [Tooltip("Rotationshastighed når statuen vender mod spilleren (grader/sekund).")]
    public float rotationSpeed = 120f;

    [Header("Angreb")]
    [Tooltip("Afstand hvorfra statuen trigger game over.")]
    public float attackRange = 1.2f;

    [Header("Lyd (valgfrit)")]
    [Tooltip("Lyd der afspilles når statuen begynder at bevæge sig.")]
    public AudioClip moveSound;

    [Tooltip("Lyd der afspilles når statuen fryser fordi spilleren kigger.")]
    public AudioClip freezeSound;

    [Tooltip("AudioSource på statuen. Lad stå tomt for automatisk find.")]
    public AudioSource audioSource;

    // ── Runtime state ──────────────────────────────────────────────
    private AngelState _state = AngelState.Idle;
    private NavMeshAgent _agent;
    private float _losTimer;
    private float _losInterval;
    private bool _playerLooking;

    public AngelState CurrentState => _state;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else Debug.LogWarning("[WeepingAngel] Ingen spiller fundet — sæt tag 'Player' på spilleren.");
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        _losInterval = 1f / losChecksPerSecond;

        // NavMeshAgent konfigureres her så objektet er placeret på NavMesh først
        _agent.speed = moveSpeed;
        _agent.angularSpeed = 0f; // Vi styrer rotation manuelt
        _agent.isStopped = true;

        // Registrér i EnemySpawnManager hvis det findes
        EnemySpawnManager.Instance?.RegisterEnemy(gameObject);
    }

    void Update()
    {
        if (_state == AngelState.Idle || _state == AngelState.Petrified)
            return;

        // ── Line-of-sight tjek ─────────────────────────────────────
        _losTimer += Time.deltaTime;
        if (_losTimer >= _losInterval)
        {
            _losTimer = 0f;
            bool wasLooking = _playerLooking;
            _playerLooking = CheckLineOfSight();

            // Skift tilstand baseret på om spilleren kigger
            if (_playerLooking && _state == AngelState.Hunting)
                EnterFrozen(wasLooking);
            else if (!_playerLooking && _state == AngelState.Frozen)
                EnterHunting();
        }

        // ── Bevægelse og rotation ──────────────────────────────────
        if (_state == AngelState.Hunting)
        {
            if (playerTransform != null)
            {
                _agent.SetDestination(playerTransform.position);
                RotateTowardsPlayer();
                CheckAttackRange();
            }
        }
    }

    // ── Tilstandsskift ─────────────────────────────────────────────

    /// <summary>Aktiverer statuen — kaldes når lyset slukkes.</summary>
    public void Activate()
    {
        if (_state == AngelState.Petrified) return;

        Debug.Log("[WeepingAngel] Aktiveret — lyset er slukket.");
        EnterHunting();
    }

    /// <summary>Deaktiverer statuen permanent — kaldes når lyset tændes.</summary>
    public void Deactivate()
    {
        Debug.Log("[WeepingAngel] Deaktiveret — lyset er tændt igen.");
        EnterPetrified();
    }

    void EnterHunting()
    {
        _state = AngelState.Hunting;
        _agent.isStopped = false;

        PlaySound(moveSound);

        Debug.Log("[WeepingAngel] Begynder at jage.");
    }

    void EnterFrozen(bool wasAlreadyLooking)
    {
        _state = AngelState.Frozen;
        _agent.isStopped = true;

        if (!wasAlreadyLooking)
            PlaySound(freezeSound);
    }

    void EnterPetrified()
    {
        _state = AngelState.Petrified;
        _agent.isStopped = true;
        _agent.enabled = false; // Slår NavMesh helt fra så den ikke glider

        EnemySpawnManager.Instance?.UnregisterEnemy(gameObject);

        Debug.Log("[WeepingAngel] Permanent fryst.");
    }

    // ── Line-of-Sight ──────────────────────────────────────────────

    /// <summary>
    /// Returnerer true hvis spilleren både har statuen inden for
    /// kameraets synsfelt OG ikke er blokeret af en væg.
    /// </summary>
    bool CheckLineOfSight()
    {
        if (playerCamera == null || playerTransform == null) return false;

        // Trin 1: Er statuen inden for kameraets vinkel?
        Vector3 dirToAngel = (transform.position - playerCamera.transform.position).normalized;
        float angle = Vector3.Angle(playerCamera.transform.forward, dirToAngel);

        if (angle > detectionAngle)
            return false; // Statuen er uden for synsvinklen

        // Trin 2: Er der en væg imellem?
        Vector3 origin = playerCamera.transform.position;
        Vector3 target = transform.position + Vector3.up * 0.5f; // Sigt mod statuens midte
        float distance = Vector3.Distance(origin, target);

        if (Physics.Raycast(origin, (target - origin).normalized, out RaycastHit hit, distance, occlusionMask))
        {
            // Ramte noget — tjek om det er statuen selv eller en forhindring
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                return false; // Blokeret af væg eller andet objekt
        }

        return true;
    }

    // ── Angreb ─────────────────────────────────────────────────────

    void CheckAttackRange()
    {
        if (playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= attackRange)
            TriggerGameOver();
    }

    void TriggerGameOver()
    {
        Debug.Log("[WeepingAngel] Spilleren er fanget — GAME OVER.");
        _agent.isStopped = true;
        _state = AngelState.Petrified; // Stop statuen mens game over håndteres

        // Find GameOverManager hvis det findes, ellers log en advarsel
        GameOverManager gameOver = FindAnyObjectByType<GameOverManager>();
        if (gameOver != null)
            gameOver.TriggerGameOver();
        else
            Debug.LogWarning("[WeepingAngel] Ingen GameOverManager fundet — opret et script der håndterer game over.");
    }

    // ── Hjælpemetoder ──────────────────────────────────────────────

    void RotateTowardsPlayer()
    {
        if (playerTransform == null) return;

        Vector3 dir = (playerTransform.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    // ── Gizmo ──────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Angrebsradius
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Line-of-sight kegle fra kameraet
        if (playerCamera != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 forward = playerCamera.transform.forward * 10f;
            Quaternion left = Quaternion.Euler(0, -detectionAngle, 0);
            Quaternion right = Quaternion.Euler(0, detectionAngle, 0);
            Gizmos.DrawRay(playerCamera.transform.position, left * forward);
            Gizmos.DrawRay(playerCamera.transform.position, right * forward);
            Gizmos.DrawRay(playerCamera.transform.position, forward);
        }
    }
#endif
}