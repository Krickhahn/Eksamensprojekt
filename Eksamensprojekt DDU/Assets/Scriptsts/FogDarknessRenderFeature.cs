using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class FogDarknessRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader fogDarknessShader;

        [Header("Fog")]
        public Color fogColor = new Color(0.05f, 0.05f, 0.1f, 1f);
        [Range(0f, 1f)] public float fogDensity = 0.5f;
        [Range(0f, 1f)] public float fogStart = 0.0f;
        [Range(0f, 1f)] public float fogEnd = 0.95f;

        [Header("Darkness")]
        [Range(0f, 1f)] public float darkness = 0.6f;

        [Header("Vignette")]
        [Range(0f, 2f)] public float vignetteStrength = 1.0f;
        [Range(0f, 1f)] public float vignetteRadius = 0.45f;

        [Header("Lommelygte")]
        public bool flashlightEnabled = true;
        [Range(0f, 1f)] public float flashlightRadius = 0.2f;
        [Range(0.01f, 0.5f)] public float flashlightSoftness = 0.12f;
        [Range(0f, 3f)] public float flashlightIntensity = 1.5f;
        public Color flashlightColor = new Color(1f, 0.95f, 0.8f, 1f);
        [Range(0f, 1f)] public float flickerAmount = 0.0f;

        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    // Statisk reference så andre scripts kan styre lommelygten
    public static FogDarknessRenderFeature Instance { get; private set; }

    private FogDarknessPass _pass;
    private Material _material;

    static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    static readonly int FogDensityId = Shader.PropertyToID("_FogDensity");
    static readonly int FogStartId = Shader.PropertyToID("_FogStart");
    static readonly int FogEndId = Shader.PropertyToID("_FogEnd");
    static readonly int DarknessId = Shader.PropertyToID("_Darkness");
    static readonly int VignetteStrId = Shader.PropertyToID("_VignetteStrength");
    static readonly int VignetteRadId = Shader.PropertyToID("_VignetteRadius");
    static readonly int FlashPosId = Shader.PropertyToID("_FlashlightPos");
    static readonly int FlashRadiusId = Shader.PropertyToID("_FlashlightRadius");
    static readonly int FlashSoftnessId = Shader.PropertyToID("_FlashlightSoftness");
    static readonly int FlashIntensityId = Shader.PropertyToID("_FlashlightIntensity");
    static readonly int FlashColorId = Shader.PropertyToID("_FlashlightColor");
    static readonly int FlashEnabledId = Shader.PropertyToID("_FlashlightEnabled");
    static readonly int FlashFlickerId = Shader.PropertyToID("_FlashlightFlicker");

    public override void Create()
    {
        Instance = this;
        if (settings.fogDarknessShader == null) return;
        _material = CoreUtils.CreateEngineMaterial(settings.fogDarknessShader);
        _pass = new FogDarknessPass(_material);
        _pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        _material.SetColor(FogColorId, settings.fogColor);
        _material.SetFloat(FogDensityId, settings.fogDensity);
        _material.SetFloat(FogStartId, settings.fogStart);
        _material.SetFloat(FogEndId, settings.fogEnd);
        _material.SetFloat(DarknessId, settings.darkness);
        _material.SetFloat(VignetteStrId, settings.vignetteStrength);
        _material.SetFloat(VignetteRadId, settings.vignetteRadius);
        _material.SetFloat(FlashEnabledId, settings.flashlightEnabled ? 1f : 0f);
        _material.SetFloat(FlashRadiusId, settings.flashlightRadius);
        _material.SetFloat(FlashSoftnessId, settings.flashlightSoftness);
        _material.SetFloat(FlashIntensityId, settings.flashlightIntensity);
        _material.SetColor(FlashColorId, settings.flashlightColor);
        _material.SetFloat(FlashFlickerId, settings.flickerAmount);

        renderer.EnqueuePass(_pass);
    }

    // Kaldes fra FlashlightController hvert frame med skærmposition
    public void SetFlashlightScreenPos(Vector2 screenPosNormalized)
    {
        _material?.SetVector(FlashPosId, new Vector4(screenPosNormalized.x, screenPosNormalized.y, 0, 0));
    }

    public void SetFlickerValue(float value)
    {
        if (settings != null) settings.flickerAmount = value;
    }

    protected override void Dispose(bool disposing)
    {
        Instance = null;
        CoreUtils.Destroy(_material);
    }

    class FogDarknessPass : ScriptableRenderPass
    {
        private readonly Material _mat;

        public FogDarknessPass(Material mat)
        {
            _mat = mat;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;
            var destDesc = renderGraph.GetTextureDesc(source);
            destDesc.name = "FogDarknessDest";
            destDesc.clearBuffer = false;

            TextureHandle dest = renderGraph.CreateTexture(destDesc);
            RenderGraphUtils.BlitMaterialParameters para = new(source, dest, _mat, 0);
            renderGraph.AddBlitPass(para, "FogDarknessBlit");

            resourceData.cameraColor = dest;
        }
    }
}