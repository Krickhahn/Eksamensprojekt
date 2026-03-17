using UnityEngine;

/// <summary>
/// Tilføj dette script til et objekt i kontoret (f.eks. en computer, printer
/// eller et bord) som spilleren skal scanne for at modtage næste ordre.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et GameObject med en Collider.
///   2. Sæt OrderManager til at starte uden automatisk første ordre
///      ved at slå Auto Start fra på OrderManager.
///   3. Sæt startOrderStation til true på den første station hvis
///      spilleren skal scanne for at få allerede den første ordre.
///
/// HVORDAN DET VIRKER:
///   Når spilleren scanner denne station kalder den OrderManager.GiveNextOrder().
///   Stationen er kun aktiv når spilleren har afleveret den forrige ordre.
///   Hvis spilleren prøver at scanne stationen for tidligt viser displayet en besked.
/// </summary>
public class OrderStation : MonoBehaviour
{
    [Header("Indstillinger")]
    [Tooltip("Tekst der vises på skanneren når spilleren scanner stationen og får en ny ordre.")]
    public string readyMessage = "NY ORDRE KLAR";

    [Tooltip("Tekst der vises hvis spilleren scanner stationen mens den forrige ordre ikke er afleveret endnu.")]
    public string notReadyMessage = "AFSLUT NUVAERENDE ORDRE FORST";

    [Tooltip("Tekst der vises når alle ordrer er fuldført og der ikke er flere at hente.")]
    public string allDoneMessage = "INGEN FLERE ORDRER";

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Kaldes af ScannerDisplay når spilleren scanner denne station.
    /// </summary>
    public void OnScanned()
    {
        OrderManager manager = OrderManager.Instance;
        ScannerDisplay display = FindAnyObjectByType<ScannerDisplay>();

        if (manager == null || display == null) return;

        // Tjek om der er en aktiv ordre der ikke er afleveret endnu
        if (manager.CurrentOrder != null)
        {
            display.ShowCustomMessage(notReadyMessage, display.colorWarning);
            return;
        }

        // Tjek om der er flere ordrer at give
        if (!manager.HasMoreOrders())
        {
            display.ShowCustomMessage(allDoneMessage, display.colorNormal);
            return;
        }

        // Giv næste ordre
        display.ShowCustomMessage(readyMessage, display.colorSuccess);
        manager.GiveNextOrder();
    }
}