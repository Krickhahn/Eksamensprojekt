using UnityEngine;

public class RealtimeLightBar : MonoBehaviour
{
    [Header("Bar Settings")]
    public Transform bar;              // The object to scale (e.g., a cube)
    public float maxScale = 5f;        // Maximum height/length of the bar
    public float speed = 5f;           // Smoothness

    [Header("Light Settings")]
    public Light pointLight;          // Optional light component
    public float maxIntensity = 10f;

    [Header("Input Value")]
    [Range(0f, 1f)]
    public float value;               // The real-time value (0–1)

    private Vector3 initialScale;
    private Material barMaterial;

    void Start()
    {
        if (bar != null)
        {
            initialScale = bar.localScale;
            barMaterial = bar.GetComponent<Renderer>().material;
        }
    }

    void Update()
    {
        // Example: simulate real-time value (REMOVE if using real input)
        value = Mathf.PingPong(Time.time, 1f);

        UpdateBar(value);
    }

    public void UpdateBar(float input)
    {
        float targetScale = Mathf.Lerp(0.1f, maxScale, input);

        // Smooth scaling
        Vector3 newScale = initialScale;
        newScale.y = Mathf.Lerp(bar.localScale.y, targetScale, Time.deltaTime * speed);
        bar.localScale = newScale;

        // Adjust light intensity
        if (pointLight != null)
        {
            pointLight.intensity = Mathf.Lerp(0f, maxIntensity, input);
        }

        // Emission glow (if material supports emission)
        if (barMaterial != null)
        {
            Color emission = Color.Lerp(Color.black, Color.cyan * 5f, input);
            barMaterial.SetColor("_EmissionColor", emission);
        }
    }
}