using UnityEngine;

/// <summary>
/// Weeping Angel-fjende der bevæger sig mod spilleren når lyset er slukket
/// og spilleren ikke kigger på den. Undgår vægge via raycast steering.
///
/// OPSÆTNING:
///   1. Tilføj dette script til din statue-prefab.
///   2. Sæt tag "Player" på spillerens GameObject.
///   3. Sæt obstacleLayers til de layers der indeholder vægge (typisk "Default").
///   4. WarehouseLightController kalder Activate/Deactivate automatisk.
///
/// KRÆVER INGEN NAVMESH.
/// </summary>
public class WeepingAngelEnemy : MonoBehaviour
{
    public enum AngelState { Idle, Hunting, Frozen, Petrified }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Referencer")]
    [Tooltip("Spillerens Transform. Finder automatisk via tag 'Player' hvis tomt.")]
    public Transform playerTransform;

    [Tooltip("Spillerens kamera. Finder automatisk Camera.main hvis tomt.")]
    public Camera playerCamera;

    [Header("Line-of-Sight")]
    [Tooltip("Maks vinkel fra kameraets forwardvektor før statuen regnes som set (grader).")]
    [Range(1f, 60f)]
    public float detectionAngle = 25f;

    [Tooltip("Layers der blokerer synslinjen — typisk Default.")]
    public LayerMask occlusionMask = ~0;

    [Tooltip("Hvor mange gange per sekund line-of-sight tjekkes.")]
    [Range(5, 30)]
    public int losChecksPerSecond = 15;

    [Header("Bevægelse")]
    [Tooltip("Bevægelseshastighed mod spilleren (meter/sekund).")]
    public float moveSpeed = 2f;

    [Tooltip("Rotationshastighed (grader/sekund).")]
    public float rotationSpeed = 180f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Layers der regnes som forhindringer englen skal undgå (vægge, kasser osv.).")]
    public LayerMask obstacleLayers;

    [Tooltip("Afstand hvorfra englen opdager og begynder at styre udenom forhindringer.")]
    public float obstacleDetectionRange = 1.5f;

    [Tooltip("Antal retninger der testes for at finde vej udenom forhindringer. Højere = bedre men dyrere.")]
    [Range(4, 16)]
    public int steeringRays = 8;

    [Header("Angreb")]
    [Tooltip("Afstand hvorfra englen trigger game over.")]
    public float attackRange = 1f;

    [Header("Lyd (valgfrit)")]
    public AudioClip moveSound;
    public AudioClip freezeSound;
    public AudioSource audioSource;

    // ── Runtime state ──────────────────────────────────────────────
    private AngelState _state = AngelState.Idle;
    private float _losTimer;
    private float _losInterval;
    private bool _playerLooking;

    public AngelState CurrentState => _state;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("[WeepingAngel] Ingen spiller fundet — sæt tag 'Player' på spillerens GameObject.");
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _losInterval = 1f / losChecksPerSecond;

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

            if (_playerLooking && _state == AngelState.Hunting)
                EnterFrozen(wasLooking);
            else if (!_playerLooking && _state == AngelState.Frozen)
                EnterHunting();
        }

        // ── Bevægelse ──────────────────────────────────────────────
        if (_state == AngelState.Hunting && playerTransform != null)
        {
            MoveTowardsPlayer();
            CheckAttackRange();
        }
    }

    // ── Aktivering ─────────────────────────────────────────────────

    /// <summary>Aktiverer englen. Kaldes af WarehouseLightController når lyset slukkes.</summary>
    public void Activate()
    {
        if (_state == AngelState.Petrified) return;
        Debug.Log("[WeepingAngel] Aktiveret — lyset er slukket.");
        EnterHunting();
    }

    /// <summary>Deaktiverer englen permanent. Kaldes når lyset tændes igen.</summary>
    public void Deactivate()
    {
        Debug.Log("[WeepingAngel] Permanent fryst — lyset er tændt.");
        EnterPetrified();
    }

    // ── Tilstandsskift ─────────────────────────────────────────────

    void EnterHunting()
    {
        _state = AngelState.Hunting;
        PlaySound(moveSound);
    }

    void EnterFrozen(bool wasAlreadyLooking)
    {
        _state = AngelState.Frozen;
        if (!wasAlreadyLooking)
            PlaySound(freezeSound);
    }

    void EnterPetrified()
    {
        _state = AngelState.Petrified;
        EnemySpawnManager.Instance?.UnregisterEnemy(gameObject);
    }

    // ── Bevægelse ──────────────────────────────────────────────────

    void MoveTowardsPlayer()
    {
        Vector3 desiredDir = playerTransform.position - transform.position;
        desiredDir.y = 0f;
        if (desiredDir.sqrMagnitude < 0.001f) return;
        desiredDir.Normalize();

        Vector3 moveDir = FindBestDirection(desiredDir);

        // Roter mod bevægelsesretningen
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Finder den bedste bevægelsesretning ved at kaste rays i en vifte.
    /// Prioriterer retninger tættest på spilleren der ikke rammer en forhindring.
    /// </summary>
    Vector3 FindBestDirection(Vector3 desiredDir)
    {
        // Prøv direkte retning mod spilleren først
        if (!IsBlocked(desiredDir))
            return desiredDir;

        // Prøv vinklede alternativer
        float bestScore = float.MinValue;
        Vector3 bestDir = desiredDir;

        for (int i = 0; i < steeringRays; i++)
        {
            // Fordel rays jævnt rundt om englen
            float angle = (360f / steeringRays) * i;
            Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desiredDir;

            if (IsBlocked(candidate)) continue;

            // Vælg retningen tættest på spilleren
            float score = Vector3.Dot(candidate, desiredDir);
            if (score > bestScore)
            {
                bestScore = score;
                bestDir = candidate;
            }
        }

        return bestDir;
    }

    bool IsBlocked(Vector3 dir)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        return Physics.Raycast(origin, dir, obstacleDetectionRange, obstacleLayers);
    }

    // ── Line-of-Sight ──────────────────────────────────────────────

    bool CheckLineOfSight()
    {
        if (playerCamera == null || playerTransform == null) return false;

        // Trin 1 — er englen inden for kameraets synsfelt?
        Vector3 dirToAngel = (transform.position - playerCamera.transform.position).normalized;
        float angle = Vector3.Angle(playerCamera.transform.forward, dirToAngel);
        if (angle > detectionAngle)
            return false;

        // Trin 2 — er der en væg imellem?
        Vector3 origin = playerCamera.transform.position;
        Vector3 target = transform.position + Vector3.up * 0.5f;
        float dist = Vector3.Distance(origin, target);
        Vector3 dir = (target - origin).normalized;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, occlusionMask))
        {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                return false;
        }

        return true;
    }

    // ── Angreb ─────────────────────────────────────────────────────

    void CheckAttackRange()
    {
        if (playerTransform == null) return;
        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            _state = AngelState.Petrified;
            GameOverManager.Instance?.TriggerGameOver();
        }
    }

    // ── Lyd ────────────────────────────────────────────────────────

    void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    // ── Gizmos ─────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, obstacleDetectionRange);

        if (playerCamera != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Vector3 fwd = playerCamera.transform.forward * 8f;
            Gizmos.DrawRay(playerCamera.transform.position,
                Quaternion.Euler(0, -detectionAngle, 0) * fwd);
            Gizmos.DrawRay(playerCamera.transform.position,
                Quaternion.Euler(0, detectionAngle, 0) * fwd);
        }
    }
#endif
}