using System.Collections;
using UnityEngine;

/// <summary>
/// Stikkontakt der gendanner lyset efter et strømsvigt.
/// Har en cooldown så spilleren IKKE kan aktivere den med det samme.
///
/// OPSÆTNING:
///   1. Tilføj dette script til stikkontaktens GameObject.
///   2. Tilføj en Collider på objektet.
///   3. Sæt interactKey til den tast spilleren bruger (standard: E).
///   4. Sæt interactRange til hvor tæt spilleren skal være.
///   5. Justér cooldownDuration til hvor længe spilleren skal overleve
///      inden stikkontakten kan aktiveres (anbefalet: 20-40 sekunder).
///
/// VISUEL FEEDBACK:
///   Tilslut en Renderer og to materialer (readyMaterial / notReadyMaterial)
///   for at give spilleren et visuelt signal om hvornår kontakten er klar.
/// </summary>
public class AngelPowerSwitch : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────
    [Header("Interaktion")]
    [Tooltip("Tast der aktiverer stikkontakten.")]
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("Maks afstand spilleren kan aktivere kontakten fra.")]
    public float interactRange = 2f;

    [Header("Cooldown")]
    [Tooltip("Sekunder spilleren skal overleve inden kontakten kan aktiveres.\n" +
             "Anbefalet: 20-40 sekunder for at tvinge engagement med statuen.")]
    public float cooldownDuration = 30f;

    [Header("Visuel feedback")]
    [Tooltip("Renderer på kontakten der skifter materiale baseret på tilstand.")]
    public Renderer switchRenderer;

    [Tooltip("Materiale når kontakten IKKE er klar endnu (rød).")]
    public Material notReadyMaterial;

    [Tooltip("Materiale når kontakten er klar til at aktiveres (grøn).")]
    public Material readyMaterial;

    [Tooltip("Materiale når kontakten allerede er brugt denne session.")]
    public Material usedMaterial;

    [Header("Lyd (valgfrit)")]
    [Tooltip("Lyd når spilleren forsøger at aktivere kontakten for tidligt.")]
    public AudioClip notReadySound;

    [Tooltip("Lyd når kontakten aktiveres succesfuldt.")]
    public AudioClip activateSound;

    public AudioSource audioSource;

    // ── Runtime state ──────────────────────────────────────────────
    private bool _isAvailable = false;     // Er strømsvigtets flow i gang?
    private bool _isReady = false;          // Er cooldown overstået?
    private bool _isUsed = false;           // Er den allerede aktiveret denne gang?
    private float _cooldownTimer = 0f;
    private Camera _cam;

    // Offentlig progress til evt. UI (0-1)
    public float CooldownProgress => _isAvailable ? Mathf.Clamp01(_cooldownTimer / cooldownDuration) : 0f;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        _cam = Camera.main;
        UpdateVisual();
    }

    void Update()
    {
        if (!_isAvailable || _isUsed) return;

        // ── Cooldown tæller op ─────────────────────────────────────
        if (!_isReady)
        {
            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer >= cooldownDuration)
            {
                _isReady = true;
                UpdateVisual();
                Debug.Log("[PowerSwitch] Stikkontakt er nu klar!");
            }
        }

        // ── Input ──────────────────────────────────────────────────
        if (Input.GetKeyDown(interactKey) && IsPlayerNearby())
        {
            if (_isReady)
                Activate();
            else
                OnNotReady();
        }
    }

    // ── Offentlige metoder ─────────────────────────────────────────

    /// <summary>
    /// Aktiverer eller deaktiverer kontakten.
    /// Kaldes af WarehouseLightController.
    /// </summary>
    public void SetSwitchAvailable(bool available)
    {
        _isAvailable = available;
        _isReady = false;
        _isUsed = false;
        _cooldownTimer = 0f;
        UpdateVisual();

        if (available)
            Debug.Log($"[PowerSwitch] Cooldown startet — klar om {cooldownDuration} sekunder.");
    }

    // ── Intern logik ───────────────────────────────────────────────

    void Activate()
    {
        _isUsed = true;
        UpdateVisual();

        PlaySound(activateSound);
        Debug.Log("[PowerSwitch] Aktiveret! Gendanner strøm.");

        WarehouseLightController.Instance?.RestorePower();
    }

    void OnNotReady()
    {
        float remaining = cooldownDuration - _cooldownTimer;
        Debug.Log($"[PowerSwitch] Ikke klar endnu — {remaining:F0} sekunder tilbage.");
        PlaySound(notReadySound);
    }

    bool IsPlayerNearby()
    {
        if (_cam == null) return false;

        // Simpel afstandstjek fra kameraet
        float dist = Vector3.Distance(_cam.transform.position, transform.position);
        if (dist > interactRange) return false;

        // Tjek at spilleren kigger nogenlunde mod kontakten
        Vector3 dir = (transform.position - _cam.transform.position).normalized;
        float angle = Vector3.Angle(_cam.transform.forward, dir);
        return angle < 60f;
    }

    void UpdateVisual()
    {
        if (switchRenderer == null) return;

        if (_isUsed && usedMaterial != null)
            switchRenderer.material = usedMaterial;
        else if (_isReady && readyMaterial != null)
            switchRenderer.material = readyMaterial;
        else if (notReadyMaterial != null)
            switchRenderer.material = notReadyMaterial;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;
        audioSource.PlayOneShot(clip);
    }

    // ── Gizmo ──────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isReady
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, interactRange);

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.6f,
            _isAvailable ? (_isReady ? "KLAR" : $"Cooldown: {_cooldownTimer:F0}/{cooldownDuration:F0}s") : "Inaktiv"
        );
    }
#endif
}