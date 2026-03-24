using System.Collections;
using UnityEngine;

/// <summary>
/// Weeping Angel-fjende der jager spilleren når lyset er slukket
/// og spilleren ikke kigger på den.
///
/// TILSTANDE:
///   Idle     — lyset er tændt, englen står stille på sin plads
///   Hunting  — lyset er slukket og spilleren kigger ikke på den
///   Frozen   — lyset er slukket men spilleren kigger på den
///
/// Petrified er fjernet — englen kan altid genaktiveres når lyset slukker igen.
///
/// OPSÆTNING:
///   1. Placer englen i scenen på dens startposition.
///   2. Sæt tag "Player" på spillerens GameObject.
///   3. Sæt obstacleLayers til de layers dine vægge er på.
///   4. WarehouseLightController kalder OnLightOff/OnLightOn automatisk.
/// </summary>
public class WeepingAngelEnemy : MonoBehaviour
{
    public enum AngelState { Idle, Hunting, Frozen }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Referencer")]
    [Tooltip("Finder automatisk via tag 'Player' hvis tomt.")]
    public Transform playerTransform;

    [Tooltip("Finder automatisk Camera.main hvis tomt.")]
    public Camera playerCamera;

    [Header("Line-of-Sight")]
    [Range(1f, 60f)]
    public float detectionAngle = 25f;

    [Tooltip("Layers der blokerer synslinjen.")]
    public LayerMask occlusionMask = ~0;

    [Range(5, 30)]
    public int losChecksPerSecond = 15;

    [Header("Bevægelse")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 180f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Layers der regnes som forhindringer (vægge osv.).")]
    public LayerMask obstacleLayers;

    [Tooltip("Afstand hvorfra englen opdager forhindringer foran sig.")]
    public float obstacleDetectionRange = 1.5f;

    [Tooltip("Bredde af englen brugt til side-raycasts — sæt til ca. halvdelen af bredden.")]
    public float angelWidth = 0.4f;

    [Header("Angreb")]
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
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    // Steering state
    private float _steerAngle = 0f;        // nuværende styrevinkel
    private float _steerTimer = 0f;        // tid siden vi sidst prøvede en ny retning
    private const float SteerInterval = 0.3f;

    public AngelState CurrentState => _state;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
            else Debug.LogWarning("[WeepingAngel] Ingen spiller fundet — sæt tag 'Player'.");
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _losInterval = 1f / losChecksPerSecond;
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    void Update()
    {
        if (_state == AngelState.Idle) return;

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

    // ── Lys-events ─────────────────────────────────────────────────

    /// <summary>Kaldes af WarehouseLightController når lyset slukkes.</summary>
    public void OnLightOff()
    {
        Debug.Log("[WeepingAngel] Lyset er slukket — begynder at jage.");
        EnterHunting();
    }

    /// <summary>Kaldes af WarehouseLightController når lyset tændes.</summary>
    public void OnLightOn()
    {
        Debug.Log("[WeepingAngel] Lyset er tændt — stopper.");
        EnterIdle();
    }

    // ── Tilstandsskift ─────────────────────────────────────────────

    void EnterIdle()
    {
        _state = AngelState.Idle;
        // Vent til spilleren kigger væk, teleportér så tilbage til start
        StartCoroutine(ReturnToStartWhenUnwatched());
    }

    void EnterHunting()
    {
        _state = AngelState.Hunting;
        _steerAngle = 0f;
        PlaySound(moveSound);
    }

    void EnterFrozen(bool wasAlreadyLooking)
    {
        _state = AngelState.Frozen;
        if (!wasAlreadyLooking)
            PlaySound(freezeSound);
    }

    IEnumerator ReturnToStartWhenUnwatched()
    {
        // Vent til spilleren ikke kigger i 1 sekund
        float unwatchedTime = 0f;
        while (unwatchedTime < 1f)
        {
            if (!CheckLineOfSight())
                unwatchedTime += Time.deltaTime;
            else
                unwatchedTime = 0f;
            yield return null;
        }

        // Teleportér tilbage usynligt
        transform.position = _startPosition;
        transform.rotation = _startRotation;
        Debug.Log("[WeepingAngel] Tilbage på startposition.");
    }

    // ── Bevægelse og pathfinding ───────────────────────────────────

    void MoveTowardsPlayer()
    {
        Vector3 toPlayer = playerTransform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.001f) return;

        Vector3 desiredDir = toPlayer.normalized;
        Vector3 moveDir = CalculateMoveDirection(desiredDir);

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
    /// Beregner bedste bevægelsesretning med wall-following logik.
    /// Bruger tre raycasts (frem, venstre-frem, højre-frem) og
    /// husker hvilken side den styre udenom for at undgå at gå i cirkler.
    /// </summary>
    Vector3 CalculateMoveDirection(Vector3 desiredDir)
    {
        _steerTimer += Time.deltaTime;

        Vector3 origin = transform.position + Vector3.up * 0.5f;

        // Tjek om den direkte vej er fri
        bool frontBlocked = Physics.Raycast(origin, desiredDir, obstacleDetectionRange, obstacleLayers);

        // Tjek siderne med lidt bredde (simulerer engelens krop)
        Vector3 leftOrig = origin - transform.right * angelWidth;
        Vector3 rightOrig = origin + transform.right * angelWidth;
        bool leftBlocked = Physics.Raycast(leftOrig, desiredDir, obstacleDetectionRange, obstacleLayers);
        bool rightBlocked = Physics.Raycast(rightOrig, desiredDir, obstacleDetectionRange, obstacleLayers);

        if (!frontBlocked && !leftBlocked && !rightBlocked)
        {
            // Vej er fri — nulstil styrevinkel gradvist
            _steerAngle = Mathf.MoveTowards(_steerAngle, 0f, 90f * Time.deltaTime);
            return Quaternion.Euler(0f, _steerAngle, 0f) * desiredDir;
        }

        // Forhindring forude — find en ny styrevinkel hvert SteerInterval sekund
        if (_steerTimer >= SteerInterval)
        {
            _steerTimer = 0f;

            // Prøv vinkler i stigende størrelse, skiftevis venstre og højre
            float[] angles = { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f, 150f, -150f, 180f };
            foreach (float angle in angles)
            {
                Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desiredDir;
                bool blocked = Physics.Raycast(origin, candidate, obstacleDetectionRange, obstacleLayers);
                if (!blocked)
                {
                    _steerAngle = angle;
                    break;
                }
            }
        }

        return Quaternion.Euler(0f, _steerAngle, 0f) * desiredDir;
    }

    // ── Line-of-Sight ──────────────────────────────────────────────

    bool CheckLineOfSight()
    {
        if (playerCamera == null || playerTransform == null) return false;

        Vector3 dirToAngel = (transform.position - playerCamera.transform.position).normalized;
        float angle = Vector3.Angle(playerCamera.transform.forward, dirToAngel);
        if (angle > detectionAngle) return false;

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
            EnterIdle();
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