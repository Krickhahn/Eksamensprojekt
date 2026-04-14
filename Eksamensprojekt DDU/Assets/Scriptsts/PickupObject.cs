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
///
/// LYD:
///   Tilføj en AudioSource-komponent til dette GameObject (Play On Awake: OFF, Loop: OFF).
///   Fyld lyd-arrays med dine clips i Inspector — scriptet vælger tilfældigt blandt dem.
///   Pakke-type lydene spilles kort efter pickup- og putdown-lyden (packageTypeSoundDelay sekunder).
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

    // ── Lyd ───────────────────────────────────────────────────────

    [Header("Lyd — Opsamling")]
    [Tooltip("Lyde der kan afspilles når pakken samles op. Én vælges tilfældigt.\n" +
             "Kræver en AudioSource-komponent på dette GameObject.")]
    public AudioClip[] pickupSounds;

    [Tooltip("Lydstyrke for opsamlings-lyde (0–1).")]
    [Range(0f, 1f)]
    public float pickupVolume = 1f;

    [Header("Lyd — Sætning ned")]
    [Tooltip("Lyde der kan afspilles når pakken sættes ned. Én vælges tilfældigt.")]
    public AudioClip[] putdownSounds;

    [Tooltip("Lydstyrke for sætning-ned-lyde (0–1).")]
    [Range(0f, 1f)]
    public float putdownVolume = 1f;

    [Header("Lyd — Pakke-type (opsamling)")]
    [Tooltip("Lyde der afspilles ved opsamling af en Standard-pakke. Én vælges tilfældigt.\n" +
             "Spilles kort efter pickup-lyden — brug fx en neutral kvittering.")]
    public AudioClip[] standardPickupTypeSounds;

    [Tooltip("Lyde der afspilles ved opsamling af en Fragile-pakke. Én vælges tilfældigt.\n" +
             "Brug fx en advarselstone eller en forsigtig lyd.")]
    public AudioClip[] fragilePickupTypeSounds;

    [Tooltip("Lyde der afspilles ved opsamling af en Heavy-pakke. Én vælges tilfældigt.\n" +
             "Brug fx et grunt eller en tung lyd.")]
    public AudioClip[] heavyPickupTypeSounds;

    [Header("Lyd — Pakke-type (sætning ned)")]
    [Tooltip("Lyde der afspilles når en Standard-pakke sættes ned. Én vælges tilfældigt.")]
    public AudioClip[] standardPutdownTypeSounds;

    [Tooltip("Lyde der afspilles når en Fragile-pakke sættes ned. Én vælges tilfældigt.\n" +
             "Brug fx en lettelsens suk eller en forsigtig lyd.")]
    public AudioClip[] fragilePutdownTypeSounds;

    [Tooltip("Lyde der afspilles når en Heavy-pakke sættes ned. Én vælges tilfældigt.\n" +
             "Brug fx et tungt bump eller en aflastningslyd.")]
    public AudioClip[] heavyPutdownTypeSounds;

    [Tooltip("Forsinkelse i sekunder før pakke-type lyden afspilles efter pickup- eller putdown-lyden.\n" +
             "0 = spilles samtidig.")]
    public float packageTypeSoundDelay = 0.2f;

    [Tooltip("Lydstyrke for pakke-type lyde (0–1).")]
    [Range(0f, 1f)]
    public float packageTypeVolume = 0.85f;

    // ── Private state ──────────────────────────────────────────────
    private bool _isHeld;
    private Rigidbody _rb;
    private Camera _cam;
    private int _originalLayer;
    private PlayerMovement _player;
    private const string HeldLayerName = "HeldObject";
    private RigidbodyInterpolation _originalInterpolation;
    private Scannable _scannable;
    private AudioSource _audio;

    /// <summary>True hvis spilleren pt. holder et objekt. Bruges af ScannerDisplay.</summary>
    public static bool IsHoldingItem { get; private set; }

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _cam = Camera.main;
        _scannable = GetComponent<Scannable>()
                  ?? GetComponentInChildren<Scannable>()
                  ?? GetComponentInParent<Scannable>();

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.spatialBlend = 1f; // 3D-lyd — ændres til 0 for 2D
        }

        if (_cam == null)
            Debug.LogWarning("[PickupObject] Intet kamera med 'MainCamera'-tag fundet!");

        _originalLayer = gameObject.layer;

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

    void LateUpdate()
    {
        if (!_isHeld) return;

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

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                Pickup();
        }
    }

    void Pickup()
    {
        _isHeld = true;
        IsHoldingItem = true;

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
            float effectiveWeight = (_scannable != null && _scannable.isHeavy)
                ? weight * 2f
                : weight;
            _player.weightMultiplier = Mathf.Max(0f, 1f - effectiveWeight / (_player.maxCarryWeight + effectiveWeight));
        }

        scannerAnimator?.Hide();

        // ── Lyd ───────────────────────────────────────────────────
        PlayRandomSound(pickupSounds, pickupVolume);
        StartCoroutine(PlayPackageTypeSoundDelayed(isPickup: true));

        Debug.Log($"[PickupObject] Samlede op: {gameObject.name} ({weight} kg)");
    }

    void PutDown()
    {
        _isHeld = false;
        IsHoldingItem = false;

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

        // ── Lyd ───────────────────────────────────────────────────
        PlayRandomSound(putdownSounds, putdownVolume);
        StartCoroutine(PlayPackageTypeSoundDelayed(isPickup: false));

        Debug.Log($"[PickupObject] Satte ned: {gameObject.name}");
    }

    // ── Lyd-hjælpere ──────────────────────────────────────────────

    /// <summary>Spiller en tilfældig clip fra arrayet, hvis det ikke er tomt.</summary>
    void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (_audio == null || clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
            _audio.PlayOneShot(clip, volume);
    }

    /// <summary>
    /// Venter <see cref="packageTypeSoundDelay"/> sekunder og spiller derefter
    /// den pakke-type-specifikke lyd for enten opsamling eller sætning ned.
    /// </summary>
    System.Collections.IEnumerator PlayPackageTypeSoundDelayed(bool isPickup)
    {
        if (packageTypeSoundDelay > 0f)
            yield return new WaitForSeconds(packageTypeSoundDelay);

        if (_scannable == null) yield break;

        AudioClip[] typeSounds = isPickup
            ? _scannable.packageType switch
            {
                PackageType.Fragile => fragilePickupTypeSounds,
                PackageType.Heavy => heavyPickupTypeSounds,
                _ => standardPickupTypeSounds,
            }
            : _scannable.packageType switch
            {
                PackageType.Fragile => fragilePutdownTypeSounds,
                PackageType.Heavy => heavyPutdownTypeSounds,
                _ => standardPutdownTypeSounds,
            };

        PlayRandomSound(typeSounds, packageTypeVolume);
    }

    // ── Fragile collision ─────────────────────────────────────────

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

    void ApplyFragilePenalty()
    {
        if (_scannable == null || !_scannable.isFragile) return;
        if (_scannable.fragileDropPenalty <= 0) return;
        if (IsInsideSafeZone()) return;

        int penalty = _scannable.fragileDropPenalty;

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

        if (_scannable == null)
            _scannable = GetComponent<Scannable>() ?? GetComponentInChildren<Scannable>()
                      ?? GetComponentInParent<Scannable>();

        if (_scannable == null || !_scannable.isFragile) return;
        if (collision.gameObject.layer != 0) return;

        Debug.Log($"[PickupObject] OnCollisionEnter — fragile pakke '{gameObject.name}' ramte '{collision.gameObject.name}' (layer {collision.gameObject.layer})");
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