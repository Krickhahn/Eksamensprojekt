using System.Collections;
using UnityEngine;

/// <summary>
/// FloorFogTrigger
/// Attach to the Player.
/// Detects the floor tag below the player and smoothly transitions
/// RenderSettings fog density and colour to the matching preset.
/// </summary>
public class FloorFogTrigger : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    //  Floor Tags
    // ─────────────────────────────────────────────────────────────
    [Header("Floor Tags")]
    public string floor1Tag = "Floor1";
    public string floor2Tag = "Floor2";
    public string floor3Tag = "Floor3";
    public string floor4Tag = "Floor4";

    // ─────────────────────────────────────────────────────────────
    //  Fog Densities
    // ─────────────────────────────────────────────────────────────
    [Header("Fog Densities")]
    [Range(0f, 1f)] public float floor1FogDensity = 0.005f;
    [Range(0f, 1f)] public float floor2FogDensity = 0.020f;
    [Range(0f, 1f)] public float floor3FogDensity = 0.050f;
    [Range(0f, 1f)] public float floor4FogDensity = 0.090f;

    // ─────────────────────────────────────────────────────────────
    //  Fog Colours
    // ─────────────────────────────────────────────────────────────
    [Header("Fog Colours")]
    public Color floor1FogColor = new Color(0.80f, 0.90f, 1.00f);
    public Color floor2FogColor = new Color(0.60f, 0.70f, 0.60f);
    public Color floor3FogColor = new Color(0.30f, 0.20f, 0.20f);
    public Color floor4FogColor = new Color(0.10f, 0.05f, 0.15f);

    // ─────────────────────────────────────────────────────────────
    //  Transition & Detection
    // ─────────────────────────────────────────────────────────────
    [Header("Transition")]
    [Range(0.1f, 30f)]
    public float transitionDuration = 3f;

    [Header("Detection")]
    [Range(0.05f, 1f)]
    public float checkInterval = 0.2f;
    public float raycastDistance = 5f;

    // ─────────────────────────────────────────────────────────────
    //  Private state
    // ─────────────────────────────────────────────────────────────
    private string _currentFloorTag = "";
    private Coroutine _transitionCoroutine;

    private void Start()
    {
        RenderSettings.fog = true;
        InvokeRepeating(nameof(CheckFloor), 0f, checkInterval);
    }

    private void CheckFloor()
    {
        if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, raycastDistance))
            return;

        string tag = hit.collider.tag;

        if (tag == _currentFloorTag) return;
        if (tag != floor1Tag && tag != floor2Tag && tag != floor3Tag && tag != floor4Tag) return;

        _currentFloorTag = tag;

        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);

        _transitionCoroutine = StartCoroutine(TransitionFog(GetDensityForTag(tag), GetColorForTag(tag)));
    }

    private IEnumerator TransitionFog(float targetDensity, Color targetColor)
    {
        float elapsed = 0f;
        float startDensity = RenderSettings.fogDensity;
        Color startColor = RenderSettings.fogColor;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));

            RenderSettings.fogDensity = Mathf.Lerp(startDensity, targetDensity, t);
            RenderSettings.fogColor = Color.Lerp(startColor, targetColor, t);

            yield return null;
        }

        RenderSettings.fogDensity = targetDensity;
        RenderSettings.fogColor = targetColor;
    }

    private float GetDensityForTag(string tag)
    {
        if (tag == floor1Tag) return floor1FogDensity;
        if (tag == floor2Tag) return floor2FogDensity;
        if (tag == floor3Tag) return floor3FogDensity;
        if (tag == floor4Tag) return floor4FogDensity;
        return RenderSettings.fogDensity;
    }

    private Color GetColorForTag(string tag)
    {
        if (tag == floor1Tag) return floor1FogColor;
        if (tag == floor2Tag) return floor2FogColor;
        if (tag == floor3Tag) return floor3FogColor;
        if (tag == floor4Tag) return floor4FogColor;
        return RenderSettings.fogColor;
    }
}