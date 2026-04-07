using UnityEngine;
using UnityEngine.AI;

public class ChasingEnemy : MonoBehaviour
{
    public enum State { Idle, Chase, Search }
    [SerializeField] private State currentState = State.Idle;

    [Header("Auto-find Player")]
    [Tooltip("Hvis tom, finder fjenden automatisk Player via tag 'Player'.")]
    [SerializeField] private Transform player;

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverScreen;

    private NavMeshAgent agent;

    [Header("Vision Settings")]
    [Tooltip("Hvor langt fjenden kan se.")]
    [SerializeField] private float viewDistance = 12f;

    [Tooltip("Synsvinkel i grader (fx 90 = 45 grader til hver side).")]
    [Range(1f, 180f)]
    [SerializeField] private float viewAngle = 90f;

    [Tooltip("Lag der må rammes af syns-raycast. Sæt typisk til 'Everything' og fjern Enemy-laget hvis nødvendigt.")]
    [SerializeField] private LayerMask visionMask = ~0;

    [Tooltip("Højde på fjendens 'øjne' over pivot (meter).")]
    [SerializeField] private float eyeHeight = 1.6f;

    [Tooltip("Højde på punktet vi sigter efter på spilleren (meter fra spillerens pivot).")]
    [SerializeField] private float playerTargetHeight = 1.2f;

    [Header("Chase Settings")]
    [Tooltip("Hvor længe fjenden må miste synet før den giver op.")]
    [SerializeField] private float loseSightTime = 3f;

    private float loseSightTimer = 0f;
    private bool playerIsHiding = false;

    // Debug/Gizmo helpers
    private bool lastCanSeePlayer = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        AutoFindPlayerIfNeeded();

        // Lyt på hiding-event (din manager er singleton og broadcaster ændringer). [2](https://teccph-my.sharepoint.com/personal/otkr2_elev_tec_dk/Documents/Microsoft%20Copilot%20Chat%20Files/HidingManager.cs)
        if (HidingManager.Instance != null)
        {
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
            playerIsHiding = HidingManager.Instance.IsPlayerHiding;
        }
    }

    private void Update()
    {
        // Hvis player blev destrueret / scene reload, så find igen
        if (player == null)
            AutoFindPlayerIfNeeded();

        if (player == null) return;

        switch (currentState)
        {
            case State.Idle:
                IdleBehaviour();
                break;
            case State.Chase:
                ChaseBehaviour();
                break;
            case State.Search:
                SearchBehaviour();
                break;
        }
    }

    private void AutoFindPlayerIfNeeded()
    {
        if (player != null) return;

        GameObject go = GameObject.FindGameObjectWithTag("Player"); // dine kasser forventer Player-tag. [1](https://teccph-my.sharepoint.com/personal/otkr2_elev_tec_dk/Documents/Microsoft%20Copilot%20Chat%20Files/CardboardBox.cs)
        if (go != null)
            player = go.transform;
    }

    // ---------------------------------------------------------
    // STATE LOGIC
    // ---------------------------------------------------------
    private void IdleBehaviour()
    {
        lastCanSeePlayer = CanSeePlayer();

        if (lastCanSeePlayer)
            StartChase();
    }

    private void ChaseBehaviour()
    {
        if (player == null) return;

        agent.SetDestination(player.position);

        // Hvis spilleren gemmer sig midt i jagten:
        if (playerIsHiding)
        {
            lastCanSeePlayer = CanSeePlayer();

            if (lastCanSeePlayer)
            {
                // Fjenden SER spilleren gemme sig -> træk ud + game over
                PullPlayerOutOfBox();
            }
            else
            {
                // Fjenden ser ikke at spilleren gemmer sig (fx rundt om hjørne) -> stop jagten
                StopChase();
            }
            return;
        }

        // Mistet synet af spilleren?
        lastCanSeePlayer = CanSeePlayer();
        if (!lastCanSeePlayer)
        {
            loseSightTimer += Time.deltaTime;
            if (loseSightTimer >= loseSightTime)
                StopChase();
        }
        else
        {
            loseSightTimer = 0f;
        }
    }

    private void SearchBehaviour()
    {
        // Minimal placeholder: Du kan udbygge med patrol/look-around.
        lastCanSeePlayer = CanSeePlayer();
        if (lastCanSeePlayer)
            StartChase();
    }

    // ---------------------------------------------------------
    // EVENT: spiller gemmer sig
    // ---------------------------------------------------------
    private void OnPlayerHidingChanged(bool hiding)
    {
        playerIsHiding = hiding;

        // Kun relevant hvis fjenden er i gang med at jage
        if (currentState != State.Chase || player == null)
            return;

        if (hiding)
        {
            // Samme logik som i Update: ser fjenden det ske eller ej?
            lastCanSeePlayer = CanSeePlayer();
            if (lastCanSeePlayer) PullPlayerOutOfBox();
            else StopChase();
        }
    }

    // ---------------------------------------------------------
    // ACTIONS
    // ---------------------------------------------------------
    private void StartChase()
    {
        currentState = State.Chase;
        loseSightTimer = 0f;
    }

    private void StopChase()
    {
        currentState = State.Idle;
        loseSightTimer = 0f;
        if (agent != null) agent.ResetPath();
    }

    private void PullPlayerOutOfBox()
    {
        Debug.Log("[ChasingEnemy] Fjenden så spilleren gemme sig -> GAME OVER!");

        if (agent != null) agent.ResetPath();

        if (gameOverScreen != null)
            gameOverScreen.SetActive(true);

        Time.timeScale = 0f;
    }

    // ---------------------------------------------------------
    // VISION SYSTEM
    // ---------------------------------------------------------
    private bool CanSeePlayer()
    {
        if (player == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPos = player.position + Vector3.up * playerTargetHeight;

        Vector3 toPlayer = (targetPos - eyePos);
        float dist = toPlayer.magnitude;

        // Distance check
        if (dist > viewDistance) return false;

        // Angle check
        Vector3 dir = toPlayer / Mathf.Max(dist, 0.0001f);
        float angle = Vector3.Angle(transform.forward, dir);
        if (angle > viewAngle * 0.5f) return false;

        // Line of sight (raycast)
        // Vi rammer første collider på visionMask. Hvis det er Player -> synligt.
        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, viewDistance, visionMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.CompareTag("Player");
        }

        return false;
    }

    private void OnDestroy()
    {
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    // ---------------------------------------------------------
    // GIZMOS (EDITOR)
    // ---------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Tegn syns-radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, viewDistance);

        // Tegn synsvinkel-kegle (2 yderlinjer)
        Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

        float half = viewAngle * 0.5f;
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * transform.forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * transform.forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(eyePos, eyePos + leftDir * viewDistance);
        Gizmos.DrawLine(eyePos, eyePos + rightDir * viewDistance);

        // Hvis vi har en player, så tegn LOS-ray (grøn/rød)
        if (Application.isPlaying && player != null)
        {
            Vector3 targetPos = player.position + Vector3.up * playerTargetHeight;
            Vector3 dir = (targetPos - eyePos).normalized;

            Gizmos.color = lastCanSeePlayer ? Color.green : Color.red;
            Gizmos.DrawLine(eyePos, targetPos);

            // Markér øje- og målpunkt
            Gizmos.DrawSphere(eyePos, 0.05f);
            Gizmos.DrawSphere(targetPos, 0.05f);
        }
    }
#endif
}
