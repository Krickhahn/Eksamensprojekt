using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Styrer spawn af fjender baseret på tid og afleverede pakker.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Tilføj fjende-prefabs til enemies listen og sæt en vægt på hver.
///      Højere vægt = spawner oftere. Fx vægt 3 spawner 3x så tit som vægt 1.
///   3. Opret EnemySpawnPoint-objekter i scenen og tilføj dem til spawnPoints.
///   4. Tilføj dette script som listener på OrderManager.onOrderComplete i Inspector.
///
/// INTEGRATION MED EKSISTERENDE SCRIPTS:
///   - Lytter på OrderManager.onOrderComplete for at øge spawn-chancen.
///   - Læser ScoreManager.TotalScore hvis du vil bruge score som faktor (valgfrit).
///   - Maks antal aktive fjender styres af maxActiveEnemies.
/// </summary>
public class EnemySpawnManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────
    public static EnemySpawnManager Instance { get; private set; }

    // ── Vægtet fjende-entry ────────────────────────────────────────
    [System.Serializable]
    public class WeightedEnemy
    {
        [Tooltip("Fjende-prefabben der skal spawnes.")]
        public GameObject prefab;

        [Tooltip("Relativ spawn-vægt. Højere tal = spawner oftere.\n" +
                 "Eksempel: Vagt=3, Rotte=1 → vagten spawner 3x så tit som rotten.")]
        [Min(0.01f)]
        public float weight = 1f;

        [Tooltip("Læsbart navn til debug-log. Udfyldes automatisk hvis tomt.")]
        public string displayName = "";
    }

    // ── Inspector ──────────────────────────────────────────────────
    [Header("Fjender")]
    [Tooltip("Alle fjende-typer der kan spawnes. Justér Weight per fjende for hyppighed.")]
    public List<WeightedEnemy> enemies = new List<WeightedEnemy>();

    [Header("Spawn-punkter")]
    [Tooltip("Faste spawn-punkter i scenen (indgange, mørke hjørner osv.).")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Spawn-chance")]
    [Tooltip("Grundchance for spawn per tick (0–1). Fx 0.1 = 10% chance.")]
    [Range(0f, 1f)]
    public float baseSpawnChance = 0.05f;

    [Tooltip("Hvor meget chance stiger per afleveret pakke (0–1).")]
    [Range(0f, 0.2f)]
    public float chancePerPackage = 0.03f;

    [Tooltip("Hvor meget chance stiger per minut spillet har kørt (0–1).")]
    [Range(0f, 0.2f)]
    public float chancePerMinute = 0.05f;

    [Tooltip("Maksimal samlet spawn-chance uanset tid og pakker (0–1).")]
    [Range(0f, 1f)]
    public float maxSpawnChance = 0.8f;

    [Header("Spawn-interval")]
    [Tooltip("Sekunder mellem hvert spawn-forsøg.")]
    public float spawnTickInterval = 15f;

    [Header("Begrænsninger")]
    [Tooltip("Maks antal aktive fjender i scenen på én gang.")]
    public int maxActiveEnemies = 4;

    [Tooltip("Statuen tæller IKKE med i maxActiveEnemies — den håndteres separat.")]
    public bool excludeStatueFromCount = true;

    // ── Runtime state ──────────────────────────────────────────────
    private int _packagesDelivered = 0;
    private float _timeElapsed = 0f;
    private List<GameObject> _activeEnemies = new List<GameObject>();

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // Lyt på OrderManager's onOrderComplete event
        if (OrderManager.Instance != null)
            OrderManager.Instance.onOrderComplete.AddListener(OnPackageDelivered);
        else
            Debug.LogWarning("[EnemySpawnManager] OrderManager ikke fundet — pakke-integration virker ikke.");

        StartCoroutine(SpawnTick());
    }

    void Update()
    {
        _timeElapsed += Time.deltaTime;

        // Ryd døde/ødelagte fjender fra listen løbende
        _activeEnemies.RemoveAll(e => e == null);
    }

    void OnDestroy()
    {
        if (OrderManager.Instance != null)
            OrderManager.Instance.onOrderComplete.RemoveListener(OnPackageDelivered);
    }

    // ── Event fra OrderManager ─────────────────────────────────────

    /// <summary>
    /// Kaldes automatisk af OrderManager.onOrderComplete hver gang
    /// spilleren afleverer en pakke korrekt.
    /// </summary>
    void OnPackageDelivered(Order completedOrder)
    {
        _packagesDelivered++;
        Debug.Log($"[EnemySpawnManager] Pakke afleveret ({_packagesDelivered} total) — spawn-chance stiger.");
    }

    // ── Spawn-logik ────────────────────────────────────────────────

    IEnumerator SpawnTick()
    {
        // Vent lidt inden første tick så scenen er loaded
        yield return new WaitForSeconds(spawnTickInterval);

        while (true)
        {
            TrySpawn();
            yield return new WaitForSeconds(spawnTickInterval);
        }
    }

    void TrySpawn()
    {
        // Tjek maks-grænse
        int activeCount = GetActiveEnemyCount();
        if (activeCount >= maxActiveEnemies)
        {
            Debug.Log($"[EnemySpawnManager] Maks fjender nået ({activeCount}/{maxActiveEnemies}) — springer spawn over.");
            return;
        }

        float chance = CalculateSpawnChance();
        float roll = Random.value;

        Debug.Log($"[EnemySpawnManager] Spawn-tick: chance={chance:P0}, roll={roll:P0}");

        if (roll <= chance)
            SpawnEnemy();
    }

    float CalculateSpawnChance()
    {
        float minutesElapsed = _timeElapsed / 60f;

        float chance = baseSpawnChance
                     + (_packagesDelivered * chancePerPackage)
                     + (minutesElapsed * chancePerMinute);

        return Mathf.Clamp(chance, 0f, maxSpawnChance);
    }

    void SpawnEnemy()
    {
        if (enemies.Count == 0 || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[EnemySpawnManager] Ingen fjender eller spawn-punkter sat op!");
            return;
        }

        // Vælg fjende via vægtet tilfældig udvælgelse
        WeightedEnemy chosen = PickWeightedEnemy();
        if (chosen == null || chosen.prefab == null)
        {
            Debug.LogWarning("[EnemySpawnManager] Kunne ikke vælge en fjende — tjek at prefabs er sat op.");
            return;
        }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject enemy = Instantiate(chosen.prefab, point.position, point.rotation);
        _activeEnemies.Add(enemy);

        string name = string.IsNullOrEmpty(chosen.displayName) ? chosen.prefab.name : chosen.displayName;
        Debug.Log($"[EnemySpawnManager] Spawned '{name}' (vægt {chosen.weight}) ved '{point.name}'. Aktive fjender: {GetActiveEnemyCount()}");
    }

    /// <summary>
    /// Vælger en fjende via vægtet tilfældig udvælgelse.
    /// En fjende med vægt 3 spawner 3x så tit som en med vægt 1.
    /// </summary>
    WeightedEnemy PickWeightedEnemy()
    {
        float totalWeight = 0f;
        foreach (var e in enemies)
        {
            if (e.prefab != null)
                totalWeight += e.weight;
        }

        if (totalWeight <= 0f) return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var e in enemies)
        {
            if (e.prefab == null) continue;
            cumulative += e.weight;
            if (roll <= cumulative)
                return e;
        }

        // Fallback — returnér første gyldige entry
        return enemies.Find(e => e.prefab != null);
    }

    int GetActiveEnemyCount()
    {
        _activeEnemies.RemoveAll(e => e == null);
        return _activeEnemies.Count;
    }

    // ── Offentlige metoder ─────────────────────────────────────────

    /// <summary>
    /// Returnerer den nuværende beregnede spawn-chance.
    /// Kan bruges til UI eller debug.
    /// </summary>
    public float GetCurrentSpawnChance() => CalculateSpawnChance();

    /// <summary>
    /// Registrerer en fjende manuelt — brug dette hvis din statue
    /// eller andre fjender spawnes uden om EnemySpawnManager.
    /// </summary>
    public void RegisterEnemy(GameObject enemy)
    {
        if (enemy != null && !_activeEnemies.Contains(enemy))
            _activeEnemies.Add(enemy);
    }

    /// <summary>
    /// Fjerner en fjende fra den aktive liste.
    /// Kald dette fra fjendernes OnDeath/OnDestroy.
    /// </summary>
    public void UnregisterEnemy(GameObject enemy)
    {
        _activeEnemies.Remove(enemy);
    }

    // ── Gizmo ──────────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        foreach (Transform point in spawnPoints)
        {
            if (point == null) continue;
            Gizmos.DrawWireSphere(point.position, 0.4f);
            UnityEditor.Handles.Label(point.position + Vector3.up * 0.5f, point.name);
        }
    }
#endif
}