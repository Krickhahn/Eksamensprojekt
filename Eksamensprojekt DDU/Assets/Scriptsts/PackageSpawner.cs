using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawner pakker i grid-baserede spawn-zoner ved spilstart.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Udfyld Packages med alle pakke-prefabs der skal spawnes.
///   3. Opret SpawnZone-objekter i scenen og tilføj dem til Zones.
///   4. Juster Weight på hver zone — højere tal = større sandsynlighed.
///   5. Træk ScannerAnimator ind så den sættes automatisk på alle pakker.
///
/// GRID-SPAWNING:
///   Pakker placeres i pæne rækker og kolonner defineret af SpawnZone.
///   Spacing og Jitter justeres direkte på hver SpawnZone.
///   Hvis en zone løber tør for slots vælges en anden zone.
/// </summary>
public class PackageSpawner : MonoBehaviour
{
    [System.Serializable]
    public class WeightedZone
    {
        [Tooltip("SpawnZone-komponenten der definerer spawn-området.")]
        public SpawnZone zone;

        [Tooltip("Relativ sandsynlighed for at en pakke spawner her. Højere = mere sandsynligt.")]
        [Min(0.01f)]
        public float weight = 1f;
    }

    [Header("Pakker")]
    [Tooltip("Alle pakke-prefabs der skal spawnes i scenen.")]
    public List<GameObject> packages = new List<GameObject>();

    [Header("Spawn-zoner")]
    [Tooltip("Zoner hvor pakker kan spawne. Juster Weight per zone for sandsynlighed.")]
    public List<WeightedZone> zones = new List<WeightedZone>();

    [Header("Referencer")]
    [Tooltip("ScannerAnimator sættes automatisk på alle spawned pakkers PickupObject.")]
    public ScannerAnimator scannerAnimator;

    // ──────────────────────────────────────────────────────────────
    void Start()
    {
        SpawnAll();
    }

    void SpawnAll()
    {
        if (packages.Count == 0)
        {
            Debug.LogWarning("[PackageSpawner] Ingen pakker i listen!");
            return;
        }

        zones.RemoveAll(z => z.zone == null);
        if (zones.Count == 0)
        {
            Debug.LogWarning("[PackageSpawner] Ingen gyldige spawn-zoner!");
            return;
        }

        // Initialiser alle zones' grids
        foreach (var wz in zones)
            wz.zone.InitGrid();

        float totalWeight = 0f;
        foreach (var wz in zones) totalWeight += wz.weight;

        int spawned = 0;
        int failed = 0;

        // Lav en kopi så vi kan fjerne pakker der er tvunget til bestemte zoner
        var remainingPackages = new System.Collections.Generic.List<GameObject>(packages);
        remainingPackages.RemoveAll(p => p == null);

        // ── Trin 1: Placer pakker der er tvunget til specifikke zoner ──
        foreach (var wz in zones)
        {
            foreach (string reqID in wz.zone.requiredItemIDs)
            {
                if (string.IsNullOrEmpty(reqID)) continue;

                // Find første pakke der matcher dette ID
                GameObject match = remainingPackages.Find(p =>
                {
                    var s = p.GetComponent<Scannable>() ?? p.GetComponentInChildren<Scannable>();
                    return s != null && s.itemID.StartsWith(reqID);
                });

                if (match == null) continue;

                Vector3 half = GetPrefabHalfExtents(match);
                Vector3? slot = wz.zone.GetNextSlot(half.y);
                if (slot.HasValue)
                {
                    PlacePackage(match, slot.Value, wz.zone.GetSlotRotation(), wz.zone.zoneName);
                    remainingPackages.Remove(match);
                    spawned++;
                    Debug.Log($"[PackageSpawner] Tvungen placering: '{match.name}' → {wz.zone.gameObject.name}");
                }
            }
        }

        // ── Trin 2: Placer resterende pakker tilfældigt (respektér ekskludering) ──
        foreach (GameObject prefab in remainingPackages)
        {
            Vector3 halfExtents = GetPrefabHalfExtents(prefab);
            var s = prefab.GetComponent<Scannable>() ?? prefab.GetComponentInChildren<Scannable>();
            string itemID = s != null ? s.itemID : "";
            bool placed = false;

            // Vælg kun zoner der tillader denne pakke
            WeightedZone chosen = PickZoneForPackage(totalWeight, itemID, null);
            List<WeightedZone> tried = new List<WeightedZone>();

            while (chosen != null)
            {
                Vector3? slot = chosen.zone.GetNextSlot(halfExtents.y);
                if (slot.HasValue)
                {
                    PlacePackage(prefab, slot.Value, chosen.zone.GetSlotRotation(), chosen.zone.zoneName);
                    placed = true;
                    break;
                }

                tried.Add(chosen);
                chosen = PickZoneForPackage(totalWeight, itemID, tried);
            }

            if (placed) spawned++;
            else
            {
                failed++;
                Debug.LogWarning($"[PackageSpawner] Kunne ikke placere '{prefab.name}' — alle kompatible zoner er fulde eller ekskluderer pakken.");
            }
        }

        Debug.Log($"[PackageSpawner] Spawned {spawned} pakker. {failed} mislykkedes.");
    }

    // ──────────────────────────────────────────────────────────────

    void PlacePackage(GameObject prefab, Vector3 position, Quaternion rotation, string spawnZoneName = "")
    {
        GameObject spawned = Instantiate(prefab, position, rotation);

        Scannable scannable = spawned.GetComponentInChildren<Scannable>();
        if (scannable != null)
            OrderManager.Instance?.RegisterPackage(scannable, spawnZoneName);

        PickupObject pickup = spawned.GetComponentInChildren<PickupObject>();
        if (pickup != null && scannerAnimator != null)
            pickup.scannerAnimator = scannerAnimator;
    }

    /// <summary>Vælger en tilfældig zone der tillader pakken med dette ID og har ledige slots.</summary>
    WeightedZone PickZoneForPackage(float totalWeight, string itemID, List<WeightedZone> exclude)
    {
        float available = 0f;
        foreach (var wz in zones)
        {
            if (exclude != null && exclude.Contains(wz)) continue;
            if (wz.zone.AvailableSlots == 0) continue;
            if (!wz.zone.AllowsPackage(itemID)) continue;
            available += wz.weight;
        }

        if (available <= 0f) return null;

        float roll = Random.Range(0f, available);
        float cumulative = 0f;

        foreach (var wz in zones)
        {
            if (exclude != null && exclude.Contains(wz)) continue;
            if (wz.zone.AvailableSlots == 0) continue;
            if (!wz.zone.AllowsPackage(itemID)) continue;
            cumulative += wz.weight;
            if (roll <= cumulative) return wz;
        }

        return null;
    }

    Vector3 GetPrefabHalfExtents(GameObject prefab)
    {
        GameObject temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        temp.SetActive(false);

        Collider col = temp.GetComponentInChildren<Collider>();
        Vector3 half = col != null
            ? col.bounds.extents
            : new Vector3(0.25f, 0.25f, 0.25f);

        Destroy(temp);
        return half;
    }
}