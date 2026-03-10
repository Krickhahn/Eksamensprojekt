using UnityEngine;

/// <summary>
/// Sæt dette script på ethvert objekt der skal kunne samles op af spilleren.
///
/// OPSÆTNING:
///   1. Tilføj scriptet til en kasse/pakke (kræver Rigidbody + Collider).
///   2. Sæt dit kamera-objekt til tag "MainCamera" (samme kamera som i PlayerMovement).
///   3. (Anbefalet) Opret et Layer "HeldObject" og ignorer kollision med dit Player-layer
///      via Edit → Project Settings → Physics.
///
/// ROTATION:
///   Objektet holdes altid oprejst (verdensaksens Y er altid op).
///   Det roterer vandret med spilleren når du drejer til siderne,
///   men tipper ikke når du kigger op/ned.
///   Brug "Rotation Offset" til at justere hvilken side der vender fremad per objekt.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PickupObject : MonoBehaviour
{
    [Header("Interaktion")]
    [Tooltip("Maks afstand spilleren kan samle objektet op fra.")]
    public float pickupRange = 3f;

    [Tooltip("Tast til at samle op og sætte ned.")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Hold-position")]
    [Tooltip("Afstand foran kameraet objektet holdes.")]
    public float holdDistance = 1.5f;

    [Tooltip("Fin-juster position (x = side, y = op/ned, z = frem/tilbage ekstra).")]
    public Vector3 holdOffset = new Vector3(0f, -0.1f, 0f);

    [Header("Rotation")]
    [Tooltip("Ekstra Y-rotation (grader). Justér hvilken side af objektet der vender fremad.")]
    public float yawOffset = 0f;

    [Tooltip("Definér hvad 'oprejst' betyder for dette objekt (grader). " +
             "Justér X og Z hvis objektets model ikke står oprejst som standard. " +
             "Eksempel: X = 90 hvis objektets top-side er modellens forside (+Z).")]
    public Vector3 uprightOffset = Vector3.zero;

    [Header("Vægt")]
    [Tooltip("Kassens vægt (kg). Tunge kasser sænker spillerens hastighed.\n" +
             "0 = ingen effekt. Prøv værdier mellem 0 og 20.")]
    public float weight = 5f;

    // ── Private state ──────────────────────────────────────────────
    private bool _isHeld;
    private Rigidbody _rb;
    private Camera _cam;
    private int _originalLayer;
    private PlayerMovement _player;

    private const string HeldLayerName = "HeldObject";

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _cam = Camera.main;

        if (_cam == null)
            Debug.LogWarning("[PickupObject] Intet kamera med 'MainCamera'-tag fundet!");

        _originalLayer = gameObject.layer;

        // Find PlayerMovement via kameraets parent-hierarki
        if (_cam != null)
            _player = _cam.GetComponentInParent<PlayerMovement>();

        if (_player == null)
            Debug.LogWarning("[PickupObject] Fandt ikke PlayerMovement — vægt-effekt virker ikke.");
    }

    void Update()
    {
        if (_cam == null) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (_isHeld) PutDown();
            else TryPickup();
        }
    }

    void FixedUpdate()
    {
        // Fysik-steget bruges ikke til at flytte kassen mens den holdes.
        // Position og rotation håndteres i LateUpdate for glat bevægelse.
        if (!_isHeld) return;

        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    void LateUpdate()
    {
        if (!_isHeld) return;

        // ── Position ──────────────────────────────────────────────
        // LateUpdate kører efter Update (og efter kamera-bevægelse),
        // så kassen følger kameraet frame-perfekt uden hak.
        Vector3 targetPos = _cam.transform.position
                          + _cam.transform.forward * holdDistance
                          + _cam.transform.right * holdOffset.x
                          + _cam.transform.up * holdOffset.y
                          + _cam.transform.forward * holdOffset.z;

        _rb.MovePosition(targetPos);

        // ── Rotation ──────────────────────────────────────────────
        float cameraYaw = _cam.transform.eulerAngles.y;
        Quaternion upright = Quaternion.Euler(uprightOffset);
        Quaternion yawRot = Quaternion.Euler(0f, cameraYaw + yawOffset, 0f);
        Quaternion targetRot = yawRot * upright;

        _rb.MoveRotation(targetRot);
    }

    // ──────────────────────────────────────────────────────────────
    void TryPickup()
    {
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                Pickup();
        }
    }

    void Pickup()
    {
        _isHeld = true;

        _rb.useGravity = false;
        _rb.freezeRotation = false;

        int heldLayer = LayerMask.NameToLayer(HeldLayerName);
        if (heldLayer != -1)
            gameObject.layer = heldLayer;

        if (_player != null)
            _player.weightMultiplier = Mathf.Max(0f, 1f - weight / (_player.maxCarryWeight + weight));

        Debug.Log($"[PickupObject] Samlede op: {gameObject.name} ({weight} kg)");
    }

    void PutDown()
    {
        _isHeld = false;

        _rb.useGravity = true;
        _rb.freezeRotation = false;
        _rb.angularVelocity = Vector3.zero;
        _rb.linearVelocity = Vector3.down * 0.5f;
        gameObject.layer = _originalLayer;

        if (_player != null)
            _player.weightMultiplier = 1f;

        Debug.Log($"[PickupObject] Satte ned: {gameObject.name}");
    }

    // ── Editor-gizmo ──────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        Camera sceneCamera = Camera.main;
        if (sceneCamera != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(sceneCamera.transform.position,
                           sceneCamera.transform.forward * pickupRange);
        }
    }
}