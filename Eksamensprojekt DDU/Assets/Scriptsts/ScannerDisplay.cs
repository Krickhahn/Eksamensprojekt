using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Håndskanner-display der ruller tekst fra højre mod venstre.
///
/// OPSÆTNING:
///   1. Opret et World Space Canvas som child af dit Scanner-objekt (child af kameraet).
///   2. Tilføj ét TMP_Text felt inde i Canvas med:
///        - Wrapping: Disabled
///        - Overflow: Overflow
///        - Alignment: Left / Middle
///        - Font størrelse efter smag
///   3. Tilføj dette script til et GameObject i scenen.
///   4. Træk TMP_Text feltet ind i Display Text.
///   5. Træk dette script ind i OrderManager's Scanner UI felt.
///
/// DISPLAY OPFØRSEL:
///   Normal besked  → ruller fra højre mod venstre, gentager i loop
///   Fejlbesked     → ruller fra højre mod venstre én gang, stopper
///                    Efter stop vises den normale besked igen
///
/// SCANNING:
///   Venstreklik scanner det objekt midtskærmen peger på.
/// </summary>
public class ScannerDisplay : MonoBehaviour
{
    [Header("Referencer")]
    [Tooltip("TMP_Text feltet på skannerens display.")]
    public TMP_Text displayText;

    [Header("Display")]
    [Tooltip("Antal synlige tegn på displayet. Sæt størrelsen her og hold teksten inden for denne.")]
    public int displayWidth = 12;

    [Tooltip("Antal spaces der paddes foran teksten inden den ruller ind fra højre. 0 = brug displayWidth (fuld bredde).")]
    public int leadingSpaces = 0;

    [Tooltip("Tegn der ruller per sekund.")]
    public float scrollSpeed = 8f;

    [Tooltip("Sekunder displayet holder pause efter en besked er rullet igennem inden den gentages.")]
    public float loopPause = 1.0f;

    [Header("Farver")]
    public Color colorNormal = new Color(0.8f, 1f, 0.8f);
    public Color colorSuccess = new Color(0.3f, 1f, 0.4f);
    public Color colorWarning = new Color(1f, 0.85f, 0.2f);
    public Color colorError = new Color(1f, 0.3f, 0.3f);

    [Header("Scanning")]
    [Tooltip("Maks afstand for scanning.")]
    public float scanRange = 3f;

    // ── Private ────────────────────────────────────────────────────
    private Camera _cam;
    private Coroutine _displayRoutine;
    private string _loopMessage;      // den besked der kører i loop (ordreinfo)
    private Color _loopColor;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = Camera.main;
        SetLoop("STAND BY", colorNormal);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)
            && Cursor.lockState == CursorLockMode.Locked
            && !PickupObject.IsHoldingItem)
            TryScan();
    }

    // ── Offentlige metoder kaldt af OrderManager ───────────────────

    public void ShowNewOrder(Order order)
    {
        // Vis kun varenummer og navn — destination afsløres først efter scanning
        SetLoop(
            $"{order.itemID} -- {order.itemName}",
            colorNormal
        );
    }

    public void ShowItemConfirmed(Order order)
    {
        // Nu afsløres destinationen
        SetLoop(
            $"OK >> {order.deliveryZone?.zoneName ?? "?"}",
            colorSuccess
        );
    }

    public void ShowOrderComplete(Order order)
    {
        SetLoop("DONE", colorSuccess);
    }

    public void ShowAllComplete()
    {
        SetLoop("ALLE FERDIGE", colorSuccess);
    }

    public void ShowWrongItem(string scannedID, string expectedID)
    {
        PlayOnce($"FEJL: {scannedID} != {expectedID}", colorError);
    }

    public void ShowWrongZone(DeliveryZone expectedZone)
    {
        PlayOnce($"FEJL ZONE: {expectedZone?.zoneName ?? "?"}", colorError);
    }

    public void ShowPackageNotInZone(DeliveryZone zone)
    {
        PlayOnce($"PLACER PAKKE: {zone?.zoneName ?? "?"}", colorWarning);
    }

    public void ShowWrongPackageInZone(string expectedID)
    {
        PlayOnce($"FORKERT PAKKE I ZONE: {expectedID}", colorError);
    }

    public void ShowCustomMessage(string message, Color color)
    {
        PlayOnce(message, color);
    }

    public void ShowScanPackageFirst()
    {
        PlayOnce("SCAN PAKKE FORST", colorWarning);
    }

    // ── Scanning ──────────────────────────────────────────────────

    void TryScan()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, scanRange))
        {
            PlayOnce("NO READ", colorNormal);
            return;
        }

        // Tjek først om objektet har en ScanMessage
        ScanMessage scanMsg = hit.collider.GetComponentInParent<ScanMessage>();
        if (scanMsg != null)
        {
            scanMsg.TryShow();
            return;
        }

        Scannable scanned = hit.collider.GetComponentInParent<Scannable>();
        if (scanned == null)
        {
            PlayOnce("NO READ", colorNormal);
            return;
        }

        ScanResult result = OrderManager.Instance.TryScan(scanned);

        switch (result)
        {
            case ScanResult.AlreadyConfirmed:
                PlayOnce("ALLEREDE SCANNET", colorNormal);
                break;
            case ScanResult.NoActiveOrder:
                PlayOnce("INGEN ORDRE", colorNormal);
                break;
        }
    }

    // ── Intern display-logik ───────────────────────────────────────

    /// <summary>
    /// Sætter en besked der kører i uendeligt loop.
    /// Afbryder evt. fejlbesked med det samme.
    /// </summary>
    void SetLoop(string message, Color color)
    {
        _loopMessage = message;
        _loopColor = color;
        StartDisplay(ScrollLoop(message, color));
    }

    /// <summary>
    /// Spiller en besked én gang fra højre til venstre.
    /// Når den er færdig genoptages loop-beskeden.
    /// </summary>
    void PlayOnce(string message, Color color)
    {
        StartDisplay(ScrollOnce(message, color, thenResume: true));
    }

    void StartDisplay(IEnumerator routine)
    {
        if (_displayRoutine != null)
            StopCoroutine(_displayRoutine);

        _displayRoutine = StartCoroutine(routine);
    }

    // ── Scroll-coroutines ──────────────────────────────────────────

    /// <summary>
    /// Ruller beskeden fra højre mod venstre, gentager i loop.
    /// </summary>
    IEnumerator ScrollLoop(string message, Color color)
    {
        float delay = 1f / Mathf.Max(0.1f, scrollSpeed);

        while (true)
        {
            yield return ScrollAcross(message, color, delay);
            yield return new WaitForSeconds(loopPause);
        }
    }

    /// <summary>
    /// Ruller beskeden fra højre mod venstre én gang.
    /// Hvis thenResume er true genoptages loop-beskeden bagefter.
    /// </summary>
    IEnumerator ScrollOnce(string message, Color color, bool thenResume)
    {
        float delay = 1f / Mathf.Max(0.1f, scrollSpeed);
        yield return ScrollAcross(message, color, delay);

        if (thenResume && _loopMessage != null)
            StartDisplay(ScrollLoop(_loopMessage, _loopColor));
    }

    /// <summary>
    /// Kerne-scroll: ruller message ét tegn ad gangen fra højre mod venstre
    /// gennem et vindue på displayWidth tegn.
    ///
    /// Teksten starter helt ude til højre (displayWidth spaces foran)
    /// og forsvinder helt til venstre (displayWidth spaces bag efter).
    /// Ingen tegn vises uden for displayets bredde.
    /// </summary>
    IEnumerator ScrollAcross(string message, Color color, float delay)
    {
        if (displayText == null) yield break;

        displayText.color = color;

        // Pad foran med leadingSpaces (eller displayWidth hvis 0) så teksten starter usynlig fra højre
        int leading = leadingSpaces > 0 ? leadingSpaces : displayWidth;
        string padded = new string(' ', leading) + message + new string(' ', displayWidth);

        // Rul ét tegn ad gangen — vis altid præcis displayWidth tegn
        for (int i = 0; i <= padded.Length - displayWidth; i++)
        {
            displayText.text = padded.Substring(i, displayWidth);
            yield return new WaitForSeconds(delay);
        }
    }
}