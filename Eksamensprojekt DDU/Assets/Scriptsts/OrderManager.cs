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
    [Tooltip("Reference til ScannerDisplay-scriptet.")]
    public ScannerDisplay scannerDisplay;

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

        // Par hver pakke med en kompatibel zone
        // En zone er kompatibel hvis dens requiredItemID er tomt,
        // eller pakken starter med det krævede præfiks
        int attempts = 0;
        int maxAttempts = count * allZones.Count * 2; // undgå uendelig løkke

        while (orders.Count < count && attempts < maxAttempts)
        {
            attempts++;

            Scannable pkg = availablePackages[Random.Range(0, availablePackages.Count)];

            // Find alle zoner der accepterer denne pakke
            List<DeliveryZone> compatibleZones = availableZones.FindAll(z =>
                string.IsNullOrEmpty(z.requiredItemID) ||
                pkg.itemID.StartsWith(z.requiredItemID)
            );

            if (compatibleZones.Count == 0)
            {
                Debug.LogWarning($"[OrderManager] Ingen kompatibel zone fundet til pakke '{pkg.itemID}' — springer over.");
                availablePackages.Remove(pkg);
                if (availablePackages.Count == 0) break;
                continue;
            }

            // Vælg en tilfældig kompatibel zone
            DeliveryZone zone = compatibleZones[Random.Range(0, compatibleZones.Count)];

            string resolvedName = !string.IsNullOrEmpty(pkg.itemName) ? pkg.itemName
                                  : !string.IsNullOrEmpty(pkg.itemID) ? pkg.itemID
                                  : pkg.gameObject.name;

            Debug.Log($"[OrderManager] Pakke: '{pkg.gameObject.name}' | itemID='{pkg.itemID}' | itemName='{pkg.itemName}' | bruger: '{resolvedName}'");

            orders.Add(new Order
            {
                itemID = pkg.itemID,
                itemName = resolvedName,
                deliveryZone = zone,
            });

            // Fjern pakken så den ikke bruges igen i denne session
            availablePackages.Remove(pkg);

            // Fjern kun zonen hvis den har et krævet præfiks —
            // tomme zoner kan modtage mange forskellige pakker og genbruges
            if (!string.IsNullOrEmpty(zone.requiredItemID))
                availableZones.Remove(zone);

            // Stop hvis der ikke er flere pakker
            if (availablePackages.Count == 0) break;
        }

        if (orders.Count == 0)
            Debug.LogError("[OrderManager] Kunne ikke generere nogen ordrer — tjek at pakke-ID'er matcher zone-præfikser!");
        else
            Debug.Log($"[OrderManager] Genererede {orders.Count} kompatible ordrer.");
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
                scannerDisplay?.ShowItemConfirmed(order);
                return ScanResult.ItemCorrect;
            }
            else
            {
                scannerDisplay?.ShowWrongItem(scanned.itemID, order.itemID);
                return ScanResult.WrongItem;
            }
        }

        // ── Scanning en afleveringszone ────────────────────────────
        if (scanned.type == Scannable.ScanType.DeliveryZone)
        {
            if (!order.itemConfirmed)
            {
                scannerDisplay?.ShowScanPackageFirst();
                return ScanResult.ItemNotConfirmedYet;
            }

            if (scanned.deliveryZone == order.deliveryZone)
            {
                // Tjek at pakken fysisk ligger inden for zonen
                DeliveryZone zone = scanned.deliveryZone;
                if (zone.PackageInZone == null)
                {
                    scannerDisplay?.ShowPackageNotInZone(zone);
                    return ScanResult.PackageNotInZone;
                }

                // Tjek at pakken i zonen er den rigtige pakke
                Scannable pkg = zone.PackageInZone.GetComponent<Scannable>();
                if (pkg == null || pkg.itemID != order.itemID)
                {
                    scannerDisplay?.ShowWrongPackageInZone(order.itemID);
                    return ScanResult.PackageNotInZone;
                }

                order.delivered = true;
                onOrderComplete?.Invoke(order);
                scannerDisplay?.ShowOrderComplete(order);
                Debug.Log($"[OrderManager] Ordre {_currentIndex + 1}/{orders.Count} fuldført: {order.itemID}");
                AdvanceToNextOrder();
                return ScanResult.OrderComplete;
            }
            else
            {
                scannerDisplay?.ShowWrongZone(order.deliveryZone);
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
        scannerDisplay?.ShowNewOrder(order);

        Debug.Log($"[OrderManager] Ordre {_currentIndex + 1}/{orders.Count} aktiveret: {order.itemName} ({order.itemID}) → {order.deliveryZone?.zoneName}");
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
            scannerDisplay?.ShowAllComplete();
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
    PackageNotInZone,
    NoActiveOrder,
    Unknown,
}