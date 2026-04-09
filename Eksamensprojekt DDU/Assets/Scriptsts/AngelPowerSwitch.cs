using UnityEngine;

/// <summary>
/// Stikkontakt der gendanner lyset efter et strømsvigt.
/// Har en cooldown så spilleren ikke kan aktivere den med det samme.
///
/// OPSÆTNING:
///   1. Tilføj dette script til stikkontaktens GameObject med en Collider.
///   2. Træk håndtaget ind i "switchHandle" — det roterer ned ved strømsvigt, op ved aktivering.
///   3. Træk det røde lys-objekt ind i "redLight" og det grønne i "greenLight".
///   4. Juster cooldownDuration — anbefalet 20-40 sekunder.
///   5. Juster handleDownAngle og handleUpAngle til at passe til dit model.
/// </summary>
public class AngelPowerSwitch : MonoBehaviour
{
    [Header("Interaktion")]
    [Tooltip("Tast til at aktivere stikkontakten.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Maks afstand spilleren kan aktivere kontakten fra.")]
    public float interactRange = 2f;

    [Header("Cooldown")]
    [Tooltip("Sekunder spilleren skal overleve inden kontakten kan aktiveres (20-40 anbefalet).")]
    public float cooldownDuration = 30f;

    [Header("Håndtag")]
    [Tooltip("Transform på håndtaget der roterer op/ned.")]
    public Transform switchHandle;

    [Tooltip("Lokal X-rotation (grader) når lyset er slukket — håndtag nede.")]
    public float handleDownAngle = -40f;

    [Tooltip("Lokal X-rotation (grader) når lyset er tændt — håndtag oppe.")]
    public float handleUpAngle = 40f;

    [Tooltip("Tid i sekunder håndtaget bruger på at rotere.")]
    public float handleAnimDuration = 0.3f;

    [Header("Indikatorlys")]
    [Tooltip("GameObject for det røde lys — vises når strømmen er slukket.")]
    public GameObject redLight;

    [Tooltip("GameObject for det grønne lys — vises når strømmen er tændt.")]
    public GameObject greenLight;

    [Header("Lyd (valgfrit)")]
    public AudioClip notReadySound;
    public AudioClip activateSound;
    public AudioSource audioSource;

    // Runtime state
    private bool _isAvailable = false;
    private bool _isReady = false;
    private float _cooldownTimer = 0f;
    private Camera _cam;

    // Handle animation
    private bool _isAnimating = false;
    private float _animElapsed = 0f;
    private float _animFromAngle;
    private float _animToAngle;

    public float CooldownProgress => _isAvailable
        ? Mathf.Clamp01(_cooldownTimer / cooldownDuration)
        : 0f;

    void Start()
    {
        _cam = Camera.main;

        // Start i tændt tilstand — håndtag oppe, grønt lys
        SetHandleAngleImmediate(handleUpAngle);
        SetLightIndicators(isPowered: true);
    }

    void Update()
    {
        // Handle rotation animation
        if (_isAnimating)
        {
            _animElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_animElapsed / handleAnimDuration);
            float easedT = t * t * (3f - 2f * t); // smoothstep
            SetHandleAngleImmediate(Mathf.Lerp(_animFromAngle, _animToAngle, easedT));

            if (t >= 1f)
                _isAnimating = false;
        }

        if (!_isAvailable) return;

        if (!_isReady)
        {
            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer >= cooldownDuration)
            {
                _isReady = true;
                Debug.Log("[PowerSwitch] Klar til aktivering!");
            }
        }

        if (Input.GetKeyDown(interactKey) && IsPlayerNearby())
        {
            if (_isReady)
                Activate();
            else
            {
                float remaining = cooldownDuration - _cooldownTimer;
                Debug.Log($"[PowerSwitch] Ikke klar endnu — {remaining:F0} sek tilbage.");
                PlaySound(notReadySound);
            }
        }
    }

    /// <summary>
    /// Kaldes af WarehouseLightController når strømmen går.
    /// Roterer håndtaget ned og tænder det røde lys.
    /// </summary>
    public void SetSwitchAvailable(bool available)
    {
        _isAvailable = available;
        _isReady = false;
        _cooldownTimer = 0f;

        if (available)
        {
            // Strøm slukket — håndtag ned, rødt lys
            AnimateHandle(handleDownAngle);
            SetLightIndicators(isPowered: false);
            Debug.Log($"[PowerSwitch] Aktiveret — klar om {cooldownDuration} sek.");
        }
        else
        {
            // Strøm tændt — håndtag op, grønt lys
            AnimateHandle(handleUpAngle);
            SetLightIndicators(isPowered: true);
        }
    }

    void Activate()
    {
        _isAvailable = false;
        PlaySound(activateSound);
        Debug.Log("[PowerSwitch] Aktiveret! Gendanner strøm.");
        WarehouseLightController.Instance?.RestorePower();
        // Handle og lys opdateres via SetSwitchAvailable(false) kaldt af controlleren
    }

    // ── Håndtag ───────────────────────────────────────────────────

    void AnimateHandle(float targetAngle)
    {
        if (switchHandle == null) return;
        _animFromAngle = switchHandle.localEulerAngles.x;
        // Konverter fra Unity's 0-360 til -180..180 så lerp går den korte vej
        if (_animFromAngle > 180f) _animFromAngle -= 360f;
        _animToAngle = targetAngle;
        _animElapsed = 0f;
        _isAnimating = true;
    }

    void SetHandleAngleImmediate(float angle)
    {
        if (switchHandle == null) return;
        Vector3 e = switchHandle.localEulerAngles;
        e.x = angle;
        switchHandle.localEulerAngles = e;
    }

    // ── Indikatorlys ──────────────────────────────────────────────

    void SetLightIndicators(bool isPowered)
    {
        if (redLight != null) redLight.SetActive(!isPowered);
        if (greenLight != null) greenLight.SetActive(isPowered);
    }

    // ── Hjælpefunktioner ──────────────────────────────────────────

    bool IsPlayerNearby()
    {
        if (_cam == null) return false;
        float dist = Vector3.Distance(_cam.transform.position, transform.position);
        if (dist > interactRange) return false;
        Vector3 dir = (transform.position - _cam.transform.position).normalized;
        return Vector3.Angle(_cam.transform.forward, dir) < 60f;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isReady
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
#endif
}