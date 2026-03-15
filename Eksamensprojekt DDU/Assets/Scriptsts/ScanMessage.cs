using UnityEngine;

/// <summary>
/// Tilføj dette script til et GameObject for at vise en besked på
/// håndskanneren når spilleren scanner det.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et hvilket som helst GameObject.
///   2. Udfyld Message med den tekst der skal vises på skanneren.
///   3. Vælg MessageType for at styre farven på beskeden.
///   4. Objektet skal have en Collider så raycasting kan ramme det.
///
/// EKSEMPEL:
///   En mystisk kasse med scriptet og Message = "ADVARSEL: SKRØBELIGT"
///   viser den besked på skanneren når spilleren klikker på den.
/// </summary>
public class ScanMessage : MonoBehaviour
{
    public enum MessageType
    {
        Normal,   // hvid
        Success,  // grøn
        Warning,  // gul
        Error,    // rød
    }

    [Header("Besked")]
    [Tooltip("Teksten der vises på skannerens display.")]
    public string message = "";

    [Tooltip("Beskedtype styrer farven på displayet.")]
    public MessageType messageType = MessageType.Normal;

    [Tooltip("Hvis true afspilles beskeden kun én gang og ignoreres bagefter.")]
    public bool playOnce = false;

    // ── Runtime state ──────────────────────────────────────────────
    private bool _hasPlayed;

    // ──────────────────────────────────────────────────────────────
    /// <summary>
    /// Kaldes af ScannerDisplay når dette objekt scannes.
    /// Returnerer false hvis beskeden er brugt og playOnce er sat.
    /// </summary>
    public bool TryShow()
    {
        if (playOnce && _hasPlayed)
            return false;

        if (string.IsNullOrEmpty(message))
            return false;

        ScannerDisplay display = FindAnyObjectByType<ScannerDisplay>();
        if (display == null)
        {
            Debug.LogWarning("[ScanMessage] Fandt ikke ScannerDisplay i scenen.");
            return false;
        }

        switch (messageType)
        {
            case MessageType.Normal:
                display.ShowCustomMessage(message, display.colorNormal);
                break;
            case MessageType.Success:
                display.ShowCustomMessage(message, display.colorSuccess);
                break;
            case MessageType.Warning:
                display.ShowCustomMessage(message, display.colorWarning);
                break;
            case MessageType.Error:
                display.ShowCustomMessage(message, display.colorError);
                break;
        }

        _hasPlayed = true;
        return true;
    }
}