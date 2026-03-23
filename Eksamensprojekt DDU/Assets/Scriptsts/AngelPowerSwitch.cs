using UnityEngine;

/// <summary>
/// Stikkontakt der gendanner lyset efter et strømsvigt.
/// Har en cooldown så spilleren ikke kan aktivere den med det samme.
///
/// OPSÆTNING:
///   1. Tilføj dette script til stikkontaktens GameObject med en Collider.
///   2. Juster cooldownDuration — anbefalet 20-40 sekunder.
///   3. Tilslut evt. to materialer til switchRenderer for visuel feedback.
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

    [Header("Visuel feedback (valgfrit)")]
    [Tooltip("Renderer der skifter materiale baseret på tilstand.")]
    public Renderer switchRenderer;

    [Tooltip("Materiale når kontakten ikke er klar endnu.")]
    public Material notReadyMaterial;

    [Tooltip("Materiale når kontakten er klar til aktivering.")]
    public Material readyMaterial;

    [Header("Lyd (valgfrit)")]
    public AudioClip notReadySound;
    public AudioClip activateSound;
    public AudioSource audioSource;

    // Runtime state
    private bool _isAvailable = false;
    private bool _isReady = false;
    private float _cooldownTimer = 0f;
    private Camera _cam;

    public float CooldownProgress => _isAvailable
        ? Mathf.Clamp01(_cooldownTimer / cooldownDuration)
        : 0f;

    void Start()
    {
        _cam = Camera.main;
        UpdateVisual();
    }

    void Update()
    {
        if (!_isAvailable) return;

        if (!_isReady)
        {
            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer >= cooldownDuration)
            {
                _isReady = true;
                UpdateVisual();
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

    public void SetSwitchAvailable(bool available)
    {
        _isAvailable = available;
        _isReady = false;
        _cooldownTimer = 0f;
        UpdateVisual();
        if (available)
            Debug.Log($"[PowerSwitch] Aktiveret — klar om {cooldownDuration} sek.");
    }

    void Activate()
    {
        _isAvailable = false;
        UpdateVisual();
        PlaySound(activateSound);
        Debug.Log("[PowerSwitch] Aktiveret! Gendanner strøm.");
        WarehouseLightController.Instance?.RestorePower();
    }

    bool IsPlayerNearby()
    {
        if (_cam == null) return false;
        float dist = Vector3.Distance(_cam.transform.position, transform.position);
        if (dist > interactRange) return false;
        Vector3 dir = (transform.position - _cam.transform.position).normalized;
        return Vector3.Angle(_cam.transform.forward, dir) < 60f;
    }

    void UpdateVisual()
    {
        if (switchRenderer == null) return;
        if (_isReady && readyMaterial != null)
            switchRenderer.material = readyMaterial;
        else if (!_isReady && notReadyMaterial != null)
            switchRenderer.material = notReadyMaterial;
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