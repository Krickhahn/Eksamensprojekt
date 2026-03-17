using UnityEngine;

/// <summary>
/// Markerer et fysisk afleveringssted i verdenen.
///
/// OPSÆTNING:
///   1. Opret et GameObject på afleveringsstedet (f.eks. en palet eller hylde).
///   2. Tilføj dette script + en Scannable (type = DeliveryZone).
///   3. Træk denne komponent ind i Scannable.deliveryZone-feltet.
///   4. Tilføj en Collider til zonen og sæt den til Is Trigger = true.
///      Størrelsen bestemmer det område pakken skal ligge inden for.
///   5. (Valgfrit) Tilføj en MeshRenderer med et visuelt materiale der
///      tændes/slukkes som highlight via SetHighlight().
/// </summary>
public class DeliveryZone : MonoBehaviour
{
    [Header("Info")]
    [Tooltip("Læsbart navn vist på HUD-skanneren, f.eks. 'Hylde B3' eller 'Lastbil 2'.")]
    public string zoneName = "Afleveringszone";

    [Tooltip("Krævet ID-præfiks på pakker der må placeres her, f.eks. 'B3'\n" +
             "Lad feltet stå tomt for at acceptere alle pakker.")]
    public string requiredItemID = "";

    [Header("Gizmo")]
    [Tooltip("Vis afleveringszonen i Scene View.")]
    public bool showGizmo = true;
    public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.25f);

    [Header("Visuals")]
    [Tooltip("Renderer der skifter materiale når zonen er aktiv. Kan være en pil, lysring, mm.")]
    public Renderer highlightRenderer;

    [Tooltip("Materiale der bruges når zonen er fremhævet (aktiv ordre).")]
    public Material activeMaterial;

    [Tooltip("Materiale der bruges når zonen er inaktiv.")]
    public Material inactiveMaterial;

    // ── Trigger tracking ───────────────────────────────────────────
    /// <summary>
    /// Den pakke der pt. ligger inden for zonen. Null hvis ingen pakke er inden for.
    /// Sættes automatisk via OnTriggerEnter/Exit.
    /// </summary>
    public PickupObject PackageInZone { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        PickupObject pkg = other.GetComponentInParent<PickupObject>();
        if (pkg == null) return;

        // Hvis zonen har et krævet ID, tjek at pakken matcher
        if (!string.IsNullOrEmpty(requiredItemID))
        {
            Scannable s = pkg.GetComponent<Scannable>();
            if (s == null || !s.itemID.StartsWith(requiredItemID))
                return; // forkert pakke — ignorer
        }

        PackageInZone = pkg;
        Debug.Log($"[DeliveryZone] {gameObject.name}: pakke '{pkg.gameObject.name}' er nu i zonen.");
    }

    void OnTriggerExit(Collider other)
    {
        PickupObject pkg = other.GetComponentInParent<PickupObject>();
        if (pkg != null && pkg == PackageInZone)
            PackageInZone = null;
        Debug.Log($"[DeliveryZone] {gameObject.name}: pakke forlod zonen.");
    }

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        // Sæt automatisk layer til "DeliveryZone" så scanner-raycastet kan ignorere det
        int layer = LayerMask.NameToLayer("DeliveryZone");
        if (layer != -1)
            gameObject.layer = layer;
        else
            Debug.LogWarning("[DeliveryZone] Layer 'DeliveryZone' findes ikke — opret det i Project Settings → Tags and Layers.");
    }

    // ── Gizmo ─────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Hent størrelsen fra trigger-collideren hvis den findes
        Collider col = GetComponent<Collider>();
        Vector3 size = col != null ? col.bounds.size : Vector3.one;
        Vector3 center = col != null ? col.bounds.center : transform.position;

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(center, size);

        // Vis zone-navn og præfiks som label
#if UNITY_EDITOR
        string label = string.IsNullOrEmpty(requiredItemID)
            ? zoneName
            : $"{zoneName} [{requiredItemID}]";
        UnityEditor.Handles.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.1f), label);
#endif
    }

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