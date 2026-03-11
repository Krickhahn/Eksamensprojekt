using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 3D Player Movement Controller
/// Attach this to a GameObject with a CharacterController component.
/// Adjust all parameters in the Inspector.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    [Header("Movement")]
    [Tooltip("Top speed when walking.")]
    public float walkSpeed = 5f;

    [Tooltip("Top speed when sprinting (hold Left Shift).")]
    public float sprintSpeed = 10f;

    [Tooltip("Top speed when crouching.")]
    public float crouchSpeed = 2.5f;

    [Tooltip("How quickly the player reaches top speed.")]
    public float acceleration = 15f;

    [Tooltip("How quickly the player slows down.")]
    public float deceleration = 20f;

    // ─────────────────────────────────────────────
    //  GRAVITY
    // ─────────────────────────────────────────────
    [Header("Gravity")]
    [Tooltip("Gravity applied when grounded (keeps the player stuck to slopes).")]
    public float groundedGravity = -2f;

    [Tooltip("Gravity applied when airborne.")]
    public float fallGravity = -20f;

    [Tooltip("Maximum downward speed.")]
    public float maxFallSpeed = -30f;

    // ─────────────────────────────────────────────
    //  CROUCHING
    // ─────────────────────────────────────────────
    [Header("Crouching")]
    [Tooltip("CharacterController height while crouching.")]
    public float crouchHeight = 1f;

    [Tooltip("CharacterController height while standing.")]
    public float standingHeight = 2f;

    [Tooltip("How fast the controller lerps between crouch and stand heights.")]
    public float crouchTransitionSpeed = 10f;

    [Tooltip("Layer mask used to check for ceilings when trying to stand up.")]
    public LayerMask ceilingMask = ~0;

    // ─────────────────────────────────────────────
    //  WEIGHT / CARRY
    // ─────────────────────────────────────────────
    [Header("Carry Weight")]
    [Tooltip("Den vægt (kg) hvor spilleren er fuldstændig bremset (hastighed = 0).\n" +
             "Bruges som reference i PickupObject. Prøv en værdi omkring 20.")]
    public float maxCarryWeight = 20f;

    /// <summary>
    /// Sættes automatisk af PickupObject når man samler noget op (0–1).
    /// 1 = fuld hastighed, 0 = ingen hastighed.
    /// </summary>
    [HideInInspector]
    public float weightMultiplier = 1f;

    // ─────────────────────────────────────────────
    //  HEALTH
    // ─────────────────────────────────────────────
    [Header("Health")]
    [Tooltip("Spillerens maksimale liv.")]
    public float maxHealth = 100f;

    [Tooltip("Sæt til true for at aktivere regenerering af liv over tid.")]
    public bool enableHealthRegen = false;

    [Tooltip("Hvor meget liv der regenereres pr. sekund.")]
    public float healthRegenRate = 2f;

    [Tooltip("Sekunder uden skade før regenerering begynder.")]
    public float regenDelay = 5f;

    [Tooltip("Hvor lang tid spilleren er låst fast i death-tilstand før respawn/game over. Sæt til 0 for øjeblikkelig.")]
    public float deathDuration = 3f;

    [Tooltip("Kaldes når spilleren tager skade. Sender (nuværende liv, max liv).")]
    public UnityEvent<float, float> onDamaged;

    [Tooltip("Kaldes når spilleren bliver helbredt. Sender (nuværende liv, max liv).")]
    public UnityEvent<float, float> onHealed;

    [Tooltip("Kaldes én gang når spilleren dør.")]
    public UnityEvent onDeath;

    [Tooltip("Kaldes når respawn-logik skal køre (efter deathDuration).")]
    public UnityEvent onRespawn;

    // ─────────────────────────────────────────────
    //  CAMERA / LOOK
    // ─────────────────────────────────────────────
    [Header("Camera Look")]
    [Tooltip("Assign the Camera (or a child pivot) here.")]
    public Transform cameraTransform;

    [Tooltip("Mouse sensitivity on the X axis (horizontal turn).")]
    public float sensitivityX = 2f;

    [Tooltip("Mouse sensitivity on the Y axis (vertical look).")]
    public float sensitivityY = 2f;

    [Tooltip("Clamp how far up the camera can look (degrees).")]
    public float lookUpLimit = 80f;

    [Tooltip("Clamp how far down the camera can look (degrees).")]
    public float lookDownLimit = 80f;

    [Tooltip("Invert the vertical look axis.")]
    public bool invertY = false;

    // ─────────────────────────────────────────────
    //  HEAD BOB  (optional)
    // ─────────────────────────────────────────────
    [Header("Head Bob (optional)")]
    [Tooltip("Enable subtle camera bobbing while moving.")]
    public bool enableHeadBob = true;

    [Tooltip("How fast the bob cycle runs.")]
    public float bobFrequency = 5f;

    [Tooltip("Vertical amplitude of the bob.")]
    public float bobAmplitudeY = 0.05f;

    [Tooltip("Horizontal amplitude of the bob.")]
    public float bobAmplitudeX = 0.025f;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    private CharacterController _cc;
    private Vector3 _velocity;          // current move velocity (world space)
    private float _verticalVelocity;    // Y component handled separately
    private float _xRotation;           // camera pitch tracker
    private float _bobTimer;
    private Vector3 _cameraLocalOrigin;
    private bool _isCrouching;
    private float _targetHeight;

    // Health state
    private float _currentHealth;
    private bool _isDead;
    private float _timeSinceLastDamage;
    private float _deathTimer;

    // ─────────────────────────────────────────────
    //  PUBLIC PROPERTIES
    // ─────────────────────────────────────────────

    /// <summary>Spillerens nuværende liv (read-only udefra).</summary>
    public float CurrentHealth => _currentHealth;

    /// <summary>Er spilleren død?</summary>
    public bool IsDead => _isDead;

    /// <summary>Liv som en 0–1 værdi, nyttig til UI-healthbar.</summary>
    public float HealthPercent => _currentHealth / maxHealth;

    // ─────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────
    void Start()
    {
        _cc = GetComponent<CharacterController>();
        _targetHeight = standingHeight;

        _currentHealth = maxHealth;
        _isDead = false;
        _timeSinceLastDamage = 0f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
            _cameraLocalOrigin = cameraTransform.localPosition;
    }

    // ─────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────
    void Update()
    {
        if (_isDead)
        {
            HandleDeath();
            return;
        }

        HandleLook();
        HandleCrouch();
        HandleMovement();
        if (enableHeadBob) HandleHeadBob();
        if (enableHealthRegen) HandleHealthRegen();
    }

    // ─────────────────────────────────────────────
    //  HEALTH SYSTEM
    // ─────────────────────────────────────────────

    /// <summary>
    /// Giv spilleren skade. Kaldes typisk fra et enemy-script:
    /// <code>player.TakeDamage(25f);</code>
    /// </summary>
    /// <param name="amount">Positiv mængde skade.</param>
    public void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Max(_currentHealth - amount, 0f);
        _timeSinceLastDamage = 0f;

        onDamaged?.Invoke(_currentHealth, maxHealth);

        if (_currentHealth <= 0f)
            Die();
    }

    /// <summary>
    /// Helbred spilleren.
    /// </summary>
    /// <param name="amount">Positiv mængde liv der gendannes.</param>
    public void Heal(float amount)
    {
        if (_isDead || amount <= 0f) return;

        _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
        onHealed?.Invoke(_currentHealth, maxHealth);
    }

    /// <summary>
    /// Dræb spilleren øjeblikkeligt, uanset nuværende liv.
    /// </summary>
    public void KillInstantly()
    {
        if (_isDead) return;
        _currentHealth = 0f;
        Die();
    }

    void Die()
    {
        _isDead = true;
        _deathTimer = 0f;

        // Stop al bevægelse
        _velocity = Vector3.zero;
        _verticalVelocity = 0f;

        onDeath?.Invoke();
    }

    void HandleDeath()
    {
        // Tæl ned – herefter kaldes onRespawn så du kan loade en scene, vise game over, osv.
        _deathTimer += Time.deltaTime;
        if (_deathTimer >= deathDuration)
        {
            onRespawn?.Invoke();
        }
    }

    void HandleHealthRegen()
    {
        _timeSinceLastDamage += Time.deltaTime;

        if (_timeSinceLastDamage >= regenDelay && _currentHealth < maxHealth)
        {
            Heal(healthRegenRate * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────
    //  MOUSE LOOK
    // ─────────────────────────────────────────────
    void HandleLook()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * sensitivityY * (invertY ? 1f : -1f);

        _xRotation = Mathf.Clamp(_xRotation + mouseY, -lookUpLimit, lookDownLimit);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    // ─────────────────────────────────────────────
    //  CROUCH
    // ─────────────────────────────────────────────
    void HandleCrouch()
    {
        bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

        // Try to stand up: check for ceiling
        if (_isCrouching && !wantsCrouch)
        {
            Vector3 top = transform.position + Vector3.up * (standingHeight * 0.5f);
            if (!Physics.CheckSphere(top, _cc.radius * 0.9f, ceilingMask))
            {
                _isCrouching = false;
                _targetHeight = standingHeight;
            }
        }
        else if (!_isCrouching && wantsCrouch)
        {
            _isCrouching = true;
            _targetHeight = crouchHeight;
        }

        // Smoothly resize the CharacterController
        float newHeight = Mathf.Lerp(_cc.height, _targetHeight, Time.deltaTime * crouchTransitionSpeed);
        float oldHeight = _cc.height;

        _cc.height = newHeight;

        // flyt center kun halvdelen af ændringen
        float centerOffset = (newHeight - oldHeight) / 2f;
        _cc.center += new Vector3(0f, centerOffset, 0f);

        // Move the camera with the height change so it doesn't clip into the ceiling
        if (cameraTransform != null)
        {
            Vector3 camPos = _cameraLocalOrigin;
            camPos.y += (_cc.height - standingHeight);          // offset relative to standing
            _cameraLocalOrigin = Vector3.Lerp(_cameraLocalOrigin, camPos, Time.deltaTime * crouchTransitionSpeed);
        }
    }

    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    void HandleMovement()
    {
        bool isGrounded = _cc.isGrounded;

        // ── Gravity ──────────────────────────────
        if (isGrounded)
        {
            _verticalVelocity = groundedGravity;
        }
        else
        {
            _verticalVelocity = Mathf.Max(
                _verticalVelocity + fallGravity * Time.deltaTime,
                maxFallSpeed
            );
        }

        // ── Input ─────────────────────────────────
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.sqrMagnitude > 1f)
            inputDir.Normalize();

        // Translate input from local to world space
        Vector3 worldInput = transform.TransformDirection(inputDir);

        // ── Target Speed ──────────────────────────
        float targetSpeed = walkSpeed;
        if (_isCrouching)
            targetSpeed = crouchSpeed;
        else if (Input.GetKey(KeyCode.LeftShift))
            targetSpeed = sprintSpeed;

        Vector3 targetVelocity = worldInput * targetSpeed * weightMultiplier;

        // ── Accelerate / Decelerate ───────────────
        float rate = inputDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        _velocity = Vector3.MoveTowards(_velocity, targetVelocity, rate * Time.deltaTime);

        // ── Apply ─────────────────────────────────
        Vector3 move = _velocity + Vector3.up * _verticalVelocity;
        _cc.Move(move * Time.deltaTime);
    }

    // ─────────────────────────────────────────────
    //  HEAD BOB
    // ─────────────────────────────────────────────
    void HandleHeadBob()
    {
        if (cameraTransform == null) return;

        bool isMoving = _velocity.sqrMagnitude > 0.1f && _cc.isGrounded;

        if (isMoving)
        {
            _bobTimer += Time.deltaTime * bobFrequency;
            float bobY = Mathf.Sin(_bobTimer) * bobAmplitudeY;
            float bobX = Mathf.Sin(_bobTimer * 0.5f) * bobAmplitudeX;

            cameraTransform.localPosition = _cameraLocalOrigin + new Vector3(bobX, bobY, 0f);
        }
        else
        {
            // Smoothly return to rest
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                _cameraLocalOrigin,
                Time.deltaTime * bobFrequency
            );
        }
    }

    // ─────────────────────────────────────────────
    //  GIZMOS  (editor visualisation)
    // ─────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_cc == null) _cc = GetComponent<CharacterController>();
        if (_cc == null) return;

        // Draw ceiling check sphere
        Gizmos.color = Color.yellow;
        Vector3 top = transform.position + Vector3.up * (standingHeight * 0.9f);
        Gizmos.DrawWireSphere(top, _cc.radius * 0.9f);
    }
#endif
}