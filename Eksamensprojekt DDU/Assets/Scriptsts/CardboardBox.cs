using UnityEngine;

/// <summary>
/// Placer dette script på din papkasse-GameObject.
/// Kræver en Collider med "Is Trigger" = true (bruges til at finde spilleren).
/// Spilleren skal have tagget "Player" og have både
/// PlayerMovement og CharacterController på sig.
/// </summary>
public class CardboardBox : MonoBehaviour
{
    [Header("Indstillinger")]
    [Tooltip("Knap der bruges til at gemme/vise sig")]
    [SerializeField] private KeyCode hideKey = KeyCode.E;

    [Tooltip("Maksimal afstand i meter spilleren skal være fra kassen for at kunne gemme sig")]
    [SerializeField] private float interactDistance = 2f;

    [Tooltip("UI-objekt der vises når spilleren er tæt nok på (kan være null)")]
    [SerializeField] private GameObject promptUI;

    [Tooltip("Spillerens hoved-UI der skjules mens spilleren er gemt (f.eks. dit Canvas eller HUD-objekt)")]
    [SerializeField] private GameObject playerHUD;

    [Tooltip("Skjul spillerens mesh (alle Renderers) mens de er gemt")]
    [SerializeField] private bool hidePlayerMesh = true;

    [Tooltip("Offset fra kassens centrum hvor spilleren placeres (justér hvis spilleren clipper)")]
    [SerializeField] private Vector3 hideOffset = new Vector3(0f, 0.1f, 0f);

    [Tooltip("Offset fra kassen spilleren spawnes ved når de kigger ud")]
    [SerializeField] private Vector3 exitOffset = new Vector3(1.2f, 0f, 0f);

    // ── Private state ───────────────────────────────────────
    private bool _playerHiding;
    private bool _promptShowing;

    private Transform _playerTransform;
    private PlayerMovement _playerMovement;
    private CharacterController _characterController;
    private Renderer[] _playerRenderers;

    // ── Unity lifecycle ─────────────────────────────────────
    void Start()
    {
        if (promptUI != null)
            promptUI.SetActive(false);
    }

    void Update()
    {
        if (_playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);
        bool withinRange = dist <= interactDistance;

        // Vis/skjul prompt baseret på afstand (kun når spilleren ikke er gemt)
        if (!_playerHiding)
        {
            if (withinRange && !_promptShowing)
            {
                _promptShowing = true;
                if (promptUI != null) promptUI.SetActive(true);
            }
            else if (!withinRange && _promptShowing)
            {
                _promptShowing = false;
                if (promptUI != null) promptUI.SetActive(false);
            }
        }

        // Gem/vis kun hvis spilleren er inden for interaktionsafstanden
        if (withinRange && Input.GetKeyDown(hideKey))
        {
            if (_playerHiding) ExitBox();
            else EnterBox();
        }
    }

    // ── Trigger bruges til at finde spillerens referencer ───
    // Selve interaktionsafstanden styres af interactDistance i Update().
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || _playerTransform != null) return;

        _playerTransform = other.transform;
        _playerMovement = other.GetComponent<PlayerMovement>();
        _characterController = other.GetComponent<CharacterController>();
        _playerRenderers = other.GetComponentsInChildren<Renderer>();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (_playerHiding)
            ExitBox();

        if (promptUI != null)
        {
            promptUI.SetActive(false);
            _promptShowing = false;
        }

        _playerTransform = null;
    }

    // ── Gem/vis logik ───────────────────────────────────────
    void EnterBox()
    {
        if (_playerMovement == null || _characterController == null) return;

        _playerHiding = true;

        if (promptUI != null)
        {
            promptUI.SetActive(false);
            _promptShowing = false;
        }

        _characterController.enabled = false;
        _playerTransform.position = transform.position + hideOffset;
        _characterController.enabled = true;

        Vector3 exitDir = new Vector3(exitOffset.x, 0f, exitOffset.z).normalized;
        if (exitDir == Vector3.zero) exitDir = transform.forward;

        _playerMovement.SetHiding(true, exitDir);

        if (hidePlayerMesh && _playerRenderers != null)
            foreach (var r in _playerRenderers)
                r.enabled = false;

        if (playerHUD != null) playerHUD.SetActive(false);

        HidingManager.Instance?.SetPlayerHiding(true);

        Debug.Log("[CardboardBox] Spiller er gemt.");
    }

    void ExitBox()
    {
        _playerHiding = false;

        if (_playerTransform != null && _characterController != null)
        {
            _characterController.enabled = false;
            _playerTransform.position = transform.position + exitOffset;
            _characterController.enabled = true;
        }

        if (_playerMovement != null)
            _playerMovement.SetHiding(false);

        if (hidePlayerMesh && _playerRenderers != null)
            foreach (var r in _playerRenderers)
                r.enabled = true;

        if (playerHUD != null) playerHUD.SetActive(true);

        HidingManager.Instance?.SetPlayerHiding(false);

        Debug.Log("[CardboardBox] Spiller har forladt kassen.");
    }

    // ── Gizmo: vis interaktionsafstand i editor ─────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
#endif
}