using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
    public float deceleration = 60f;

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
    [Tooltip("Den vaegt (kg) hvor spilleren er fuldstaendig bremset.")]
    public float maxCarryWeight = 20f;

    [HideInInspector]
    public float weightMultiplier = 1f;

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
    //  SKADE OG HELBRED
    // ─────────────────────────────────────────────
    [Header("Skade og helbred")]
    [Tooltip("Global Volume i scenen med en Vignette override.")]
    public Volume globalVolume;

    [Tooltip("Sekunder den roede vignette fader ud igen efter foerste hit.")]
    public float vignetteRecoverTime = 5f;

    // ─────────────────────────────────────────────
    //  HIDING LOOK CLAMP
    // ─────────────────────────────────────────────
    [Header("Hiding Look Clamp")]
    [Tooltip("Maksimal vinkel spilleren kan se til siden inde i kassen (grader).")]
    public float boxLookAngleLimit = 40f;

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────
    private CharacterController _cc;
    private Vector3 _velocity;
    private float _verticalVelocity;
    private float _xRotation;
    private float _bobTimer;
    private Vector3 _cameraLocalOrigin;
    private bool _isCrouching;
    private float _targetHeight;

    public bool IsMapLocked { get; private set; }
    public bool IsHiding { get; private set; }
    public bool IsDamaged { get; private set; }
    public bool IsDead { get; private set; }

    private Vector3 _boxExitDirection = Vector3.forward;
    private Vignette _vignette;
    private Coroutine _vignetteCoroutine;

    // ─────────────────────────────────────────────
    //  INIT
    // ─────────────────────────────────────────────
    void Start()
    {
        _cc = GetComponent<CharacterController>();
        _targetHeight = standingHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform != null)
            _cameraLocalOrigin = cameraTransform.localPosition;

        if (globalVolume != null && globalVolume.profile.TryGet(out Vignette v))
        {
            _vignette = v;
            _vignette.intensity.Override(0f);
        }
    }

    // ─────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────
    void Update()
    {
        if (IsDead || IsMapLocked) return;

        HandleLook();

        if (IsHiding) return;

        HandleCrouch();
        HandleMovement();
        if (enableHeadBob) HandleHeadBob();
    }

    // ─────────────────────────────────────────────
    //  MAP LOCK API
    // ─────────────────────────────────────────────
    public void SetMapLock(bool locked)
    {
        IsMapLocked = locked;
        if (locked)
        {
            _velocity = Vector3.zero;
            _verticalVelocity = groundedGravity;
        }
    }

    // ─────────────────────────────────────────────
    //  HIDING API
    // ─────────────────────────────────────────────
    public float GetCurrentSpeed() => new Vector3(_velocity.x, 0f, _velocity.z).magnitude;

    public void SetHiding(bool hiding, Vector3 exitDirection = default)
    {
        IsHiding = hiding;

        if (hiding)
        {
            _boxExitDirection = exitDirection == Vector3.zero
                ? Vector3.forward
                : new Vector3(exitDirection.x, 0f, exitDirection.z).normalized;

            if (_boxExitDirection != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(_boxExitDirection);

            _velocity = Vector3.zero;
            _verticalVelocity = groundedGravity;
        }
    }

    // ─────────────────────────────────────────────
    //  DAMAGE API  <- kaldes af BlindSorter
    // ─────────────────────────────────────────────
    /// <summary>
    /// Foerste hit: roed vignette, spilleren er skadet.
    /// Andet hit mens IsDamaged er true: spilleren doer.
    /// </summary>
    public void TakeDamage()
    {
        if (IsDead) return;

        if (!IsDamaged)
        {
            IsDamaged = true;
            if (_vignetteCoroutine != null) StopCoroutine(_vignetteCoroutine);
            _vignetteCoroutine = StartCoroutine(DamageVignetteCoroutine());
        }
        else
        {
            Die();
        }
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        _velocity = Vector3.zero;
        _verticalVelocity = 0f;

        if (_vignetteCoroutine != null) StopCoroutine(_vignetteCoroutine);
        _vignetteCoroutine = StartCoroutine(DeathVignetteCoroutine());
    }

    // ─────────────────────────────────────────────
    //  VIGNETTE COROUTINES
    // ─────────────────────────────────────────────
    IEnumerator DamageVignetteCoroutine()
    {
        yield return LerpVignette(0.6f, 6f);
        yield return new WaitForSeconds(0.3f);
        yield return LerpVignette(0f, 0.6f / vignetteRecoverTime);
        IsDamaged = false;
    }

    IEnumerator DeathVignetteCoroutine()
    {
        yield return LerpVignette(0.9f, 1.5f);
    }

    IEnumerator LerpVignette(float target, float speed)
    {
        if (_vignette == null) yield break;

        while (!Mathf.Approximately(_vignette.intensity.value, target))
        {
            _vignette.intensity.Override(
                Mathf.MoveTowards(_vignette.intensity.value, target, speed * Time.deltaTime));
            yield return null;
        }
        _vignette.intensity.Override(target);
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

        if (IsHiding)
        {
            Quaternion nextRot = transform.rotation * Quaternion.Euler(0f, mouseX, 0f);
            Vector3 nextForward = nextRot * Vector3.forward;
            float angle = Vector3.SignedAngle(_boxExitDirection, nextForward, Vector3.up);

            if (Mathf.Abs(angle) <= boxLookAngleLimit)
                transform.Rotate(Vector3.up * mouseX);
        }
        else
        {
            transform.Rotate(Vector3.up * mouseX);
        }
    }

    // ─────────────────────────────────────────────
    //  CROUCH
    // ─────────────────────────────────────────────
    void HandleCrouch()
    {
        bool wantsCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);

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

        float newHeight = Mathf.Lerp(_cc.height, _targetHeight, Time.deltaTime * crouchTransitionSpeed);
        float oldHeight = _cc.height;
        _cc.height = newHeight;
        float centerOffset = (newHeight - oldHeight) / 2f;
        _cc.center += new Vector3(0f, centerOffset, 0f);

        if (cameraTransform != null)
        {
            float standingCamY = standingHeight - 0.15f;
            float crouchCamY = crouchHeight - 0.15f;
            float t = 1f - (_cc.height - crouchHeight) / (standingHeight - crouchHeight);
            float targetCamY = Mathf.Lerp(standingCamY, crouchCamY, t);

            Vector3 origin = _cameraLocalOrigin;
            origin.y = targetCamY;
            _cameraLocalOrigin = Vector3.Lerp(_cameraLocalOrigin, origin, Time.deltaTime * crouchTransitionSpeed);

            Vector3 camPos = cameraTransform.localPosition;
            camPos.y = _cameraLocalOrigin.y;
            cameraTransform.localPosition = camPos;
        }
    }

    // ─────────────────────────────────────────────
    //  MOVEMENT
    // ─────────────────────────────────────────────
    void HandleMovement()
    {
        bool isGrounded = _cc.isGrounded;

        if (isGrounded)
            _verticalVelocity = groundedGravity;
        else
            _verticalVelocity = Mathf.Max(_verticalVelocity + fallGravity * Time.deltaTime, maxFallSpeed);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        Vector3 worldInput = transform.TransformDirection(inputDir);

        float targetSpeed = walkSpeed;
        if (_isCrouching)
            targetSpeed = crouchSpeed;
        else if (Input.GetKey(KeyCode.LeftShift))
            targetSpeed = sprintSpeed;

        Vector3 targetVelocity = worldInput * targetSpeed * weightMultiplier;

        float rate = inputDir.sqrMagnitude > 0.01f ? acceleration : deceleration;
        _velocity = Vector3.MoveTowards(_velocity, targetVelocity, rate * Time.deltaTime);

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
            cameraTransform.localPosition = Vector3.Lerp(
                cameraTransform.localPosition,
                _cameraLocalOrigin,
                Time.deltaTime * bobFrequency
            );
        }
    }

    // ─────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (_cc == null) _cc = GetComponent<CharacterController>();
        if (_cc == null) return;

        Gizmos.color = Color.yellow;
        Vector3 top = transform.position + Vector3.up * (standingHeight * 0.9f);
        Gizmos.DrawWireSphere(top, _cc.radius * 0.9f);
    }
#endif
}