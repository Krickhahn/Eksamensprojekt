using UnityEngine;

/// <summary>
/// Dataklasse der beskriver én pakkeordre.
/// Opret instanser i OrderManager's Inspector-liste.
/// </summary>
[System.Serializable]
public class Order
{
    [Tooltip("Unikt varenummer der står på pakken (skal matche Scannable.itemID).")]
    public string itemID;

    [Tooltip("Læsbart navn der vises på HUD-skanneren.")]
    public string itemName;

    [Tooltip("Reference til den DeliveryZone pakken skal afleveres i.")]
    public DeliveryZone deliveryZone;

    // ── Runtime state ──────────────────────────────────────────────
    /// <summary>Sat til true når pakken er scannet og bekræftet korrekt.</summary>
    [System.NonSerialized] public bool itemConfirmed;

    /// <summary>Sat til true når pakken er afleveret på korrekt zone.</summary>
    [System.NonSerialized] public bool delivered;

    /// <summary>Antal point der blev tjent for denne levering (efter fragile-straf).</summary>
    [System.NonSerialized] public int earnedPoints;

    /// <summary>Antal point pakken er værd ved levering.</summary>
    [System.NonSerialized] public int basePoints;

    /// <summary>Akkumulerede straffe for denne pakke (fragile fald).</summary>
    [System.NonSerialized] public int penaltiesAccrued;
}