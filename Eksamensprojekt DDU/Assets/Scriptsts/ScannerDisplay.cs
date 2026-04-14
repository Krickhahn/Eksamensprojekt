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
///   LYD:
///   6. Tilføj en AudioSource-komponent til det samme GameObject som dette script.
///      (Play On Awake: OFF, Loop: OFF)
///   7. Opret en AudioClip (fx en kort bip) og træk den ind i Beep Clip-feltet.
///   8. Fyld Error Sounds og Warning Sounds med dine fejl/advarselslyde.
///      Én vælges tilfældigt per hændelse. Lad felterne stå tomme for ingen lyd.
///      Justér de tilhørende Volume-felter efter smag.
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

    [Tooltip("Layer mask til scanning — ekskludér layers du ikke vil ramme (f.eks. DeliveryZone-layeret).\nDefault er Everything.")]
    public LayerMask scanLayerMask = ~0;

    [Tooltip("ScannerLight-komponenten der flasher når skanneren bruges. Valgfrit.")]
    public ScannerLight scannerLight;

    [Header("Lyd — Scanning (bip)")]
    [Tooltip("AudioClip der afspilles når skanneren bruges (bip-lyd). Kræver en AudioSource på dette GameObject.")]
    public AudioClip beepClip;

    [Tooltip("Lydstyrke for bip-lyden (0–1).")]
    [Range(0f, 1f)]
    public float beepVolume = 1f;

    [Header("Lyd — Fejl (rød tekst)")]
    [Tooltip("Lyde der afspilles ved fejl-beskeder (colorError), fx forkert pakke eller forkert zone.\n" +
             "Én vælges tilfældigt. Lad feltet stå tomt for ingen ekstra lyd.")]
    public AudioClip[] errorSounds;

    [Tooltip("Lydstyrke for fejl-lyde (0–1).")]
    [Range(0f, 1f)]
    public float errorVolume = 1f;

    [Header("Lyd — Advarsel (gul tekst)")]
    [Tooltip("Lyde der afspilles ved advarsel-beskeder (colorWarning), fx 'PLACE PACKAGE'.\n" +
             "Én vælges tilfældigt. Lad feltet stå tomt for ingen ekstra lyd.")]
    public AudioClip[] warningSounds;

    [Tooltip("Lydstyrke for advarsel-lyde (0–1).")]
    [Range(0f, 1f)]
    public float warningVolume = 0.85f;

    [Header("Lyd — Succes (grøn tekst)")]
    [Tooltip("Lyde der afspilles når en pakke er korrekt skannet (ItemConfirmed).\n" +
             "Én vælges tilfældigt. Brug fx en positiv bip eller en godkendelseslyd.")]
    public AudioClip[] successSounds;

    [Tooltip("Lyde der afspilles når en ordre er fuldstændig afleveret (OrderComplete).\n" +
             "Én vælges tilfældigt. Brug fx en fanfare eller en mere markant succeslyd.")]
    public AudioClip[] orderCompleteSounds;

    [Tooltip("Lydstyrke for succes-lyde (0–1).")]
    [Range(0f, 1f)]
    public float successVolume = 1f;

    // ── Private ────────────────────────────────────────────────────
    private Camera _cam;
    private AudioSource _audio;
    private Coroutine _displayRoutine;
    private string _loopMessage;
    private Color _loopColor;

    private bool _orderHasBeenShown = false;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        _cam = Camera.main;

        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.loop = false;
            _audio.spatialBlend = 0f; // 2D lyd — ændres til 1f for 3D-lyd fra skanneren
        }
    }

    void Start()
    {
        StartCoroutine(InitDisplay());
    }

    IEnumerator InitDisplay()
    {
        yield return null;
        SetLoop("GO TO OFFICE", colorNormal);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.Locked)
        {
            PlayBeep();
            scannerLight?.Flash();

            if (!PickupObject.IsHoldingItem)
                TryScan();
        }
    }

    // ── Lyd ───────────────────────────────────────────────────────

    /// <summary>Afspiller bip-lyden én gang.</summary>
    void PlayBeep()
    {
        if (_audio == null || beepClip == null) return;
        _audio.PlayOneShot(beepClip, beepVolume);
    }

    /// <summary>Afspiller en tilfældig clip fra arrayet med den angivne lydstyrke.</summary>
    void PlayRandomSound(AudioClip[] clips, float volume)
    {
        if (_audio == null || clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
            _audio.PlayOneShot(clip, volume);
    }

    // ── Offentlige metoder kaldt af OrderManager ───────────────────

    public void ShowNewOrder(Order order)
    {
        _orderHasBeenShown = true;

        string time = ShiftTimer.Instance != null
                        ? $" [{ShiftTimer.Instance.GetTimeRemainingFormatted()}]"
                        : "";
        string location = !string.IsNullOrEmpty(order.spawnZoneName)
                        ? $"{order.spawnZoneName} >> "
                        : "";
        Color col = ShiftTimer.Instance?.Phase == ShiftTimer.ShiftPhase.Overtime ? colorWarning
                  : ShiftTimer.Instance?.Phase == ShiftTimer.ShiftPhase.Pressure ? colorWarning
                  : colorNormal;

        SetLoop($"{location}{order.itemID} -- {order.itemName}{time}", col);
    }

    public void ShowItemConfirmed(Order order)
    {
        PlayRandomSound(successSounds, successVolume);
        SetLoop(
            $"OK >> {order.deliveryZone?.zoneName ?? "?"}",
            colorSuccess
        );
    }

    public void ShowOrderComplete(Order order, System.Action onFinished = null)
    {
        PlayRandomSound(orderCompleteSounds, successVolume);
        StartDisplay(ScrollOnceThen(
            $"DONE +{order.earnedPoints}PT",
            colorSuccess,
            onFinished
        ));
    }

    IEnumerator ScrollOnceThen(string message, Color color, System.Action onFinished)
    {
        float delay = 1f / Mathf.Max(0.1f, scrollSpeed);
        yield return ScrollAcross(message, color, delay);
        onFinished?.Invoke();
    }

    public void ShowShiftEnded() => SetLoop("SHIFT IS OVER", colorWarning);

    public void ShowStandBy()
    {
        if (!_orderHasBeenShown) return;
        SetLoop("STAND BY", colorNormal);
    }

    public void ShowGoToOffice()
    {
        SetLoop("GO TO OFFICE", colorNormal);
    }

    public void ShowAllComplete() => SetLoop("ALL DONE", colorSuccess);

    public void ShowWrongItem(string scannedID, string expectedID)
    {
        PlayRandomSound(errorSounds, errorVolume);
        PlayOnce($"ERR: {scannedID} != {expectedID}", colorError);
    }

    public void ShowWrongZone(DeliveryZone expectedZone)
    {
        PlayRandomSound(errorSounds, errorVolume);
        PlayOnce($"ERR ZONE: {expectedZone?.zoneName ?? "?"}", colorError);
    }

    public void ShowPackageNotInZone(DeliveryZone zone)
    {
        PlayRandomSound(warningSounds, warningVolume);
        PlayOnce($"PLACE PACKAGE: {zone?.zoneName ?? "?"}", colorWarning);
    }

    public void ShowWrongPackageInZone(string expectedID)
    {
        PlayRandomSound(errorSounds, errorVolume);
        PlayOnce($"WRONG PACKAGE IN ZONE: {expectedID}", colorError);
    }

    public void ShowCustomMessage(string message, Color color)
    {
        // Afspil lyd baseret på farven — error = rød, warning = gul, ellers ingen ekstra lyd
        if (color == colorError)
            PlayRandomSound(errorSounds, errorVolume);
        else if (color == colorWarning)
            PlayRandomSound(warningSounds, warningVolume);

        PlayOnce(message, color);
    }

    public void ShowScanPackageFirst()
    {
        PlayRandomSound(warningSounds, warningVolume);
        PlayOnce("SCAN PACKAGE FIRST", colorWarning);
    }

    // ── Scanning ──────────────────────────────────────────────────

    void TryScan()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, scanRange, scanLayerMask))
        {
            PlayOnce("NO READ", colorNormal);
            return;
        }

        OrderStation station = hit.collider.GetComponentInParent<OrderStation>();
        if (station != null)
        {
            station.OnScanned();
            return;
        }

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
                PlayOnce("ALREADY SCANNED", colorNormal);
                break;
            case ScanResult.NoActiveOrder:
                PlayOnce("NO ORDERS", colorNormal);
                break;
        }
    }

    // ── Intern display-logik ───────────────────────────────────────

    void SetLoop(string message, Color color)
    {
        _loopMessage = message;
        _loopColor = color;
        StartDisplay(ScrollLoop(message, color));
    }

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

    IEnumerator ScrollLoop(string message, Color color)
    {
        float delay = 1f / Mathf.Max(0.1f, scrollSpeed);

        while (true)
        {
            yield return ScrollAcross(message, color, delay);
            yield return new WaitForSeconds(loopPause);
        }
    }

    IEnumerator ScrollOnce(string message, Color color, bool thenResume)
    {
        float delay = 1f / Mathf.Max(0.1f, scrollSpeed);
        yield return ScrollAcross(message, color, delay);

        if (thenResume && _loopMessage != null)
            StartDisplay(ScrollLoop(_loopMessage, _loopColor));
    }

    IEnumerator ScrollAcross(string message, Color color, float delay)
    {
        if (displayText == null) yield break;

        displayText.color = color;

        int leading = leadingSpaces > 0 ? leadingSpaces : displayWidth;
        string padded = new string(' ', leading) + message + new string(' ', displayWidth);

        for (int i = 0; i <= padded.Length - displayWidth; i++)
        {
            displayText.text = padded.Substring(i, displayWidth);
            yield return new WaitForSeconds(delay);
        }
    }
}