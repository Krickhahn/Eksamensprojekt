using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ShadowEnemy : MonoBehaviour
{
    public enum ShadowState { Dormant, Hunting }

    [Header("Referencer")]
    public Transform playerTransform;

    [Header("Bevægelse")]
    public float huntSpeed = 5f;
    public float attackRange = 1.2f;
    public float wanderRadius = 15f;

    [Header("Visuals")]
    [Range(0f, 1f)]
    public float darkAlpha = 0.35f;
    [Tooltip("URP: '_BaseColor', Built-in: '_Color'")]
    public string alphaProperty = "_BaseColor";

    [Header("Lyd")]
    public AudioSource movementSource;
    public AudioClip moveSound;
    public AudioSource sfxSource;
    public AudioClip appearSound;
    public AudioClip killSound;

    // ── Private state ─────────────────────────────────────────────
    private ShadowState _state = ShadowState.Dormant;
    private NavMeshAgent _agent;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    private Vector3 _spawnPosition;
    private bool _playerIsHiding;

    public ShadowState CurrentState => _state;

    // ── Init ──────────────────────────────────────────────────────
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _renderers = GetComponentsInChildren<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        _spawnPosition = transform.position;

        if (playerTransform == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        SetAlpha(0f);
        EnterDormant(teleport: false);

        // Lys-events
        if (WarehouseLightController.Instance != null)
        {
            WarehouseLightController.Instance.onLightOff.AddListener(OnLightOff);
            WarehouseLightController.Instance.onLightOn.AddListener(OnLightOn);
            if (!WarehouseLightController.Instance.IsPowerOn)
                EnterHunting();
        }

        // Hiding-events
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

    // ── Update ────────────────────────────────────────────────────
    void Update()
    {
        if (_state != ShadowState.Hunting || playerTransform == null) return;

        // Opdater destination hvert frame
        _agent.SetDestination(playerTransform.position);
        UpdateMoveSound();

        // Angreb kun hvis spilleren ikke er gemt
        if (!_playerIsHiding &&
            Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            EnterDormant(teleport: true);
            PlaySFX(killSound);
            GameOverManager.Instance?.TriggerGameOver("Skyggen indhentede dig i mørket...");
        }
    }

    // ── Lys-events ────────────────────────────────────────────────
    public void OnLightOff()
    {
        if (!_playerIsHiding)
            EnterHunting();
    }

    public void OnLightOn()
    {
        EnterDormant(teleport: true);
    }

    // ── Hiding-event ──────────────────────────────────────────────
    void OnPlayerHidingChanged(bool hiding)
    {
        _playerIsHiding = hiding;

        if (hiding && _state == ShadowState.Hunting)
            EnterDormant(teleport: true); // Skyggen forsvinder når spilleren gemmer sig
    }

    // ── Tilstandsskift ────────────────────────────────────────────
    void EnterHunting()
    {
        _state = ShadowState.Hunting;

        // Sæt isStopped = false FØR ResetPath, ellers kan agenten ikke modtage ny destination
        _agent.isStopped = false;
        _agent.ResetPath();
        _agent.speed = huntSpeed;

        // Giv agenten en destination med det samme så den ikke venter til næste Update()
        if (playerTransform != null)
            _agent.SetDestination(playerTransform.position);

        SetAlpha(darkAlpha);
        PlaySFX(appearSound);
        StartMoveSound();
    }

    void EnterDormant(bool teleport)
    {
        _state = ShadowState.Dormant;
        _agent.isStopped = true;
        _agent.ResetPath();
        StopMoveSound();
        SetAlpha(0f);

        if (teleport)
            StartCoroutine(TeleportToSafety());
    }

    // ── Teleport ──────────────────────────────────────────────────
    IEnumerator TeleportToSafety()
    {
        yield return null;

        for (int i = 0; i < 20; i++)
        {
            Vector3 candidate = _spawnPosition + Random.insideUnitSphere * wanderRadius;
            candidate.y = _spawnPosition.y;

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                float dist = playerTransform != null
                    ? Vector3.Distance(hit.position, playerTransform.position)
                    : 999f;

                if (dist > 10f)
                {
                    _agent.Warp(hit.position);
                    // Eksplicit nulstil isStopped efter Warp — Warp kan efterlade agenten i en stoppet intern tilstand
                    _agent.isStopped = false;
                    yield break;
                }
            }
        }

        _agent.Warp(_spawnPosition);
        _agent.isStopped = false;
    }

    // ── Visuals ───────────────────────────────────────────────────
    void SetAlpha(float alpha)
    {
        foreach (var r in _renderers)
        {
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(alphaProperty, alpha);
            r.SetPropertyBlock(_propBlock);
        }
    }

    // ── Lyd ───────────────────────────────────────────────────────
    void StartMoveSound()
    {
        if (movementSource == null || moveSound == null) return;
        movementSource.clip = moveSound;
        movementSource.loop = true;
        if (!movementSource.isPlaying) movementSource.Play();
    }

    void StopMoveSound()
    {
        if (movementSource != null && movementSource.isPlaying)
            movementSource.Stop();
    }

    void UpdateMoveSound()
    {
        if (movementSource == null) return;
        bool moving = _agent.velocity.magnitude > 0.05f;
        if (moving && !movementSource.isPlaying) StartMoveSound();
        if (!moving && movementSource.isPlaying) StopMoveSound();
    }

    void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(
            Application.isPlaying ? _spawnPosition : transform.position, wanderRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif
}