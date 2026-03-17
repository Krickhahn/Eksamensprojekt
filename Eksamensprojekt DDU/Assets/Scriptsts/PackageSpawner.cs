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

        foreach (GameObject prefab in packages)
        {
            if (prefab == null) continue;

            Vector3 halfExtents = GetPrefabHalfExtents(prefab);
            bool placed = false;

            // Forsøg at placere i en vægtet zone — prøv alle zoner hvis valgte er fuld
            WeightedZone chosen = PickZone(totalWeight);
            List<WeightedZone> tried = new List<WeightedZone>();

            while (chosen != null)
            {
                Vector3? slot = chosen.zone.GetNextSlot(halfExtents.y);

                if (slot.HasValue)
                {
                    Quaternion rot = chosen.zone.GetSlotRotation();
                    PlacePackage(prefab, slot.Value, rot);
                    placed = true;
                    break;
                }

                // Zonen er fuld — prøv en anden
                tried.Add(chosen);
                chosen = PickZoneExcluding(totalWeight, tried);
            }

            if (placed) spawned++;
            else
            {
                failed++;
                Debug.LogWarning($"[PackageSpawner] Alle zoner er fulde — kunne ikke placere '{prefab.name}'.");
            }
        }

        Debug.Log($"[PackageSpawner] Spawned {spawned} pakker. {failed} mislykkedes.");
    }

    // ──────────────────────────────────────────────────────────────

    void PlacePackage(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject spawned = Instantiate(prefab, position, rotation);

        Scannable scannable = spawned.GetComponentInChildren<Scannable>();
        if (scannable != null)
            OrderManager.Instance?.RegisterPackage(scannable);

        PickupObject pickup = spawned.GetComponentInChildren<PickupObject>();
        if (pickup != null && scannerAnimator != null)
            pickup.scannerAnimator = scannerAnimator;
    }

    WeightedZone PickZone(float totalWeight)
    {
        return PickZoneExcluding(totalWeight, null);
    }

    WeightedZone PickZoneExcluding(float totalWeight, List<WeightedZone> exclude)
    {
        // Byg liste over tilgængelige zoner
        float available = 0f;
        foreach (var wz in zones)
        {
            if (exclude != null && exclude.Contains(wz)) continue;
            if (wz.zone.AvailableSlots == 0) continue;
            available += wz.weight;
        }

        if (available <= 0f) return null;

        float roll = Random.Range(0f, available);
        float cumulative = 0f;

        foreach (var wz in zones)
        {
            if (exclude != null && exclude.Contains(wz)) continue;
            if (wz.zone.AvailableSlots == 0) continue;
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