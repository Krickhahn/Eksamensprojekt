using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshAgent))]
public class WeepingAngelEnemy : MonoBehaviour
{
    public enum AngelState { Idle, Hunting, Frozen }

    [Header("Referencer")]
    public Transform playerTransform;
    public Camera playerCamera;

    [Header("Line-of-Sight")]
    public float detectionAngle = 25f;
    public LayerMask occlusionMask = ~0;
    public int losChecksPerSecond = 15;

    [Header("Bevægelse (NavMesh)")]
    public float moveSpeed = 3.5f;
    public float rotationSpeed = 180f;

    [Header("Angreb")]
    public float attackRange = 1f;

    [Header("Adfærd")]
    public bool returnToStartOnLightOn = true;

    [Header("Lyd")]
    public AudioClip moveSound;
    public AudioClip freezeSound;
    public AudioClip killSound;
    public AudioSource audioSource;

    // Runtime
    private AngelState _state = AngelState.Idle;
    private float _losTimer;
    private float _losInterval;
    private bool _playerLooking;

    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private Rigidbody _rb;
    private NavMeshAgent _agent;

    public AngelState CurrentState => _state;

    private const float MoveSoundThreshold = 0.05f;
    private bool _playingFreezeSound = false;

    public AudioSource movementSource; // til moveSound (loop)
    public AudioSource sfxSource;      // til freeze og kill one-shots

    public float freezeAngle = 25f;
    public float unfreezeAngle = 30f;  // lidt større end freezeAngle



    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.freezeRotation = true;

        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.angularSpeed = rotationSpeed;
        _agent.updateRotation = true;
        _agent.updatePosition = true;

        _losInterval = 1f / losChecksPerSecond;

        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    void Update()
    {
        if (_state == AngelState.Idle) return;

        // Line-of-sight timing
        _losTimer += Time.deltaTime;
        if (_losTimer >= _losInterval)
        {
            _losTimer = 0f;
            bool wasLooking = _playerLooking;
            _playerLooking = CheckLineOfSight();

            if (_playerLooking)
            {
                if (_state == AngelState.Hunting)
                    EnterFrozen(wasLooking);
            }
            else
            {
                if (_state == AngelState.Frozen)
                    EnterHunting();
            }

        }

        if (_state == AngelState.Hunting && playerTransform != null)
        {
            _agent.SetDestination(playerTransform.position);
            CheckAttackRange();
        }
        if (_state == AngelState.Hunting)
        {
            UpdateMoveSound();
        }
        else
        {
            // State skift håndterer dette, men vi failsafer:
            if (audioSource != null && audioSource.isPlaying && audioSource.clip == moveSound)
                audioSource.Stop();
        }
    }

    // ── Lys events ──────────────────────────────
    public void OnLightOff()
    {
        EnterHunting();
    }

    public void OnLightOn()
    {
        EnterIdle();
    }

    // ── Tilstandsskift ───────────────────────────
    void EnterIdle()
    {
        _state = AngelState.Idle;
        _agent.isStopped = true;
        StopMoveSound();

        if (returnToStartOnLightOn)
            StartCoroutine(ReturnToStartWhenUnwatched());
    }

    void EnterHunting()
    {
        _state = AngelState.Hunting;
        _agent.isStopped = false;
        StartMoveSound();
    }

    void EnterFrozen(bool wasAlreadyLooking)
    {
        _state = AngelState.Frozen;

        if (_agent != null)
            _agent.isStopped = true;

        StopMoveSound();

        if (!wasAlreadyLooking)
        {
            _playingFreezeSound = true;
            audioSource.PlayOneShot(freezeSound);
            StartCoroutine(ResetFreezeSoundFlag());
        }
        _agent.isStopped = true;
        _agent.ResetPath();
        _agent.velocity = Vector3.zero;

    }
    IEnumerator ResetFreezeSoundFlag()
    {
        // Vent til freeze-lyden er færdig
        yield return new WaitForSeconds(freezeSound.length);
        _playingFreezeSound = false;
    }

    IEnumerator ReturnToStartWhenUnwatched()
    {
        float unwatchedTime = 0f;

        // Vent 1 sekund hvor spilleren ikke kigger
        while (unwatchedTime < 1f)
        {
            if (!CheckLineOfSight())
                unwatchedTime += Time.deltaTime;
            else
                unwatchedTime = 0f;

            yield return null;
        }

        // Stop bevægelse hvis agenten var på vej
        if (_agent != null)
            _agent.isStopped = true;

        // TELEPORT!
        transform.position = _startPosition;
        transform.rotation = _startRotation;
    }

    // ── Line of Sight ───────────────────────────
    bool CheckLineOfSight()
    {
        if (playerCamera == null) return false;

        Vector3 eyePos = playerCamera.transform.position;
        Vector3 target = transform.position + Vector3.up * 1.6f; // ram fjendens “head height”
        Vector3 dir = (target - eyePos).normalized;

        // --- Viewport check + debug ---
        Vector3 vp = playerCamera.WorldToViewportPoint(target);

        bool onScreen = vp.z > 0 &&
                        vp.x >= 0f && vp.x <= 1f &&
                        vp.y >= 0f && vp.y <= 1f;

        // Debug viewport indicator
#if UNITY_EDITOR
        if (!onScreen)
            Debug.DrawLine(eyePos, target, Color.red);
#endif

        if (!onScreen) return false;

        // --- Angle check ---
        float angle = Vector3.Angle(playerCamera.transform.forward, dir);

#if UNITY_EDITOR
        Color angleColor = angle <= detectionAngle ? Color.green : Color.red;
        Debug.DrawRay(eyePos, dir * 8f, angleColor);
#endif

        if (_state == AngelState.Frozen)
        {
            // Brug unfreeze-angle, så englen kun unfreezes hvis du kigger VÆK tydeligt
            if (angle > unfreezeAngle)
                return false;
        }
        else
        {
            // Brug freeze-angle til at fange at du kigger
            if (angle > freezeAngle)
                return false;
        }

        // --- Raycast occlusion check ---
        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, 30f, occlusionMask))
        {
#if UNITY_EDITOR
            Debug.DrawLine(eyePos, hit.point, Color.yellow);
#endif

            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                return false;
        }

#if UNITY_EDITOR
        Debug.DrawLine(eyePos, target, Color.green);
#endif
        return true;
    }

    // ── Attack ─────────────────────────────────
    void CheckAttackRange()
    {
        if (playerTransform == null) return;

        if (Vector3.Distance(transform.position, playerTransform.position) <= attackRange)
        {
            EnterIdle();
            PlayOneShot(killSound);
            GameOverManager.Instance?.TriggerGameOver();
        }
    }

    // ── Lyde ───────────────────────────────────

    void StartMoveSound()
    {
        if (movementSource == null || moveSound == null) return;

        if (movementSource.clip != moveSound)
            movementSource.clip = moveSound;

        if (!movementSource.isPlaying)
            movementSource.Play();
    }

    void StopMoveSound()
    {
        if (movementSource == null) return;
        movementSource.Stop(); // påvirker IKKE sfxSource
    }

    void PlayOneShot(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    void UpdateMoveSound()
    {
        if (_playingFreezeSound) return; // <-- VIGTIGT!

        if (_agent == null || audioSource == null || moveSound == null)
            return;

        float speed = _agent.velocity.magnitude;

        if (speed > MoveSoundThreshold)
        {
            if (!audioSource.isPlaying || audioSource.clip != moveSound)
            {
                audioSource.clip = moveSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying && audioSource.clip == moveSound)
                audioSource.Stop();
        }
    }

}