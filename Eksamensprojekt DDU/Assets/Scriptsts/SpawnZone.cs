using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Definerer et område hvor pakker spawner i et pænt grid.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Justér Zone Size til at dække det ønskede spawn-område.
///   3. Justér Spacing til afstanden mellem pakkerne i griddet.
///   4. Træk objektet ind i PackageSpawner's Zones-liste.
///
/// GRID-LOGIK:
///   Zonen beregner automatisk antal rækker og kolonner ud fra
///   Zone Size og Spacing. PackageSpawner tildeles slots én ad gangen
///   i tilfældig rækkefølge så pakker spredes jævnt.
/// </summary>
public class SpawnZone : MonoBehaviour
{
    [Header("Zone")]
    [Tooltip("Størrelsen på spawn-området i meter (x = bredde, z = dybde).")]
    public Vector3 zoneSize = new Vector3(4f, 0.01f, 4f);

    [Header("Grid")]
    [Tooltip("Afstand mellem pakke-midtpunkter i griddet (x = vandret, z = dybde).")]
    public Vector2 spacing = new Vector2(0.6f, 0.6f);

    [Tooltip("Tilføj lidt tilfældig variation til hver pakkes position inden for sit slot.")]
    public float jitter = 0.05f;

    [Tooltip("Maks tilfældig Y-rotation i grader til hver side fra pakkens grundrotation.\n0 = ingen rotation, 180 = helt tilfældig.")]
    [Range(0f, 180f)]
    public float maxRotationAngle = 180f;

    [Header("Pakkefilter")]
    [Tooltip("Pakke-ID'er der ALDRIG må spawne i denne zone.\n" +
             "Skriv præfikser eller fulde ID'er, ét per felt — f.eks. 'PKG-Heavy' eller 'PKG-003'.")]
    public List<string> excludedItemIDs = new List<string>();

    [Tooltip("Pakke-ID'er der ALTID spawner i denne zone (hvis de er i puljen).\n" +
             "Disse pakker tildeles zonen som de første inden tilfældig fordeling.")]
    public List<string> requiredItemIDs = new List<string>();

    [Header("Gizmo")]
    public bool showGizmo = true;
    public Color gizmoColor = new Color(0f, 0.8f, 1f, 0.2f);

    // ── Slots ──────────────────────────────────────────────────────
    private List<Vector3> _availableSlots = new List<Vector3>();
    private bool _initialized = false;

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Bygger grid-slots ud fra zone-størrelse og spacing.
    /// Kaldes automatisk første gang GetNextSlot() bruges.
    /// </summary>
    public void InitGrid()
    {
        _availableSlots.Clear();

        int cols = Mathf.Max(1, Mathf.FloorToInt(zoneSize.x / spacing.x));
        int rows = Mathf.Max(1, Mathf.FloorToInt(zoneSize.z / spacing.y));

        // Beregn offset så griddet er centreret i zonen
        float startX = -(cols - 1) * spacing.x * 0.5f;
        float startZ = -(rows - 1) * spacing.y * 0.5f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Vector3 local = new Vector3(
                    startX + col * spacing.x + Random.Range(-jitter, jitter),
                    0f,
                    startZ + row * spacing.y + Random.Range(-jitter, jitter)
                );

                _availableSlots.Add(local);
            }
        }

        // Bland slots så pakker ikke altid starter øverst til venstre
        Shuffle(_availableSlots);
        _initialized = true;

        Debug.Log($"[SpawnZone] '{gameObject.name}' har {_availableSlots.Count} slots ({cols}x{rows}).");
    }

    /// <summary>
    /// Returnerer næste ledige slot i zonen.
    /// packageHalfHeight løfter pakken så den sidder oven på zone-planet.
    /// Returnerer null hvis alle slots er brugt.
    /// </summary>
    public Vector3? GetNextSlot(float packageHalfHeight)
    {
        if (!_initialized) InitGrid();

        if (_availableSlots.Count == 0)
            return null;

        Vector3 local = _availableSlots[0];
        _availableSlots.RemoveAt(0);

        local.y = packageHalfHeight;

        return transform.TransformPoint(local);
    }

    /// <summary>Antal ledige slots tilbage i denne zone.</summary>
    public int AvailableSlots
    {
        get
        {
            if (!_initialized) InitGrid();
            return _availableSlots.Count;
        }
    }

    /// <summary>Returnerer den rotation pakken skal have i dette slot.</summary>
    public Quaternion GetSlotRotation()
    {
        float yRot = Random.Range(-maxRotationAngle, maxRotationAngle);
        return Quaternion.Euler(0f, yRot, 0f);
    }

    // ──────────────────────────────────────────────────────────────
    /// <summary>Returnerer true hvis pakken med dette ID må spawne i zonen.</summary>
    public bool AllowsPackage(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return true;

        foreach (string ex in excludedItemIDs)
            if (!string.IsNullOrEmpty(ex) && itemID.StartsWith(ex))
                return false;

        return true;
    }

    /// <summary>Returnerer true hvis denne pakke er tvunget til at spawne her.</summary>
    public bool RequiresPackage(string itemID)
    {
        if (string.IsNullOrEmpty(itemID)) return false;

        foreach (string req in requiredItemIDs)
            if (!string.IsNullOrEmpty(req) && itemID.StartsWith(req))
                return true;

        return false;
    }

    void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Gizmo ─────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (!showGizmo) return;

        // Zone-ramme
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(Vector3.zero, zoneSize);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);

        // Grid-punkter
        int cols = Mathf.Max(1, Mathf.FloorToInt(zoneSize.x / spacing.x));
        int rows = Mathf.Max(1, Mathf.FloorToInt(zoneSize.z / spacing.y));

        float startX = -(cols - 1) * spacing.x * 0.5f;
        float startZ = -(rows - 1) * spacing.y * 0.5f;

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.8f);

        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
                Gizmos.DrawSphere(new Vector3(startX + col * spacing.x, 0.02f, startZ + row * spacing.y), 0.05f);
    }
}