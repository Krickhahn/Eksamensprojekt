using UnityEngine;

public class FluorescentBarLight : MonoBehaviour
{
    [Header("Light Properties")]
    [SerializeField] private float barLength = 5f;
    [SerializeField] private float barWidth = 0.1f;
    [SerializeField] private float intensity = 2f;
    [SerializeField] private Color lightColor = new Color(0.9f, 0.95f, 1f); // Cool white

    [Header("Flicker Effect")]
    [SerializeField] private bool enableFlicker = true;
    [SerializeField] private float flickerSpeed = 0.05f;
    [SerializeField] private float flickerAmount = 0.15f;

    private Light barLight;
    private float baseIntensity;
    private float flickerOffset;

    void Start()
    {
        CreateBarLight();
        baseIntensity = intensity;
    }

    void CreateBarLight()
    {
        // Create a light GameObject
        GameObject lightObject = new GameObject("FluorescentBar");
        lightObject.transform.parent = transform;
        lightObject.transform.localPosition = Vector3.zero;

        // Add Light component
        barLight = lightObject.AddComponent<Light>();
        barLight.type = LightType.Directional;
        barLight.color = lightColor;
        barLight.intensity = intensity;
        barLight.range = 20f;

        // Create visual representation (cylinder mesh)
        GameObject visualObject = new GameObject("BarMesh");
        visualObject.transform.parent = lightObject.transform;
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localScale = new Vector3(barWidth, barWidth, barLength);

        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        meshFilter.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        MeshRenderer meshRenderer = visualObject.AddComponent<MeshRenderer>();

        // Create emissive material
        Material emissiveMaterial = new Material(Shader.Find("Standard"));
        emissiveMaterial.SetColor("_EmissionColor", lightColor * 2f);
        emissiveMaterial.EnableKeyword("_EMISSION");
        meshRenderer.material = emissiveMaterial;
    }

    void Update()
    {
        if (enableFlicker)
        {
            ApplyFlicker();
        }
    }

    void ApplyFlicker()
    {
        flickerOffset += Time.deltaTime * flickerSpeed;
        float flicker = Mathf.Sin(flickerOffset * 10f) * flickerAmount;
        barLight.intensity = baseIntensity + flicker;
    }

    public void SetIntensity(float newIntensity)
    {
        baseIntensity = newIntensity;
        intensity = newIntensity;
    }

    public void SetColor(Color newColor)
    {
        lightColor = newColor;
        barLight.color = newColor;
    }
}