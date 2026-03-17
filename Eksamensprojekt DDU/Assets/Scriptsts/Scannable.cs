using UnityEngine;

/// <summary>
/// Sæt dette script på:
///   - Alle pakker/kasser der kan scannes (type = Package)
///   - Alle afleveringszoner (type = DeliveryZone)
///
/// OPSÆTNING PAKKE:
///   - Sæt Type til "Package"
///   - Udfyld Item ID med samme varenummer som i Order-dataen
///
/// OPSÆTNING AFLEVERINGSZONE:
///   - Sæt Type til "DeliveryZone"
///   - Træk DeliveryZone-komponenten ind i Delivery Zone-feltet
/// </summary>
public class Scannable : MonoBehaviour
{
    public enum ScanType { Package, DeliveryZone }

    [Header("Type")]
    [Tooltip("Er dette en pakke eller en afleveringszone?")]
    public ScanType type = ScanType.Package;

    [Header("Pakke-indstillinger")]
    [Tooltip("Unikt varenummer — skal matche Order.itemID nøjagtigt.")]
    public string itemID;

    [Tooltip("Læsbart navn der vises på displayet, f.eks. 'Fragile Box' eller 'Tung Pakke'.\nLad feltet stå tomt for at bruge itemID som navn.")]
    public string itemName = "";

    [Tooltip("Antal point spilleren tjener ved korrekt levering af denne pakke.")]
    public int deliveryPoints = 100;

    [Tooltip("Fragile pakker mister point hver gang de rammer gulvet — medmindre de er i en afleveringszone.")]
    public bool isFragile = false;

    [Tooltip("Point der trækkes fra ved hvert fald (kun fragile pakker).")]
    public int fragileDropPenalty = 25;

    [Header("Zone-indstillinger")]
    [Tooltip("Reference til DeliveryZone-komponenten på dette objekt (kun relevant hvis type = DeliveryZone).")]
    public DeliveryZone deliveryZone;
}