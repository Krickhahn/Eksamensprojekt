using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Schizofrenifejnden med pose-support.
///
/// POSES — to metoder at sætte op:
///   A) Animator: Tilføj en Animator til fjende-prefabben med states der matcher
///      navnene i hauntPoses (fx "Pose_Point", "Pose_Crouch", "Pose_Scream").
///      Scriptet kalder animator.Play(poseName) ved hver haunting.
///
///   B) poseObjects: Opret separate child-GameObjects (ét per pose — fx hver
///      med en anden mesh/rotation slået til) og træk dem ind i poseObjects.
///      Scriptet aktiverer ét tilfældigt objekt og deaktiverer de øvrige.
///
///   Begge systemer virker uden asset — poses springes bare over hvis
///   hverken Animator eller poseObjects er sat op.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class SchizoEnemy : MonoBehaviour
{
    public enum SchizoState { Inactive, Haunting, Stalking }

    // ─────────────────────────────────────────────
    //  REFERENCER
    // ─────────────────────────────────────────────
    [Header("Referencer")]
    public Transform playerTransform;
    public Camera playerCamera;

    // ─────────────────────────────────────────────
    //  HAUNTING
    // ─────────────────────────────────────────────
    [Header("Haunting (atmosfærisk)")]
    public float hauntSpawnMinDist = 4f;
    public float hauntSpawnMaxDist = 9f;
    public float hauntVisibleDuration = 5f;
    public float hauntDetectAngle = 35f;
    public float hauntCooldownMin = 25f;
    public float hauntCooldownMax = 55f;

    // ─────────────────────────────────────────────
    //  POSES
    // ─────────────────────────────────────────────
    [Header("Poses")]
    [Tooltip("Animator på fjende-meshens GameObject. Kan være null — tilføjes når du har et asset.")]
    public Animator animator;

    [Tooltip("Navne på Animator-states brugt som poses. Et tilfældigt vælges ved hver haunting.")]
    public string[] hauntPoses = { "Pose_Idle", "Pose_Point", "Pose_Crouch", "Pose_Scream", "Pose_Stare" };

    [Tooltip("Alternativ til Animator: separate child-GameObjects per pose. " +
             "Kun ét aktiveres ad gangen. Lad stå tomt hvis du bruger Animator.")]
    public GameObject[] poseObjects;

    // ─────────────────────────────────────────────
    //  STALKING
    // ─────────────────────────────────────────────
    [Header("Stalking (dødelig)")]
    public float stalkKillTime = 7f;
    [Tooltip("Vinklen spillerens kamera skal dreje mod fodtrins-retningen for at afbryde (grader)")]
    public float lookBackAngle = 100f;
    [Range(0f, 1f)]
    public float stalkChanceAfterHaunt = 0.35f;
    public float stalkCooldown = 90f;
    public bool stalkOnlyWhenLightsOn = true;

    // ─────────────────────────────────────────────
    //  LYD
    // ─────────────────────────────────────────────
    [Header("Lyd")]
    public AudioSource footstepSource;
    public AudioClip footstepLoop;
    public AudioSource sfxSource;
    public AudioClip appearSound;
    public AudioClip dismissSound;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    private SchizoState _state = SchizoState.Inactive;
    private NavMeshAgent _agent;
    private Renderer[] _renderers;
    private float _lastStalkTime = -999f;
    private bool _lightsOn = true;
    private bool _playerIsHiding;
    private Vector3 _stalkSourceDirection;
    private int _currentPoseIndex = -1;

    // ─────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.enabled = false;
        _renderers = GetComponentsInChildren<Renderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
        if (playerCamera == null)
            playerCamera = Camera.main;

        SetVisible(false);
        DeactivateAllPoseObjects();

        if (WarehouseLightController.Instance != null)
        {
            WarehouseLightController.Instance.onLightOff.AddListener(OnLightOff);
            WarehouseLightController.Instance.onLightOn.AddListener(OnLightOn);
            _lightsOn = WarehouseLightController.Instance.IsPowerOn;
        }

        StartCoroutine(RegisterHidingListener());
        StartCoroutine(HauntingCycle());
    }

    IEnumerator RegisterHidingListener()
    {
        yield return null;
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged += OnPlayerHidingChanged;
        else
            Debug.LogWarning("[SchizoEnemy] HidingManager ikke fundet!");
    }

    void OnDestroy()
    {
        if (WarehouseLightController.Instance != null)
        {
            WarehouseLightController.Instance.onLightOff.RemoveListener(OnLightOff);
            WarehouseLightController.Instance.onLightOn.RemoveListener(OnLightOn);
        }
        if (HidingManager.Instance != null)
            HidingManager.Instance.OnPlayerHidingChanged -= OnPlayerHidingChanged;
    }

    // ─────────────────────────────────────────────
    //  LYS-EVENTS
    // ─────────────────────────────────────────────
    public void OnLightOff() => _lightsOn = false;
    public void OnLightOn() => _lightsOn = true;

    // ─────────────────────────────────────────────
    //  HIDING-EVENT
    // ─────────────────────────────────────────────
    void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;
        if (hiding && _state == SchizoState.Stalking)
            StopFootsteps();
    }

    // ─────────────────────────────────────────────
    //  HAUNTING CYKLUS
    // ─────────────────────────────────────────────
    IEnumerator HauntingCycle()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(hauntCooldownMin, hauntCooldownMax));

            if (!_playerIsHiding)
                yield return StartCoroutine(DoHaunting());

            bool canStalk = Time.time - _lastStalkTime >= stalkCooldown;
            bool lightOk = !stalkOnlyWhenLightsOn || _lightsOn;

            if (canStalk && lightOk && !_playerIsHiding && Random.value < stalkChanceAfterHaunt)
            {
                yield return new WaitForSeconds(Random.Range(3f, 8f));
                if (!_playerIsHiding)
                    yield return StartCoroutine(DoStalking());
            }
        }
    }

    // ─────────────────────────────────────────────
    //  HAUNTING
    // ─────────────────────────────────────────────
    IEnumerator DoHaunting()
    {
        _state = SchizoState.Haunting;

        Vector3 spawnPos = FindSpawnPosition();
        if (spawnPos == Vector3.zero) { _state = SchizoState.Inactive; yield break; }

        transform.position = spawnPos;

        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(lookDir);

        // Vælg tilfældig pose inden vi viser fjenden
        ApplyRandomPose();

        SetVisible(true);
        PlaySFX(appearSound);

        float elapsed = 0f;
        while (elapsed < hauntVisibleDuration)
        {
            elapsed += Time.deltaTime;
            if (IsPlayerLookingAtMe())
            {
                SetVisible(false);
                DeactivateAllPoseObjects();
                _state = SchizoState.Inactive;
                yield break;
            }
            yield return null;
        }

        SetVisible(false);
        DeactivateAllPoseObjects();
        _state = SchizoState.Inactive;
    }

    // ─────────────────────────────────────────────
    //  STALKING
    // ─────────────────────────────────────────────
    IEnumerator DoStalking()
    {
        _state = SchizoState.Stalking;
        _lastStalkTime = Time.time;
        SetVisible(false);

        _stalkSourceDirection = -playerTransform.forward;

        if (footstepSource != null && footstepLoop != null)
        {
            footstepSource.clip = footstepLoop;
            footstepSource.loop = true;
            footstepSource.volume = 0.25f;
            footstepSource.pitch = 0.85f;
            footstepSource.spatialBlend = 1f;
            UpdateFootstepPosition(1f);
            footstepSource.Play();
        }

        float elapsed = 0f;
        while (elapsed < stalkKillTime)
        {
            if (_playerIsHiding) { yield return StartCoroutine(DismissStalking()); yield break; }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stalkKillTime);

            if (footstepSource != null && footstepSource.isPlaying)
            {
                footstepSource.pitch = Mathf.Lerp(0.85f, 1.7f, t);
                footstepSource.volume = Mathf.Lerp(0.25f, 1f, t);
                UpdateFootstepPosition(1f - t);
            }

            if (IsPlayerLookingTowardSource())
            {
                yield return StartCoroutine(DismissStalking());
                yield break;
            }

            yield return null;
        }

        StopFootsteps();
        _state = SchizoState.Inactive;
        GameOverManager.Instance?.TriggerGameOver("Noget indhentede dig bagfra...");
    }

    IEnumerator DismissStalking()
    {
        StopFootsteps();
        PlaySFX(dismissSound);
        _state = SchizoState.Inactive;
        yield return new WaitForSeconds(0.4f);
    }

    // ─────────────────────────────────────────────
    //  POSE-SYSTEM
    // ─────────────────────────────────────────────
    void ApplyRandomPose()
    {
        // Animator-path
        if (animator != null && hauntPoses != null && hauntPoses.Length > 0)
        {
            int index = PickDifferentRandom(hauntPoses.Length, _currentPoseIndex);
            _currentPoseIndex = index;
            animator.Play(hauntPoses[index]);
            return;
        }

        // poseObjects-path
        if (poseObjects != null && poseObjects.Length > 0)
        {
            int index = PickDifferentRandom(poseObjects.Length, _currentPoseIndex);
            _currentPoseIndex = index;
            for (int i = 0; i < poseObjects.Length; i++)
                if (poseObjects[i] != null)
                    poseObjects[i].SetActive(i == index);
        }
    }

    void DeactivateAllPoseObjects()
    {
        if (poseObjects == null) return;
        foreach (var obj in poseObjects)
            if (obj != null) obj.SetActive(false);
    }

    int PickDifferentRandom(int count, int previousIndex)
    {
        if (count <= 1) return 0;
        int index;
        int attempts = 0;
        do { index = Random.Range(0, count); attempts++; }
        while (index == previousIndex && attempts < 10);
        return index;
    }

    // ─────────────────────────────────────────────
    //  HJÆLPEMETODER
    // ─────────────────────────────────────────────
    Vector3 FindSpawnPosition()
    {
        for (int attempt = 0; attempt < 25; attempt++)
        {
            float dist = Random.Range(hauntSpawnMinDist, hauntSpawnMaxDist);
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            dir.Normalize();

            Vector3 candidate = playerTransform.position + dir * dist;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                Vector3 dirToCandidate = (hit.position - playerCamera.transform.position).normalized;
                if (Vector3.Angle(playerCamera.transform.forward, dirToCandidate) > 40f)
                    return hit.position;
            }
        }
        return Vector3.zero;
    }

    bool IsPlayerLookingAtMe()
    {
        if (playerCamera == null) return false;
        Vector3 dirToMe = (transform.position - playerCamera.transform.position).normalized;
        return Vector3.Angle(playerCamera.transform.forward, dirToMe) <= hauntDetectAngle;
    }

    bool IsPlayerLookingTowardSource()
    {
        if (playerCamera == null) return false;
        return Vector3.Angle(playerCamera.transform.forward, _stalkSourceDirection) <= lookBackAngle;
    }

    void UpdateFootstepPosition(float distanceFactor)
    {
        if (footstepSource == null || playerTransform == null) return;
        float dist = Mathf.Lerp(0.5f, 7f, distanceFactor);
        Vector3 pos = playerTransform.position + _stalkSourceDirection.normalized * dist;
        pos.y = playerTransform.position.y + 0.5f;
        footstepSource.transform.position = pos;
    }

    void StopFootsteps()
    {
        if (footstepSource != null && footstepSource.isPlaying)
            footstepSource.Stop();
    }

    void SetVisible(bool visible)
    {
        foreach (var r in _renderers) r.enabled = visible;
    }

    void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;
        Gizmos.color = new Color(0.7f, 0f, 0.7f, 0.2f);
        Gizmos.DrawWireSphere(playerTransform.position, hauntSpawnMinDist);
        Gizmos.DrawWireSphere(playerTransform.position, hauntSpawnMaxDist);
    }
#endif
}