using System;
using UnityEngine;

/// <summary>
/// Singleton event-bus der informerer alle fjender om spillerens gemmestatus.
/// Placer på et tomt GameObject i scenen (f.eks. "GameManager").
/// </summary>
public class HidingManager : MonoBehaviour
{
    public static HidingManager Instance { get; private set; }

    /// <summary>Fyres når spilleren gemmer sig (true) eller kommer ud (false).</summary>
    public event Action<bool> OnPlayerHidingChanged;

    public bool IsPlayerHiding { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetPlayerHiding(bool hiding)
    {
        if (IsPlayerHiding == hiding) return;
        IsPlayerHiding = hiding;
        OnPlayerHidingChanged?.Invoke(hiding);
    }
}