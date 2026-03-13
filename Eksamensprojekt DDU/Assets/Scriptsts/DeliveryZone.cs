using UnityEngine;

/// <summary>
/// Markerer et fysisk afleveringssted i verdenen.
///
/// OPSÆTNING:
///   1. Opret et GameObject på afleveringsstedet (f.eks. en palet eller hylde).
///   2. Tilføj dette script + en Scannable (type = DeliveryZone).
///   3. Træk denne komponent ind i Scannable.deliveryZone-feltet.
///   4. (Valgfrit) Tilføj en MeshRenderer med et visuelt materiale der
///      tændes/slukkes som highlight via SetHighlight().
/// </summary>
public class DeliveryZone : MonoBehaviour
{
    [Header("Info")]
    [Tooltip("Læsbart navn vist på HUD-skanneren, f.eks. 'Hylde B3' eller 'Lastbil 2'.")]
    public string zoneName = "Afleveringszone";

    [Header("Visuals")]
    [Tooltip("Renderer der skifter materiale når zonen er aktiv. Kan være en pil, lysring, mm.")]
    public Renderer highlightRenderer;

    [Tooltip("Materiale der bruges når zonen er fremhævet (aktiv ordre).")]
    public Material activeMaterial;

    [Tooltip("Materiale der bruges når zonen er inaktiv.")]
    public Material inactiveMaterial;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        // Start inaktiv
        SetHighlight(false);
    }

    /// <summary>
    /// Tænder eller slukker det visuelle highlight på afleveringszonen.
    /// Kaldes automatisk af OrderManager.
    /// </summary>
    public void SetHighlight(bool active)
    {
        if (highlightRenderer == null) return;

        Material mat = active ? activeMaterial : inactiveMaterial;
        if (mat != null)
            highlightRenderer.material = mat;

        highlightRenderer.enabled = active || inactiveMaterial != null;
    }
}