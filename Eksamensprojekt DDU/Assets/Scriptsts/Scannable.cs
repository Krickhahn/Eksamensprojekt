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

    [Header("Zone-indstillinger")]
    [Tooltip("Reference til DeliveryZone-komponenten på dette objekt (kun relevant hvis type = DeliveryZone).")]
    public DeliveryZone deliveryZone;
}