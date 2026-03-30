using UnityEngine;

/// <summary>
/// Roterer et Directional Light så det følger arbejdsskiftets timer.
/// Solen starter i en natte-position og stiger til en dagtids-position
/// når skiftet er slut.
///
/// OPSÆTNING:
///   1. Tilføj dette script til et tomt GameObject.
///   2. Træk dit Directional Light ind i Sun Light-feltet.
///   3. Justér Night Rotation og Day Rotation til at matche din scenes
///      nat- og dagslys-vinkler.
///   4. Brug knapperne i Inspector (højreklik → kontekstmenu) til at
///      sætte rotationerne fra lysets nuværende position.
/// </summary>
public class SunController : MonoBehaviour
{
    [Header("Referencer")]
    [Tooltip("Directional Light der repræsenterer solen.")]
    public Light sunLight;

    [Header("Rotationer")]
    [Tooltip("Lysets rotation ved skiftets start (nat/tidlig morgen).")]
    public Vector3 nightRotation = new Vector3(10f, 170f, 0f);

    [Tooltip("Lysets rotation når skiftet slutter (solopgang).")]
    public Vector3 dayRotation = new Vector3(15f, 50f, 0f);

    [Header("Indstillinger")]
    [Tooltip("Hvis til fortsætter solen med at bevæge sig efter skiftet er slut.")]
    public bool continueAfterShiftEnd = false;

    [Tooltip("Hastighed i grader per sekund solen bevæger sig efter skiftet er slut (kun hvis Continue After Shift End er til).")]
    public float postShiftSpeed = 2f;

    [Tooltip("Animationskurve der styrer solens hastighed gennem skiftet.\nVenstre = skiftets start, højre = skiftets slut.\nBrug en S-kurve for langsom bevægelse i starten og hurtigere mod slutningen.")]
    public AnimationCurve progressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Private ────────────────────────────────────────────────────
    private bool _shiftEnded;
    private float _postShiftProgress;

    // ──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (sunLight == null)
            sunLight = GetComponent<Light>() ?? FindAnyObjectByType<Light>();

        // Start i nat-rotation
        if (sunLight != null)
            sunLight.transform.rotation = Quaternion.Euler(nightRotation);
    }

    void Start()
    {
        if (ShiftTimer.Instance != null)
            ShiftTimer.Instance.onShiftEnd.AddListener(OnShiftEnded);
        else
            Debug.LogWarning("[SunController] ShiftTimer ikke fundet — solen bevæger sig ikke.");
    }

    void OnDestroy()
    {
        if (ShiftTimer.Instance != null)
            ShiftTimer.Instance.onShiftEnd.RemoveListener(OnShiftEnded);
    }

    void Update()
    {
        if (sunLight == null) return;

        if (!_shiftEnded)
        {
            // Følg ShiftTimer's progress
            if (ShiftTimer.Instance == null) return;

            float t = progressCurve.Evaluate(ShiftTimer.Instance.Progress);
            Quaternion rot = Quaternion.Lerp(
                Quaternion.Euler(nightRotation),
                Quaternion.Euler(dayRotation),
                t
            );
            sunLight.transform.rotation = rot;
        }
        else if (continueAfterShiftEnd)
        {
            // Fortsæt med at dreje solen efter skiftet er slut
            sunLight.transform.Rotate(Vector3.right, postShiftSpeed * Time.deltaTime, Space.World);
        }
    }

    void OnShiftEnded()
    {
        _shiftEnded = true;

        // Snap til præcis dag-rotation når skiftet slutter
        if (sunLight != null)
            sunLight.transform.rotation = Quaternion.Euler(dayRotation);
    }

    // ── Editor hjælp ──────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Sæt Night Rotation fra lysets nuværende rotation")]
    void CaptureNightRotation()
    {
        if (sunLight != null)
        {
            nightRotation = sunLight.transform.eulerAngles;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[SunController] Night Rotation sat til {nightRotation}");
        }
    }

    [ContextMenu("Sæt Day Rotation fra lysets nuværende rotation")]
    void CaptureDayRotation()
    {
        if (sunLight != null)
        {
            dayRotation = sunLight.transform.eulerAngles;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[SunController] Day Rotation sat til {dayRotation}");
        }
    }
#endif
}