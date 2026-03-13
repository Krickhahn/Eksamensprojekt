using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Styrer pakkeordrernes gameplay loop.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Udfyld listen "Alle pakker" med alle Scannable-pakker i scenen.
///   3. Udfyld listen "Alle zoner" med alle DeliveryZone-objekter i scenen.
///   4. Sæt antallet af ordrer per session i Inspector.
///   5. Sæt en reference til ScannerUI i Inspector.
/// </summary>
public class OrderManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────
    public static OrderManager Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Pulje — tilføj alle pakker og zoner her")]
    [Tooltip("Alle Scannable-pakker der findes i scenen (trækkes ind manuelt).")]
    public List<Scannable> allPackages = new List<Scannable>();

    [Tooltip("Alle DeliveryZone-objekter der findes i scenen (trækkes ind manuelt).")]
    public List<DeliveryZone> allZones = new List<DeliveryZone>();

    [Header("Session")]
    [Tooltip("Antal tilfældige ordrer der genereres pr. session. -1 = kør uendeligt.")]
    public int ordersPerSession = 5;

    [Tooltip("Blander pakke-puljen tilfældigt inden session starter.")]
    public bool shufflePackages = true;

    // Genereret ved runtime — ikke vist i Inspector
    private List<Order> orders = new List<Order>();

    [Header("Referencer")]
    [Tooltip("Reference til ScannerUI HUD-scriptet.")]
    public ScannerUI scannerUI;

    [Header("Events (valgfrit)")]
    [Tooltip("Kaldes når en ny ordre aktiveres.")]
    public UnityEvent<Order> onNewOrder;

    [Tooltip("Kaldes når pakken er korrekt scannet.")]
    public UnityEvent<Order> onItemConfirmed;

    [Tooltip("Kaldes når en ordre er fuldført og afleveret.")]
    public UnityEvent<Order> onOrderComplete;

    [Tooltip("Kaldes når alle ordrer er fuldført.")]
    public UnityEvent onAllOrdersComplete;

    // ── Runtime state ──────────────────────────────────────────────
    private int _currentIndex = -1;

    /// <summary>Den aktive ordre. Null hvis ingen ordre er aktiv.</summary>
    public Order CurrentOrder => (_currentIndex >= 0 && _currentIndex < orders.Count)
                                  ? orders[_currentIndex]
                                  : null;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        GenerateOrders();

        if (orders.Count > 0)
            ActivateOrder(0);
        else
            Debug.LogWarning("[OrderManager] Kunne ikke generere ordrer — tjek at pakker og zoner er tilføjet!");
    }

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Genererer en tilfældig liste af ordrer ud fra pakkerne og zonerne.
    /// Hver pakke parres med en tilfældig zone. Ingen pakke eller zone
    /// bruges to gange i samme session (medmindre puljen er udtømt).
    /// </summary>
    void GenerateOrders()
    {
        orders.Clear();

        if (allPackages.Count == 0 || allZones.Count == 0)
        {
            Debug.LogError("[OrderManager] Pulje af pakker eller zoner er tom!");
            return;
        }

        // Lav en kopi af listerne så vi kan fjerne brugte elementer
        List<Scannable> availablePackages = new List<Scannable>(allPackages);
        List<DeliveryZone> availableZones = new List<DeliveryZone>(allZones);

        // Bland rækkefølgen
        if (shufflePackages)
        {
            Shuffle(availablePackages);
            Shuffle(availableZones);
        }

        int count = ordersPerSession < 0
                    ? availablePackages.Count
                    : Mathf.Min(ordersPerSession, availablePackages.Count);

        for (int i = 0; i < count; i++)
        {
            Scannable pkg = availablePackages[i % availablePackages.Count];
            DeliveryZone z = availableZones[i % availableZones.Count];

            orders.Add(new Order
            {
                itemID = pkg.itemID,
                itemName = string.IsNullOrEmpty(pkg.itemID) ? pkg.gameObject.name : pkg.itemID,
                deliveryZone = z,
            });
        }

        Debug.Log($"[OrderManager] Genererede {orders.Count} tilfældige ordrer.");
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Kaldes af Scannable når spilleren scanner et objekt.
    /// Returnerer en ScanResult der beskriver hvad der skete.
    /// </summary>
    public ScanResult TryScan(Scannable scanned)
    {
        Order order = CurrentOrder;

        if (order == null)
            return ScanResult.NoActiveOrder;

        // ── Scanning en pakke ──────────────────────────────────────
        if (scanned.type == Scannable.ScanType.Package)
        {
            if (order.itemConfirmed)
                return ScanResult.AlreadyConfirmed;

            if (scanned.itemID == order.itemID)
            {
                order.itemConfirmed = true;
                onItemConfirmed?.Invoke(order);
                scannerUI?.ShowItemConfirmed(order);
                return ScanResult.ItemCorrect;
            }
            else
            {
                scannerUI?.ShowWrongItem(scanned.itemID, order.itemID);
                return ScanResult.WrongItem;
            }
        }

        // ── Scanning en afleveringszone ────────────────────────────
        if (scanned.type == Scannable.ScanType.DeliveryZone)
        {
            if (!order.itemConfirmed)
            {
                scannerUI?.ShowScanPackageFirst();
                return ScanResult.ItemNotConfirmedYet;
            }

            if (scanned.deliveryZone == order.deliveryZone)
            {
                order.delivered = true;
                onOrderComplete?.Invoke(order);
                scannerUI?.ShowOrderComplete(order);
                AdvanceToNextOrder();
                return ScanResult.OrderComplete;
            }
            else
            {
                scannerUI?.ShowWrongZone(order.deliveryZone);
                return ScanResult.WrongZone;
            }
        }

        return ScanResult.Unknown;
    }

    // ──────────────────────────────────────────────────────────────
    void ActivateOrder(int index)
    {
        _currentIndex = index;
        Order order = CurrentOrder;

        // Nulstil runtime state for ny ordre
        order.itemConfirmed = false;
        order.delivered = false;

        // Aktiver afleveringszonens visuelle indikator
        order.deliveryZone?.SetHighlight(true);

        onNewOrder?.Invoke(order);
        scannerUI?.ShowNewOrder(order);

        Debug.Log($"[OrderManager] Ny ordre aktiveret: {order.itemName} ({order.itemID})");
    }

    void AdvanceToNextOrder()
    {
        // Sluk highlight på forrige zones
        if (CurrentOrder != null)
            CurrentOrder.deliveryZone?.SetHighlight(false);

        int next = _currentIndex + 1;

        if (next < orders.Count)
        {
            ActivateOrder(next);
        }
        else
        {
            _currentIndex = -1;
            onAllOrdersComplete?.Invoke();
            scannerUI?.ShowAllComplete();
            Debug.Log("[OrderManager] Alle ordrer er fuldført!");
        }
    }
}

/// <summary>Resultattype returneret af OrderManager.TryScan().</summary>
public enum ScanResult
{
    ItemCorrect,
    WrongItem,
    AlreadyConfirmed,
    ItemNotConfirmedYet,
    OrderComplete,
    WrongZone,
    NoActiveOrder,
    Unknown,
}