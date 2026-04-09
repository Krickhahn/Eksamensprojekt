using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MapInspector – hæng dette script på kortets GameObject (fx et Quad eller Sprite).
///
/// OPSÆTNING:
/// 1. Opret et Canvas (Screen Space – Overlay) til overlay-baggrunden.
/// 2. Tilføj et Image-element under Canvas som dimOverlay (sort, alpha 0 til start).
/// 3. Sæt kortets Transform som whiteboardTransform (dets normale placering på whiteboardet).
/// 4. Assign playerCamera (Main Camera) og playerMovement.
/// 5. Sørg for at kortets GameObject har en Collider (fx MeshCollider eller BoxCollider).
/// 6. Juster inspectRange, mapViewDistance og mapViewRotationOffset efter smag.
/// </summary>
public class MapInspector : MonoBehaviour
{
    [Header("Referencer")]
    [Tooltip("Kameraet der bruges til at vise kortet tæt på")]
    public Camera playerCamera;

    [Tooltip("PlayerMovement-scriptet – bruges til at låse bevægelse og kamera under inspektion")]
    public PlayerMovement playerMovement;

    [Tooltip("Image-elementet der bruges til at mørklægge baggrunden (sæt alpha til 0 i start)")]
    public Image dimOverlay;

    [Header("Whiteboard-placering")]
    [Tooltip("Den transform kortet normalt sidder på (whiteboardets position/rotation)")]
    public Transform whiteboardTransform;

    [Header("Inspektion")]
    [Tooltip("Maksimal afstand (meter) raycastet når ud — spilleren skal kigge direkte på kortet inden for denne afstand")]
    public float inspectRange = 3f;

    [Tooltip("Hvilke lag raycastet må ramme. Sæt dette til kun kortets lag for at undgå falske hits.")]
    public LayerMask inspectLayerMask = ~0;

    [Tooltip("Valgfrit: vis en prompt i UI når spilleren kigger på kortet")]
    public Text promptText;

    [Tooltip("Tekst der vises i prompten")]
    public string promptMessage = "[E] Se kortet";

    [Header("Kamera-visning")]
    [Tooltip("Afstand foran kameraet når kortet vises")]
    public float mapViewDistance = 1.5f;

    [Tooltip("Offset fra kameraets center (x = højre/venstre, y = op/ned)")]
    public Vector3 mapViewOffset = new Vector3(0f, 0f, 0f);

    [Tooltip("Skalering af kortet når det er tæt på kameraet")]
    public Vector3 mapViewScale = new Vector3(0.4f, 0.3f, 1f);

    [Tooltip("Ekstra rotation oven på kamera-retningen (Euler-grader). " +
             "Brug fx (0, 180, 0) hvis kortet vender forkert, eller (0, 0, 90) for at rotere 90° med uret.")]
    public Vector3 mapViewRotationOffset = Vector3.zero;

    [Header("Animation")]
    [Tooltip("Hastighed på lerp-animationen (højere = hurtigere)")]
    [Range(1f, 20f)]
    public float animationSpeed = 6f;

    [Tooltip("Maksimal alpha på dim-overlay (0–1)")]
    [Range(0f, 1f)]
    public float maxDimAlpha = 0.6f;

    // ── Intern tilstand ──────────────────────────────────────────────
    private bool _isMapOpen = false;

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private Vector3 _targetScale;
    private float _targetDimAlpha;

    private Vector3 _whiteboardPosition;
    private Quaternion _whiteboardRotation;
    private Vector3 _whiteboardScale;

    private Vector3 _lockedCamPosition;
    private Quaternion _lockedCamRotation;

    // Cachedt collider til raycast-sammenligning
    private Collider _mapCollider;

    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        ValidateSetup();
        CacheWhiteboardTransform();
        SetDimAlpha(0f);

        _mapCollider = GetComponent<Collider>();
        if (_mapCollider == null)
            Debug.LogWarning("[MapInspector] Ingen Collider fundet på kortet – raycast-inspektion virker ikke. Tilføj en BoxCollider eller MeshCollider.");

        if (promptText != null)
        {
            promptText.text = promptMessage;
            promptText.enabled = false;
        }
    }

    private void Start()
    {
        transform.position = _whiteboardPosition;
        transform.rotation = _whiteboardRotation;
        transform.localScale = _whiteboardScale;

        _targetPosition = _whiteboardPosition;
        _targetRotation = _whiteboardRotation;
        _targetScale = _whiteboardScale;
        _targetDimAlpha = 0f;
    }

    private void Update()
    {
        bool lookingAtMap = !_isMapOpen && IsLookingAtMap();

        HandlePrompt(lookingAtMap);
        HandleInput(lookingAtMap);
        AnimateMap();

        // Lås kameraets position og retning mens kortet er åbent
        if (_isMapOpen && playerCamera != null)
        {
            playerCamera.transform.position = _lockedCamPosition;
            playerCamera.transform.rotation = Quaternion.Slerp(
                playerCamera.transform.rotation,
                _lockedCamRotation,
                animationSpeed * Time.deltaTime
            );
        }
    }

    // ── Raycast-tjek ─────────────────────────────────────────────────
    /// <summary>
    /// Sender en ray fra midten af kameraet. Returnerer true hvis den første
    /// collider der rammes tilhører dette kort, og afstanden er inden for inspectRange.
    /// Vægge og andre objekter imellem blokerer automatisk raycastet.
    /// </summary>
    private bool IsLookingAtMap()
    {
        if (playerCamera == null || _mapCollider == null) return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, inspectRange, inspectLayerMask))
            return hit.collider == _mapCollider;

        return false;
    }

    // ── Prompt ───────────────────────────────────────────────────────
    private void HandlePrompt(bool lookingAtMap)
    {
        if (promptText == null) return;
        promptText.enabled = lookingAtMap;
    }

    // ── Input ────────────────────────────────────────────────────────
    private void HandleInput(bool lookingAtMap)
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        if (_isMapOpen)
        {
            CloseMap();
            return;
        }

        if (lookingAtMap)
            OpenMap();
    }

    // ── Toggle ───────────────────────────────────────────────────────
    private void OpenMap()
    {
        _isMapOpen = true;

        _lockedCamPosition = playerCamera.transform.position;
        _lockedCamRotation = playerCamera.transform.rotation;

        if (playerMovement != null)
            playerMovement.SetMapLock(true);

        Vector3 camFwd = playerCamera.transform.forward;
        Vector3 camRight = playerCamera.transform.right;
        Vector3 camUp = playerCamera.transform.up;

        _targetPosition = _lockedCamPosition
                          + camFwd * mapViewDistance
                          + camRight * mapViewOffset.x
                          + camUp * mapViewOffset.y;

        Quaternion faceCamera = Quaternion.LookRotation(-camFwd, camUp);
        _targetRotation = faceCamera * Quaternion.Euler(mapViewRotationOffset);
        _targetScale = mapViewScale;
        _targetDimAlpha = maxDimAlpha;

        SetRenderOnTop(true);
    }

    private void CloseMap()
    {
        _isMapOpen = false;

        if (playerMovement != null)
            playerMovement.SetMapLock(false);

        CacheWhiteboardTransform();
        _targetPosition = _whiteboardPosition;
        _targetRotation = _whiteboardRotation;
        _targetScale = _whiteboardScale;
        _targetDimAlpha = 0f;

        SetRenderOnTop(false);
    }

    // ── Animation ────────────────────────────────────────────────────
    private void AnimateMap()
    {
        float t = animationSpeed * Time.deltaTime;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, _targetRotation, t);
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, t);

        if (dimOverlay != null)
            SetDimAlpha(Mathf.Lerp(dimOverlay.color.a, _targetDimAlpha, t));
    }

    // ── Hjælpemetoder ─────────────────────────────────────────────────
    private void CacheWhiteboardTransform()
    {
        if (whiteboardTransform != null)
        {
            _whiteboardPosition = whiteboardTransform.position;
            _whiteboardRotation = whiteboardTransform.rotation;
            _whiteboardScale = whiteboardTransform.localScale;
        }
        else
        {
            _whiteboardPosition = transform.position;
            _whiteboardRotation = transform.rotation;
            _whiteboardScale = transform.localScale;
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
            Debug.LogError("[MapInspector] Ingen kamera fundet! Assign playerCamera i Inspector.");

        if (playerMovement == null)
            Debug.LogWarning("[MapInspector] playerMovement er ikke sat – bevægelse låses ikke.");

        if (dimOverlay == null)
            Debug.LogWarning("[MapInspector] dimOverlay er ikke sat – baggrunden vil ikke blive mørkere.");

        if (whiteboardTransform == null)
            Debug.LogWarning("[MapInspector] whiteboardTransform er ikke sat – kortet bruger sin startposition.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        // Visualiser raycastet i Scene-view
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * inspectRange);
    }
#endif
}