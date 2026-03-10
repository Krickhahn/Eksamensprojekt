using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class PixelArtRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Shader pixelArtShader;
        [Range(1f, 16f)] public float pixelSize = 4f;
        [Range(0f, 1f)] public float paletteStrength = 0f;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public Settings settings = new Settings();
    private PixelArtPass _pass;
    private Material _material;

    public override void Create()
    {
        if (settings.pixelArtShader == null) return;
        _material = CoreUtils.CreateEngineMaterial(settings.pixelArtShader);
        _pass = new PixelArtPass(_material, settings);
        _pass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    // ─── Inner Pass ────────────────────────────────────────────────
    class PixelArtPass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private readonly Settings _settings;
        private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
        private static readonly int PaletteStrengthId = Shader.PropertyToID("_PaletteStrength");

        public PixelArtPass(Material mat, Settings settings)
        {
            _mat = mat;
            _settings = settings;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            var source = resourceData.activeColorTexture;

            var destDescriptor = renderGraph.GetTextureDesc(source);
            destDescriptor.name = "PixelArtDest";
            destDescriptor.clearBuffer = false;

            TextureHandle dest = renderGraph.CreateTexture(destDescriptor);

            _mat.SetFloat(PixelSizeId, _settings.pixelSize);
            _mat.SetFloat(PaletteStrengthId, _settings.paletteStrength);

            RenderGraphUtils.BlitMaterialParameters para = new(source, dest, _mat, 0);
            renderGraph.AddBlitPass(para, "PixelArtBlit");

            resourceData.cameraColor = dest;
        }
    }
}