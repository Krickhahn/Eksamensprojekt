using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Udgangsdøren spilleren skal nå for at afslutte natten.
///
/// Døren låses op på to måder:
///   1. Alle pakkeordrer er afleveret
///   2. Skiftets timer løber ud (solen står op)
///
/// I begge tilfælde SKAL spilleren fysisk nå hen til døren og trykke E.
/// Win-screen vises aldrig automatisk — spilleren er aldrig i sikkerhed
/// bare fordi de venter.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et dør-GameObject med en Collider.
///   2. Tilføj WinScreen.cs til et Canvas og træk det ind i Win Screen-feltet.
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
    public UnityEvent onDoorUnlocked;
    public UnityEvent onPlayerWin;

    // ── Private ────────────────────────────────────────────────────
    private Camera _cam;
    private bool _isUnlocked;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = Camera.main;
    }

    void Start()
    {
        // Lyt på ordrer fuldført
        if (OrderManager.Instance != null)
            OrderManager.Instance.onAllOrdersComplete.AddListener(UnlockDoor);
        else
            Debug.LogWarning("[ExitDoor] OrderManager ikke fundet.");

        // Lyt på timer udløbet
        if (ShiftTimer.Instance != null)
            ShiftTimer.Instance.onShiftEnd.AddListener(UnlockDoor);
        else
            Debug.LogWarning("[ExitDoor] ShiftTimer ikke fundet.");
    }

    void OnDestroy()
    {
        if (OrderManager.Instance != null)
            OrderManager.Instance.onAllOrdersComplete.RemoveListener(UnlockDoor);

        if (ShiftTimer.Instance != null)
            ShiftTimer.Instance.onShiftEnd.RemoveListener(UnlockDoor);
    }

    void Update()
    {
        if (!_isUnlocked) return;
        if (_cam == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (Input.GetKeyDown(interactKey))
            TryInteract();
    }

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Låser døren op — kaldes både af onAllOrdersComplete og onShiftEnd.
    /// Spilleren skal stadig nå hen til døren selv.
    /// </summary>
    void UnlockDoor()
    {
        if (_isUnlocked) return; // undgå dobbelt unlock

        _isUnlocked = true;
        onDoorUnlocked?.Invoke();

        // Vis besked på skanneren
        var display = FindAnyObjectByType<ScannerDisplay>();
        if (ShiftTimer.Instance != null && ShiftTimer.Instance.ShiftEnded)
            display?.ShowShiftEnded();

        Debug.Log("[ExitDoor] Døren er låst op — spilleren skal nå hen til udgangen.");
    }

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
        ShiftTimer.Instance?.StopTimer();
        onPlayerWin?.Invoke();
        winScreen?.Show();
        Debug.Log("[ExitDoor] Spilleren nåede udgangen — natten er overstået!");
    }

    void OnDrawGizmosSelected()
    {
        // Grøn = ulåst, rød = låst
        Gizmos.color = _isUnlocked
            ? new Color(0f, 1f, 0.5f, 0.4f)
            : new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}