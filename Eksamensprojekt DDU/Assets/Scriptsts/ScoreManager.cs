using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Styrer spillerens point.
/// Er et singleton — tilgås via ScoreManager.Instance.
///
/// OPSÆTNING:
///   1. Opret et tomt GameObject og tilføj dette script.
///   2. Point vises på skannerdisplayet når en ordre fuldføres.
///      Der er ingen HUD på kameraet.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Events (valgfrit)")]
    [Tooltip("Kaldes når scoren ændres — sender ny totalscore.")]
    public UnityEvent<int> onScoreChanged;

    // ── Runtime state ──────────────────────────────────────────────
    public int TotalScore { get; private set; }

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Tilføjer point til totalen og fyrer event.</summary>
    public void AddScore(int points)
    {
        TotalScore += points;
        onScoreChanged?.Invoke(TotalScore);
        Debug.Log($"[ScoreManager] +{points} point — total: {TotalScore}");
    }

    /// <summary>Trækker point fra totalen (minimum 0).</summary>
    public void SubtractScore(int points)
    {
        TotalScore = Mathf.Max(0, TotalScore - points);
        onScoreChanged?.Invoke(TotalScore);
        Debug.Log($"[ScoreManager] -{points} point — total: {TotalScore}");
    }
}