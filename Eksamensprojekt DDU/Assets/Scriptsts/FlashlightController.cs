using UnityEngine;

// FJERNET [RequireComponent(typeof(Camera))] — konflikter med PlayerMovement
// Sæt dette script på dit KAMERA objekt
public class FlashlightController : MonoBehaviour
{
    [Header("Spotlight referencer (lades tomme = auto-opret)")]
    public Light mainSpotLight;
    public Light fillLight;

    [Header("Lommelygte indstillinger")]
    public float range = 18f;
    public float innerAngle = 18f;
    public float outerAngle = 45f;
    public float intensity = 50f;
    public Color lightColor = new Color(1f, 0.97f, 0.88f);

    [Header("Fill light")]
    public float fillIntensity = 4f;
    public float fillAngle = 80f;
    public Color fillColor = new Color(0.6f, 0.65f, 0.8f);

    [Header("Flicker")]
    public bool enableFlicker = true;
    [Range(0f, 1f)] public float flickerAmount = 0.08f;
    public float flickerSpeed = 6f;
    public float hardFlickerChance = 0.01f;

    [Header("Bobbing")]
    public bool enableBobbing = true;
    public float bobbingAmount = 0.8f;
    public float bobbingSpeed = 1.4f;

    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.F;

    private bool _isOn = true;
    private float _baseIntensity;
    private float _baseFillIntensity;
    private float _bobbingTimer;
    private float _hardFlickerTimer;

    void Start()
    {
        if (mainSpotLight == null)
            mainSpotLight = CreateMainSpotlight();

        if (fillLight == null)
            fillLight = CreateFillLight();

        _baseIntensity = intensity;
        _baseFillIntensity = fillIntensity;

        ApplySettings();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _isOn = !_isOn;
            mainSpotLight.enabled = _isOn;
            fillLight.enabled = _isOn;
        }

        if (!_isOn) return;

        HandleFlicker();
        HandleBobbing();
    }

    void HandleFlicker()
    {
        float noise1 = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
        float noise2 = Mathf.PerlinNoise(Time.time * flickerSpeed * 0.4f, 99f);
        float combined = noise1 * 0.7f + noise2 * 0.3f;

        float flicker = 1f - (1f - combined) * flickerAmount;

        _hardFlickerTimer -= Time.deltaTime;
        if (_hardFlickerTimer <= 0f)
        {
            if (Random.value < hardFlickerChance)
            {
                flicker *= Random.Range(0.05f, 0.3f);
                _hardFlickerTimer = Random.Range(0.05f, 0.15f);
            }
            else
            {
                _hardFlickerTimer = Random.Range(0.1f, 0.3f);
            }
        }

        mainSpotLight.intensity = _baseIntensity * flicker;
        fillLight.intensity = _baseFillIntensity * flicker;

        float colorShift = (combined - 0.5f) * 0.04f;
        mainSpotLight.color = new Color(
            Mathf.Clamp01(lightColor.r + colorShift),
            Mathf.Clamp01(lightColor.g),
            Mathf.Clamp01(lightColor.b - colorShift)
        );
    }

    void HandleBobbing()
    {
        if (!enableBobbing) return;

        bool isMoving = Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;
        if (isMoving)
            _bobbingTimer += Time.deltaTime * bobbingSpeed;

        float verticalBob = Mathf.Sin(_bobbingTimer * 2f) * bobbingAmount;
        float horizontalBob = Mathf.Sin(_bobbingTimer) * bobbingAmount * 0.5f;

        mainSpotLight.transform.localRotation = Quaternion.Euler(verticalBob, horizontalBob, 0f);
        fillLight.transform.localRotation = mainSpotLight.transform.localRotation;
    }

    void ApplySettings()
    {
        mainSpotLight.type = LightType.Spot;
        mainSpotLight.range = range;
        mainSpotLight.innerSpotAngle = innerAngle;
        mainSpotLight.spotAngle = outerAngle;
        mainSpotLight.intensity = intensity;
        mainSpotLight.color = lightColor;
        mainSpotLight.shadows = LightShadows.Soft;
        mainSpotLight.shadowStrength = 0.85f;
        mainSpotLight.renderMode = LightRenderMode.ForcePixel;

        fillLight.type = LightType.Spot;
        fillLight.range = range * 0.6f;
        fillLight.spotAngle = fillAngle;
        fillLight.innerSpotAngle = fillAngle * 0.5f;
        fillLight.intensity = fillIntensity;
        fillLight.color = fillColor;
        fillLight.shadows = LightShadows.None;
        fillLight.renderMode = LightRenderMode.ForcePixel;
    }

    Light CreateMainSpotlight()
    {
        GameObject obj = new GameObject("Flashlight_Main");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = new Vector3(0f, 0f, 0f);
        obj.transform.localRotation = Quaternion.identity;
        return obj.AddComponent<Light>();
    }

    Light CreateFillLight()
    {
        GameObject obj = new GameObject("Flashlight_Fill");
        obj.transform.SetParent(transform);
        obj.transform.localPosition = new Vector3(0f, 0f, 0.1f);
        obj.transform.localRotation = Quaternion.identity;
        return obj.AddComponent<Light>();
    }
}