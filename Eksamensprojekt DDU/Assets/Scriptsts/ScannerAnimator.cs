using System.Collections;
using UnityEngine;

/// <summary>
/// Animerer håndskanneren så den glider ned og forsvinder når spilleren
/// samler en kasse op, og glider op igen når kassen sættes ned.
///
/// OPSÆTNING:
///   1. Tilføj dette script til dit Scanner-GameObject (child af kameraet).
///   2. Placer skanneren i den synlige position i scenen.
///   3. Højreklik på komponenten og vælg "Hent nuværende position som Show Position".
///   4. Juster Hide Position til hvor skanneren skal glide hen (f.eks. ned og til siden).
///   5. Træk ScannerAnimator ind i hvert PickupObject's Scanner Animator-felt.
/// </summary>
public class ScannerAnimator : MonoBehaviour
{
    [Header("Positioner (lokal)")]
    [Tooltip("Skannerens synlige position. Brug højreklik → 'Hent nuværende position som Show Position'.")]
    public Vector3 showPosition;

    [Tooltip("Skannerens skjulte position — skanneren glider hertil når spilleren holder en kasse.\n" +
             "Prøv (0.4, -0.4, 0) for at glide ned og til højre ud af billedet.")]
    public Vector3 hidePosition = new Vector3(0.4f, -0.4f, 0f);

    [Header("Animation")]
    [Tooltip("Sekunder animationen tager.")]
    public float slideDuration = 0.18f;

    [Tooltip("Animationskurve der styrer bevægelsesfølelsen.")]
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Private ────────────────────────────────────────────────────
    private Coroutine _slideRoutine;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        transform.localPosition = showPosition;
    }

    /// <summary>Glider skanneren til den skjulte position.</summary>
    public void Hide()
    {
        StartSlide(hidePosition);
    }

    /// <summary>Glider skanneren tilbage til den synlige position.</summary>
    public void Show()
    {
        StartSlide(showPosition);
    }

    // ──────────────────────────────────────────────────────────────
    void StartSlide(Vector3 target)
    {
        if (_slideRoutine != null)
            StopCoroutine(_slideRoutine);

        _slideRoutine = StartCoroutine(SlideTo(target));
    }

    IEnumerator SlideTo(Vector3 target)
    {
        Vector3 start = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float ct = slideCurve.Evaluate(t);

            transform.localPosition = Vector3.LerpUnclamped(start, target, ct);
            yield return null;
        }

        transform.localPosition = target;
    }

    // ── Editor hjælp ──────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Hent nuværende position som Show Position")]
    void CaptureShowPosition()
    {
        showPosition = transform.localPosition;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ScannerAnimator] Show Position sat til {showPosition}");
    }

    [ContextMenu("Hent nuværende position som Hide Position")]
    void CaptureHidePosition()
    {
        hidePosition = transform.localPosition;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ScannerAnimator] Hide Position sat til {hidePosition}");
    }
#endif
}