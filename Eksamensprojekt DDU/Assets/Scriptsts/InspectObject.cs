using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InspectObject – generelt inspect-script til ethvert objekt spilleren skal kunne kigge nærmere på.
/// Fungerer med papirer, bøger, skilte, kort, billeder osv.
///
/// OPSÆTNING:
///   1. Tilføj dette script til det objekt der skal kunne inspiceres (fx et papir).
///   2. Sørg for at objektet har en Collider (fx BoxCollider) — bruges til raycast.
///   3. Opret et tomt GameObject som "RestTransform" på den position/rotation/scale
///      objektet normalt har i scenen, og assign det i Rest Transform-feltet.
///      (Alternativt lader du feltet stå tomt — så bruges objektets startposition.)
///   4. Assign playerCamera (Main Camera) og playerMovement.
///   5. Opret et Canvas (Screen Space – Overlay) med et sort Image-barn (alpha = 0)
///      og assign det som Dim Overlay.
///   6. (Valgfrit) Assign et UI Text-element som Prompt Text.
///   7. Juster Inspect Range, View Distance, View Scale og Rotation Offset i Inspector.
///
/// BRUG:
///   Kig på objektet og tryk E for at inspicere — objektet glider glat hen foran kameraet
///   og baggrunden mørklægges. Tryk E igen for at lægge det tilbage.
/// </summary>
public class InspectObject : MonoBehaviour
{
    [Header("Referencer")]
    [Tooltip("Spillerens kamera.")]
    public Camera playerCamera;

    [Tooltip("PlayerMovement-scriptet — låser bevægelse og kameralook under inspektion.")]
    public PlayerMovement playerMovement;

    [Tooltip("Sort UI Image der mørklægger baggrunden (alpha = 0 ved start).")]
    public Image dimOverlay;

    [Header("Hvileplads")]
    [Tooltip("Transform der definerer objektets normale position/rotation/scale i scenen.\n" +
             "Lad stå tomt for at bruge objektets egen startposition.")]
    public Transform restTransform;

    [Header("Inspektion")]
    [Tooltip("Maks afstand spilleren skal være fra objektet og kigge på det for at kunne inspicere.")]
    public float inspectRange = 3f;

    [Tooltip("Lag raycastet må ramme. Brug kun objektets eget lag for at undgå falske hits.")]
    public LayerMask inspectLayerMask = ~0;

    [Tooltip("Valgfri UI Text der vises når spilleren kigger på objektet.")]
    public Text promptText;

    [Tooltip("Tekst der vises i prompten.")]
    public string promptMessage = "[E] Inspicér";

    [Header("Visning foran kamera")]
    [Tooltip("Afstand foran kameraet objektet holdes.")]
    public float viewDistance = 1.5f;

    [Tooltip("Offset fra kameraets center (x = højre/venstre, y = op/ned).")]
    public Vector3 viewOffset = Vector3.zero;

    [Tooltip("Størrelse objektet skaleres til når det inspiceres.")]
    public Vector3 viewScale = new Vector3(0.4f, 0.3f, 1f);

    [Tooltip("Ekstra rotation oven på kamera-retningen (Euler-grader).\n" +
             "Brug fx (0, 180, 0) hvis bagsiden vender mod dig, eller (0, 0, 90) for at rotere 90°.")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Animation")]
    [Tooltip("Lerp-hastighed for animationen (højere = hurtigere).")]
    [Range(1f, 20f)]
    public float animationSpeed = 6f;

    [Tooltip("Maksimal alpha på dim-overlay (0–1).")]
    [Range(0f, 1f)]
    public float maxDimAlpha = 0.6f;

    // ── Intern tilstand ───────────────────────────────────────────
    private bool _isOpen = false;

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private Vector3 _targetScale;
    private float _targetDimAlpha;

    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private Vector3 _restScale;

    private Vector3 _lockedCamPosition;
    private Quaternion _lockedCamRotation;

    private Collider _col;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        ValidateSetup();
        CacheRestTransform();
        SetDimAlpha(0f);

        _col = GetComponent<Collider>();
        if (_col == null)
            Debug.LogWarning($"[InspectObject] '{gameObject.name}' har ingen Collider — raycast-inspektion virker ikke.");

        if (promptText != null)
        {
            promptText.text = promptMessage;
            promptText.enabled = false;
        }
    }

    private void Start()
    {
        // Placer objektet på hvilepladsen ved start
        transform.position = _restPosition;
        transform.rotation = _restRotation;
        transform.localScale = _restScale;

        _targetPosition = _restPosition;
        _targetRotation = _restRotation;
        _targetScale = _restScale;
        _targetDimAlpha = 0f;
    }

    private void Update()
    {
        bool lookingAt = !_isOpen && IsLookingAtObject();

        HandlePrompt(lookingAt);
        HandleInput(lookingAt);
        Animate();

        // Lås kameraets position og rotation mens objektet inspiceres
        if (_isOpen && playerCamera != null)
        {
            playerCamera.transform.position = _lockedCamPosition;
            playerCamera.transform.rotation = Quaternion.Slerp(
                playerCamera.transform.rotation,
                _lockedCamRotation,
                animationSpeed * Time.deltaTime
            );
        }
    }

    // ── Raycast ───────────────────────────────────────────────────
    private bool IsLookingAtObject()
    {
        if (playerCamera == null || _col == null) return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, inspectRange, inspectLayerMask, QueryTriggerInteraction.Ignore))
            return hit.collider == _col;

        return false;
    }

    // ── Prompt ────────────────────────────────────────────────────
    private void HandlePrompt(bool lookingAt)
    {
        if (promptText == null) return;
        promptText.enabled = lookingAt;
    }

    // ── Input ─────────────────────────────────────────────────────
    private void HandleInput(bool lookingAt)
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (_isOpen) { Close(); return; }
        if (lookingAt) Open();
    }

    // ── Åbn/luk ───────────────────────────────────────────────────
    private void Open()
    {
        _isOpen = true;

        // Gem kameraets tilstand så det kan låses
        _lockedCamPosition = playerCamera.transform.position;
        _lockedCamRotation = playerCamera.transform.rotation;

        if (playerMovement != null)
            playerMovement.SetMapLock(true);

        Vector3 camFwd = playerCamera.transform.forward;
        Vector3 camRight = playerCamera.transform.right;
        Vector3 camUp = playerCamera.transform.up;

        _targetPosition = _lockedCamPosition
                          + camFwd * viewDistance
                          + camRight * viewOffset.x
                          + camUp * viewOffset.y;

        _targetRotation = Quaternion.LookRotation(-camFwd, camUp)
                          * Quaternion.Euler(rotationOffset);

        _targetScale = viewScale;
        _targetDimAlpha = maxDimAlpha;

        SetRenderOnTop(true);
    }

    private void Close()
    {
        _isOpen = false;

        if (playerMovement != null)
            playerMovement.SetMapLock(false);

        CacheRestTransform();
        _targetPosition = _restPosition;
        _targetRotation = _restRotation;
        _targetScale = _restScale;
        _targetDimAlpha = 0f;

        SetRenderOnTop(false);
    }

    // ── Animation ─────────────────────────────────────────────────
    private void Animate()
    {
        float t = animationSpeed * Time.deltaTime;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, t);

        if (dimOverlay != null)
            SetDimAlpha(Mathf.Lerp(dimOverlay.color.a, _targetDimAlpha, t));
    }

    // ── Hjælpemetoder ─────────────────────────────────────────────
    private void CacheRestTransform()
    {
        if (restTransform != null)
        {
            _restPosition = restTransform.position;
            _restRotation = restTransform.rotation;
            _restScale = restTransform.localScale;
        }
        else
        {
            // Brug objektets egne værdier som fallback
            _restPosition = transform.position;
            _restRotation = transform.rotation;
            _restScale = transform.localScale;
        }
    }

    private void SetDimAlpha(float alpha)
    {
        if (dimOverlay == null) return;
        Color c = dimOverlay.color;
        c.a = alpha;
        dimOverlay.color = c;
        dimOverlay.raycastTarget = alpha > 0.01f;
    }

    private void SetRenderOnTop(bool onTop)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = onTop ? 999 : 0;

        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = onTop ? 999 : 0;
    }

    private void ValidateSetup()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            Debug.LogError($"[InspectObject] '{gameObject.name}': Ingen kamera fundet! Assign playerCamera.");

        if (playerMovement == null)
            Debug.LogWarning($"[InspectObject] '{gameObject.name}': playerMovement ikke sat — bevægelse låses ikke.");

        if (dimOverlay == null)
            Debug.LogWarning($"[InspectObject] '{gameObject.name}': dimOverlay ikke sat — ingen mørklægning.");

        if (restTransform == null)
            Debug.LogWarning($"[InspectObject] '{gameObject.name}': restTransform ikke sat — bruger startposition.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * inspectRange);

        // Vis hvilepladsen hvis restTransform er sat
        if (restTransform != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            Gizmos.DrawWireCube(restTransform.position, restTransform.localScale);
        }
    }
#endif
}