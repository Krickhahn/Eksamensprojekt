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
    [Tooltip("Alle Scannable-pakker i scenen. Udfyldes automatisk af PackageSpawner ved runtime — du behøver ikke trække dem ind manuelt.")]
    public List<Scannable> allPackages = new List<Scannable>();

    [Tooltip("Alle DeliveryZone-objekter der findes i scenen (trækkes ind manuelt).")]
    public List<DeliveryZone> allZones = new List<DeliveryZone>();

    [Header("Session")]
    [Tooltip("Antal tilfældige ordrer der genereres pr. session. -1 = kør uendeligt.")]
    public int ordersPerSession = 5;

    [Tooltip("Blander pakke-puljen tilfældigt inden session starter.")]
    public bool shufflePackages = true;

    [Tooltip("Hvis true gives den første ordre automatisk når spillet starter.\n" +
             "Hvis false skal spilleren scanne en OrderStation for at få første ordre.")]
    public bool autoStart = true;

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
    private int _nextIndex = 0;

    /// <summary>True når ordren er afleveret og vi venter på office-scanning — selv mens point-teksten ruller.</summary>
    public bool WaitingForOffice { get; private set; }
    private bool _pendingAdvance;

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
        // Vent til næste frame så PackageSpawner når at registrere pakker
        StartCoroutine(StartDelayed());
    }

    System.Collections.IEnumerator StartDelayed()
    {
        yield return null; // ét frame — PackageSpawner kører i Start()
        GenerateOrders();

        if (orders.Count == 0)
        {
            Debug.LogWarning("[OrderManager] Kunne ikke generere ordrer — tjek at pakker og zoner er tilføjet!");
            yield break;
        }

        _nextIndex = 0;

        if (autoStart)
            ActivateOrder(0);
        else
            scannerDisplay?.ShowStandBy();
    }

    /// <summary>
    /// Registrerer en pakke der er spawned af PackageSpawner.
    /// Skal kaldes inden GenerateOrders kører (dvs. i PackageSpawner.Start).
    /// </summary>
    /// <summary>
    /// Finder den ordre der matcher en specifik Scannable-instans.
    /// Returnerer null hvis ingen ordre er tilknyttet denne pakke.
    /// </summary>
    public Order FindOrderForPackage(Scannable pkg)
    {
        if (pkg == null) return null;
        return orders.Find(o => o.targetPackage == pkg);
    }

    // Maps Scannable → spawnZoneName set af PackageSpawner
    private System.Collections.Generic.Dictionary<Scannable, string> _packageZoneNames
        = new System.Collections.Generic.Dictionary<Scannable, string>();

    public void RegisterPackage(Scannable pkg, string spawnZoneName = "")
    {
        if (pkg == null) return;
        if (!allPackages.Contains(pkg))
            allPackages.Add(pkg);

        if (!string.IsNullOrEmpty(spawnZoneName))
            _packageZoneNames[pkg] = spawnZoneName;
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

        int attempts = 0;
        int maxAttempts = count * allZones.Count * 2;

        while (orders.Count < count && attempts < maxAttempts)
        {
            attempts++;

            Scannable pkg = availablePackages[Random.Range(0, availablePackages.Count)];

            // Find alle zoner der accepterer denne pakke.
            // Zones med et specifikt requiredItemID der matcher pakken
            // foretrækkes altid frem for åbne zoner (tomt requiredItemID).
            // Det forhindrer at containeren (åben zone) stjæler ordrer der
            // hører til en specifik hylde.
            List<DeliveryZone> specificZones = availableZones.FindAll(z =>
                !string.IsNullOrEmpty(z.requiredItemID) &&
                pkg.itemID.StartsWith(z.requiredItemID)
            );

            List<DeliveryZone> compatibleZones = specificZones.Count > 0
                ? specificZones
                : availableZones.FindAll(z => string.IsNullOrEmpty(z.requiredItemID));

            if (compatibleZones.Count == 0)
            {
                Debug.LogWarning($"[OrderManager] Ingen kompatibel zone til '{pkg.itemID}' — springer over.");
                availablePackages.Remove(pkg);
                if (availablePackages.Count == 0) break;
                continue;
            }

            DeliveryZone zone = compatibleZones[Random.Range(0, compatibleZones.Count)];
            AddOrder(pkg, zone);
            availablePackages.Remove(pkg);

            // Zoner med præfiks fjernes efter brug — åbne zoner genbruges
            if (!string.IsNullOrEmpty(zone.requiredItemID))
                availableZones.Remove(zone);

            if (availablePackages.Count == 0) break;
        }

        if (orders.Count == 0)
            Debug.LogError("[OrderManager] Kunne ikke generere nogen ordrer — tjek at pakke-ID'er matcher zone-præfikser!");
        else
            Debug.Log($"[OrderManager] Genererede {orders.Count} ordrer.");
    }

    void AddOrder(Scannable pkg, DeliveryZone zone)
    {
        string resolvedName = !string.IsNullOrEmpty(pkg.itemName) ? pkg.itemName
                              : !string.IsNullOrEmpty(pkg.itemID) ? pkg.itemID
                              : pkg.gameObject.name;

        Debug.Log($"[OrderManager] Ordre: '{resolvedName}' ({pkg.itemID}) → {zone.zoneName}");

        _packageZoneNames.TryGetValue(pkg, out string spawnZone);

        orders.Add(new Order
        {
            itemID = pkg.itemID,
            itemName = resolvedName,
            deliveryZone = zone,
            basePoints = pkg.deliveryPoints,
            targetPackage = pkg,
            spawnZoneName = spawnZone ?? "",
        });
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
            // Tjek at det er præcis den rigtige pakke-instans — ikke bare samme ID
            bool correctInstance = order.targetPackage != null
                ? scanned == order.targetPackage
                : scanned.itemID == order.itemID; // fallback hvis targetPackage ikke er sat

            if (!correctInstance)
            {
                scannerDisplay?.ShowWrongItem(scanned.itemID, order.itemID);
                return ScanResult.WrongItem;
            }

            // Første scanning — bekræft pakken og vis destination
            if (!order.itemConfirmed)
            {
                order.itemConfirmed = true;
                onItemConfirmed?.Invoke(order);
                scannerDisplay?.ShowItemConfirmed(order);
                return ScanResult.ItemCorrect;
            }

            // Anden scanning — tjek om pakken er i den rigtige zone
            DeliveryZone correctZone = order.deliveryZone;

            if (correctZone.PackageInZone == null)
            {
                scannerDisplay?.ShowPackageNotInZone(correctZone);
                return ScanResult.PackageNotInZone;
            }

            Scannable pkgInZone = correctZone.PackageInZone.GetComponent<Scannable>();

            // Tjek at det er præcis den rigtige pakke-instans i zonen
            bool correctPkgInZone = order.targetPackage != null
                ? pkgInZone == order.targetPackage
                : pkgInZone != null && pkgInZone.itemID == order.itemID;

            if (!correctPkgInZone)
            {
                scannerDisplay?.ShowWrongPackageInZone(order.itemID);
                return ScanResult.PackageNotInZone;
            }

            // Pakken er i den rigtige zone — ordre fuldført
            // Nulstil zonen så den er klar til næste ordre
            correctZone.ClearPackage();
            order.delivered = true;

            // Anvend timer-multiplikator — jo senere levering, jo færre point
            float multiplier = ShiftTimer.Instance != null ? ShiftTimer.Instance.ScoreMultiplier : 1f;
            int baseEarned = Mathf.Max(0, order.basePoints - order.penaltiesAccrued);
            order.earnedPoints = Mathf.RoundToInt(baseEarned * multiplier);
            ScoreManager.Instance?.AddScore(order.earnedPoints);
            onOrderComplete?.Invoke(order);

            // Beregn hvad der sker efter levering
            _currentIndex = -1;
            _nextIndex = orders.FindIndex(o => !o.delivered);
            bool moreOrders = _nextIndex >= 0;
            WaitingForOffice = moreOrders && !autoStart;

            // Hvis ingen flere ordrer — fyrer event med det samme
            // så ExitDoor kan reagere uden at vente på scroll-animation
            if (!moreOrders)
            {
                onAllOrdersComplete?.Invoke();
                Debug.Log("[OrderManager] Alle ordrer er fuldført!");
            }

            Debug.Log($"[OrderManager] Ordre fuldført: {order.itemID} +{order.earnedPoints} point (base: {order.basePoints}, straf: {order.penaltiesAccrued})");
            _pendingAdvance = true;
            scannerDisplay?.ShowOrderComplete(order, () =>
            {
                if (!_pendingAdvance) return;
                _pendingAdvance = false;

                if (!moreOrders)
                    scannerDisplay?.ShowAllComplete();
                else if (WaitingForOffice)
                    scannerDisplay?.ShowGoToOffice();
                else
                    ActivateOrder(_nextIndex);
            });
            return ScanResult.OrderComplete;
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
        // Bruges kun ved autoStart — onAllOrdersComplete er allerede fyret
        // direkte i TryScan så vi ikke fyrer det to gange
        if (_nextIndex >= 0)
            ActivateOrder(_nextIndex);
        // Ingen else — all-complete håndteres i TryScan
    }

    /// <summary>Returnerer true hvis der er flere ordrer der ikke er afleveret endnu.</summary>
    public bool HasMoreOrders()
    {
        return orders.Exists(o => !o.delivered) && _currentIndex < 0;
    }

    /// <summary>
    /// Aktiverer næste ventende ordre.
    /// Kaldes af OrderStation når spilleren scanner kontorstationen.
    /// </summary>
    public void GiveNextOrder()
    {
        WaitingForOffice = false;
        _pendingAdvance = false; // annuller evt. ventende scroll-callback
        int idx = orders.FindIndex(o => !o.delivered);
        if (idx >= 0)
            ActivateOrder(idx);
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