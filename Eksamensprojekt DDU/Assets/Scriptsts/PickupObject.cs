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

    [Tooltip("Hvor hurtigt kassen følger med til mål-positionen.\nHøjere = strammere/hurtigere. Lavere = mere svævende/tung følelse.\nPrøv 8-15 for en naturlig følelse.")]
    public float followSpeed = 10f;

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


    [Header("Skanner")]
    [Tooltip("ScannerAnimator-komponenten på Scanner-objektet.\nSkanneren glider ned/op når dette objekt samles op/sættes ned.")]
    public ScannerAnimator scannerAnimator;

    // ── Private state ──────────────────────────────────────────────
    private bool _isHeld;
    private Rigidbody _rb;
    private Camera _cam;
    private int _originalLayer;
    private PlayerMovement _player;
    private const string HeldLayerName = "HeldObject";
    private RigidbodyInterpolation _originalInterpolation;
    private Scannable _scannable;

    /// <summary>True hvis spilleren pt. holder et objekt. Bruges af ScannerDisplay.</summary>
    public static bool IsHoldingItem { get; private set; }

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _cam = Camera.main;
        // Søg også i children — Scannable kan sidde på model-child under PickupObject
        _scannable = GetComponent<Scannable>() ?? GetComponentInChildren<Scannable>();

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

    // FixedUpdate er ikke nødvendig — pakken er kinematic mens den holdes
    // og velocity håndteres ikke via fysikken under hold

    void LateUpdate()
    {
        if (!_isHeld) return;

        // ── Position ──────────────────────────────────────────────
        // Vi bruger transform.position direkte i stedet for MovePosition
        // så bevægelsen er fuldt synkroniseret med kameraet hvert frame
        // uden at gå igennem fysikmotorens interpolation.
        Vector3 targetPos = _cam.transform.position
                          + _cam.transform.forward * holdDistance
                          + _cam.transform.right * holdOffset.x
                          + _cam.transform.up * holdOffset.y
                          + _cam.transform.forward * holdOffset.z;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );

        // ── Rotation ──────────────────────────────────────────────
        // Kun kameraets vandrette drejning (yaw) bruges — pitch ignoreres
        // så kassen ikke tipper når man kigger op/ned.
        float cameraYaw = _cam.transform.eulerAngles.y;
        Quaternion yawRot = Quaternion.Euler(0f, cameraYaw + yawOffset, 0f);
        Quaternion upright = Quaternion.Euler(uprightOffset);
        Quaternion targetRot = yawRot * upright;

        transform.rotation = targetRot;
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
        IsHoldingItem = true;

        // Sæt kinematic så fysikken ikke kæmper mod vores transform-opdateringer
        _originalInterpolation = _rb.interpolation;
        _rb.isKinematic = true;
        _rb.interpolation = RigidbodyInterpolation.None;
        _rb.useGravity = false;
        _rb.freezeRotation = false;

        int heldLayer = LayerMask.NameToLayer(HeldLayerName);
        if (heldLayer != -1)
            gameObject.layer = heldLayer;

        if (_player != null)
        {
            // Heavy pakker bruger dobbelt vægtstraf
            float effectiveWeight = (_scannable != null && _scannable.isHeavy)
                ? weight * 2f
                : weight;
            _player.weightMultiplier = Mathf.Max(0f, 1f - effectiveWeight / (_player.maxCarryWeight + effectiveWeight));
        }

        scannerAnimator?.Hide();

        Debug.Log($"[PickupObject] Samlede op: {gameObject.name} ({weight} kg)");
    }

    void PutDown()
    {
        _isHeld = false;
        IsHoldingItem = false;

        // Gendan fysik
        _rb.isKinematic = false;
        _rb.interpolation = _originalInterpolation;
        _rb.useGravity = true;
        _rb.freezeRotation = false;
        _rb.angularVelocity = Vector3.zero;
        _rb.linearVelocity = Vector3.down * 0.5f;
        gameObject.layer = _originalLayer;

        if (_player != null)
            _player.weightMultiplier = 1f;

        scannerAnimator?.Show();

        Debug.Log($"[PickupObject] Satte ned: {gameObject.name}");
    }

    // ── Fragile collision ─────────────────────────────────────────

    /// <summary>
    /// Tjekker om pakken overlapper med en afleveringszone eller spawnzone.
    /// Bruges til at undgå straf når pakken sættes ned på et sikkert sted.
    /// </summary>
    bool IsInsideSafeZone()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return false;

        Collider[] hits = Physics.OverlapBox(
            col.bounds.center,
            col.bounds.extents,
            transform.rotation
        );

        foreach (var hit in hits)
        {
            if (!hit.isTrigger) continue;
            if (hit.GetComponent<DeliveryZone>() != null) return true;
            if (hit.GetComponent<SpawnZone>() != null) return true;
        }

        return false;
    }

    /// <summary>
    /// Trækker fragile-straf fra hvis alle betingelser er opfyldt.
    /// </summary>
    void ApplyFragilePenalty()
    {
        if (_scannable == null || !_scannable.isFragile) return;
        if (_scannable.fragileDropPenalty <= 0) return;
        if (IsInsideSafeZone()) return;

        int penalty = _scannable.fragileDropPenalty;

        // Find den ordre der tilhører præcis denne pakke-instans
        // Ordren behøver ikke være aktiv endnu — straffen akkumuleres uanset hvornår pakken tabes
        Order order = OrderManager.Instance?.FindOrderForPackage(_scannable);
        if (order != null)
        {
            order.penaltiesAccrued += penalty;
            Debug.Log($"[PickupObject] Fragile pakke ramte gulvet — -{penalty} point akkumuleret (total straf: {order.penaltiesAccrued})");
        }
        else
        {
            Debug.Log($"[PickupObject] Fragile pakke ramte gulvet — ingen matchende ordre fundet for {_scannable.itemID}");
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_isHeld) return;
        if (_scannable == null || !_scannable.isFragile) return;
        if (collision.gameObject.layer != 0) return;

        ApplyFragilePenalty();
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