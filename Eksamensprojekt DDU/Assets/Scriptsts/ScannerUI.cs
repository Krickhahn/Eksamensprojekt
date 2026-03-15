using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD-håndskanner der viser den aktive ordre og scanningsresultater.
///
/// OPSÆTNING:
///   1. Opret et Canvas (Screen Space - Overlay eller Camera).
///   2. Lav en UI-panel der ligner en håndskanner.
///   3. Tilføj dette script til et GameObject og udfyld UI-referencerne.
///   4. Træk ScannerUI ind i OrderManager's Inspector-felt.
///
/// SCANNING:
///   Spilleren trykker venstreklik (Mouse Button 0) for at scanne.
///   Hvis spilleren holder en kasse (PickupObject._isHeld), ignoreres klikket
///   så man ikke scanner ved et uheld når man samler noget op.
/// </summary>
public class ScannerUI : MonoBehaviour
{
    [Header("Scan-indstillinger")]
    [Tooltip("Maks afstand for scanning.")]
    public float scanRange = 3f;

    [Header("UI — Ordre-visning")]
    [Tooltip("Tekst der viser varenummeret på den aktive ordre.")]
    public TMP_Text itemIDText;

    [Tooltip("Tekst der viser varenavnet på den aktive ordre.")]
    public TMP_Text itemNameText;

    [Tooltip("Tekst der viser destinationen (zone-navn).")]
    public TMP_Text destinationText;

    [Tooltip("Tekst der viser om pakken er bekræftet endnu.")]
    public TMP_Text itemStatusText;

    [Header("UI — Statusbesked")]
    [Tooltip("Tekst til midlertidige statusbeskeder (fx 'Forkert pakke!').")]
    public TMP_Text feedbackText;

    [Tooltip("Hvor mange sekunder feedback-teksten vises.")]
    public float feedbackDuration = 2.5f;

    [Header("UI — Farver")]
    public Color colorNeutral = Color.white;
    public Color colorSuccess = new Color(0.3f, 1f, 0.4f);
    public Color colorWarning = new Color(1f, 0.85f, 0.2f);
    public Color colorError = new Color(1f, 0.3f, 0.3f);

    // ── Private ────────────────────────────────────────────────────
    private Camera _cam;
    private Coroutine _feedbackRoutine;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = Camera.main;
        ClearFeedback();
        ClearOrderDisplay();
    }

    void Update()
    {
        // Venstreklik scanner — men ikke mens musen er låst op (f.eks. i menu)
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
            TryScan();
    }

    // ──────────────────────────────────────────────────────────────
    void TryScan()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, scanRange))
        {
            ShowFeedback("Intet at scanne", colorNeutral);
            return;
        }

        // Søg Scannable på objektet og dets forældre
        Scannable scanned = hit.collider.GetComponentInParent<Scannable>();

        if (scanned == null)
        {
            ShowFeedback("Kan ikke scannes", colorNeutral);
            return;
        }

        // Send til OrderManager og vis resultat
        ScanResult result = OrderManager.Instance.TryScan(scanned);
        HandleScanResult(result);
    }

    void HandleScanResult(ScanResult result)
    {
        switch (result)
        {
            case ScanResult.ItemCorrect:
                // Håndteres af ShowItemConfirmed()
                break;
            case ScanResult.OrderComplete:
                // Håndteres af ShowOrderComplete()
                break;
            case ScanResult.WrongItem:
                // Håndteres af ShowWrongItem()
                break;
            case ScanResult.WrongZone:
                // Håndteres af ShowWrongZone()
                break;
            case ScanResult.AlreadyConfirmed:
                ShowFeedback("Pakke allerede scannet", colorNeutral);
                break;
            case ScanResult.ItemNotConfirmedYet:
                // Håndteres af ShowScanPackageFirst()
                break;
            case ScanResult.NoActiveOrder:
                ShowFeedback("Ingen aktiv ordre", colorNeutral);
                break;
        }
    }

    // ── Kaldes af OrderManager ─────────────────────────────────────

    public void ShowNewOrder(Order order)
    {
        if (itemIDText) itemIDText.text = $"Varenr: {order.itemID}";
        if (itemNameText) itemNameText.text = order.itemName;
        if (destinationText) destinationText.text = $"Destination: {order.deliveryZone?.zoneName ?? "?"}";
        if (itemStatusText)
        {
            itemStatusText.text = "Find og scan pakken";
            itemStatusText.color = colorNeutral;
        }
        ShowFeedback($"Ny ordre: {order.itemName}", colorNeutral);
    }

    public void ShowItemConfirmed(Order order)
    {
        if (itemStatusText)
        {
            itemStatusText.text = "✓ Pakke bekræftet";
            itemStatusText.color = colorSuccess;
        }
        ShowFeedback($"Korrekt! Aflevér ved {order.deliveryZone?.zoneName}", colorSuccess);
    }

    public void ShowWrongItem(string scannedID, string expectedID)
    {
        ShowFeedback($"Forkert pakke!\nFandt: {scannedID}\nForventet: {expectedID}", colorError);
    }

    public void ShowWrongZone(DeliveryZone expectedZone)
    {
        ShowFeedback($"Forkert sted!\nGå til: {expectedZone?.zoneName ?? "?"}", colorError);
    }

    public void ShowScanPackageFirst()
    {
        ShowFeedback("Scan pakken først!", colorWarning);
    }

    public void ShowOrderComplete(Order order)
    {
        if (itemStatusText)
        {
            itemStatusText.text = "✓ Afleveret";
            itemStatusText.color = colorSuccess;
        }
        ShowFeedback($"Ordre fuldført: {order.itemName}", colorSuccess);
    }

    public void ShowAllComplete()
    {
        ClearOrderDisplay();
        ShowFeedback("Alle ordrer er fuldført!", colorSuccess);
        Debug.Log("[ScannerUI] Alle ordrer fuldført.");
    }

    // ── Hjælpemetoder ──────────────────────────────────────────────

    void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;

        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackText.gameObject.SetActive(true);

        _feedbackRoutine = StartCoroutine(ClearFeedbackAfterDelay());
    }

    IEnumerator ClearFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);
        ClearFeedback();
    }

    void ClearFeedback()
    {
        if (feedbackText == null) return;
        feedbackText.text = "";
        feedbackText.gameObject.SetActive(false);
    }

    void ClearOrderDisplay()
    {
        if (itemIDText) itemIDText.text = "";
        if (itemNameText) itemNameText.text = "";
        if (destinationText) destinationText.text = "";
        if (itemStatusText) itemStatusText.text = "";
    }
}