using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SchizoEnemy : MonoBehaviour
{
    public enum SchizoState { Inactive, Haunting, Stalking }

    [Header("Referencer")]
    public Transform playerTransform;
    public Camera playerCamera;

    [Header("Haunting (atmosfærisk)")]
    public float hauntSpawnMinDist = 4f;
    public float hauntSpawnMaxDist = 9f;
    public float hauntVisibleDuration = 5f;
    public float hauntDetectAngle = 35f;
    public float hauntCooldownMin = 25f;
    public float hauntCooldownMax = 55f;

    [Header("Stalking (dødelig)")]
    public float stalkKillTime = 7f;
    [Tooltip("Vinklen spillerens kamera skal dreje væk fra fodtrins-retningen for at afbryde (grader)")]
    public float lookBackAngle = 100f;
    [Range(0f, 1f)]
    public float stalkChanceAfterHaunt = 0.35f;
    public float stalkCooldown = 90f;
    public bool stalkOnlyWhenLightsOn = true;

    [Header("Lyd")]
    public AudioSource footstepSource;
    public AudioClip footstepLoop;
    public AudioSource sfxSource;
    public AudioClip appearSound;
    public AudioClip dismissSound;

    // ── Private state ─────────────────────────────────────────────
    private SchizoState _state = SchizoState.Inactive;
    private NavMeshAgent _agent;
    private Renderer[] _renderers;
    private float _lastStalkTime = -999f;
    private bool _lightsOn = true;
    private bool _playerIsHiding;

    // Retningen fodtrinene kommer fra (bag spilleren) — bruges til look-back tjek
    private Vector3 _stalkSourceDirection;

    // ── Init ──────────────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.enabled = false;
        _renderers = GetComponentsInChildren<Renderer>();

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
        if (playerCamera == null)
            playerCamera = Camera.main;

        SetVisible(false);

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

    // ── Lys-events ────────────────────────────────────────────────
    public void OnLightOff() => _lightsOn = false;
    public void OnLightOn() => _lightsOn = true;

    // ── Hiding-event ──────────────────────────────────────────────
    void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;

        // Afbryd stalking med det samme hvis spilleren gemmer sig
        if (hiding && _state == SchizoState.Stalking)
            StopFootsteps();
    }

    // ── Haunting cyklus ───────────────────────────────────────────
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

    // ── Haunting ──────────────────────────────────────────────────
    IEnumerator DoHaunting()
    {
        _state = SchizoState.Haunting;

        Vector3 spawnPos = FindSpawnPosition();
        if (spawnPos == Vector3.zero) { _state = SchizoState.Inactive; yield break; }

        transform.position = spawnPos;
        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);

        SetVisible(true);
        PlaySFX(appearSound);

        float elapsed = 0f;
        while (elapsed < hauntVisibleDuration)
        {
            elapsed += Time.deltaTime;
            if (IsPlayerLookingAtMe()) { SetVisible(false); _state = SchizoState.Inactive; yield break; }
            yield return null;
        }

        SetVisible(false);
        _state = SchizoState.Inactive;
    }

    // ── Stalking ──────────────────────────────────────────────────
    IEnumerator DoStalking()
    {
        _state = SchizoState.Stalking;
        _lastStalkTime = Time.time;
        SetVisible(false);

        // Gem retningen bag spilleren på det tidspunkt stalking starter
        // Dette er den retning spilleren skal kigge imod for at afbryde
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
            // Afbryd hvis spilleren gemmmer sig
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

        // Timer løb ud
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

    // ── Look-behind tjek ──────────────────────────────────────────
    /// <summary>
    /// Tjekker om kameraet kigger mod den retning fodtrinene kom fra.
    /// Bruger kamera-forward direkte mod _stalkSourceDirection i world space
    /// — fungerer uanset om kameraet er et child af spilleren.
    /// </summary>
    bool IsPlayerLookingTowardSource()
    {
        if (playerCamera == null) return false;

        // _stalkSourceDirection er bag-retningen i world space fra da stalking startede
        // Vi tjekker om kamera-forward er tilstrækkelig tæt på den retning
        float angle = Vector3.Angle(playerCamera.transform.forward, _stalkSourceDirection);
        return angle <= lookBackAngle;
    }

    // ── Hjælpemetoder ─────────────────────────────────────────────
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
                float angle = Vector3.Angle(playerCamera.transform.forward, dirToCandidate);
                if (angle > 40f) return hit.position;
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

    void UpdateFootstepPosition(float distanceFactor)
    {
        if (footstepSource == null || playerTransform == null) return;
        float dist = Mathf.Lerp(0.5f, 7f, distanceFactor);
        // Placer lyden i _stalkSourceDirection fra spilleren — ikke altid direkte bag
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