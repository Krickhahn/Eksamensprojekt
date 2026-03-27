using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tilføj dette script til et dør-GameObject.
/// Spilleren kan trykke E for at interagere med døren når alle ordrer er afleveret.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et GameObject med en Collider.
///   2. Tilføj WinScreen.cs til et Canvas og træk det ind i Win Screen-feltet.
///   3. Juster Interact Range og Interact Key efter behov.
/// </summary>
public class ExitDoor : MonoBehaviour
{
    [Header("Interaktion")]
    [Tooltip("Maks afstand spilleren skal være fra døren for at kunne interagere.")]
    public float interactRange = 2.5f;

    [Tooltip("Tast til at interagere med døren.")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Referencer")]
    [Tooltip("Win screen UI der vises når spilleren bruger døren.")]
    public WinScreen winScreen;

    [Header("Events (valgfrit)")]
    public UnityEvent onPlayerWin;

    // ── Private ────────────────────────────────────────────────────
    private Camera _cam;
    private bool _allOrdersComplete;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = Camera.main;
    }

    void Start()
    {
        // Start bruges i stedet for OnEnable så OrderManager.Instance er klar
        if (OrderManager.Instance != null)
            OrderManager.Instance.onAllOrdersComplete.AddListener(OnAllOrdersComplete);
        else
            Debug.LogWarning("[ExitDoor] OrderManager ikke fundet — win-betingelse virker ikke.");
    }

    void OnDestroy()
    {
        if (OrderManager.Instance != null)
            OrderManager.Instance.onAllOrdersComplete.RemoveListener(OnAllOrdersComplete);
    }

    void Update()
    {
        if (!_allOrdersComplete) return;
        if (_cam == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (Input.GetKeyDown(interactKey))
            TryInteract();
    }

    // ──────────────────────────────────────────────────────────────
    void TryInteract()
    {
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                Interact();
        }
    }

    void Interact()
    {
        onPlayerWin?.Invoke();
        winScreen?.Show();
        Debug.Log("[ExitDoor] Spilleren gik ud — du vandt!");
    }

    void OnAllOrdersComplete()
    {
        _allOrdersComplete = true;
        Debug.Log("[ExitDoor] Alle ordrer fuldført — udgangen er nu tilgængelig.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}