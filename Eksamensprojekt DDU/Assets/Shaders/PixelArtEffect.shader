Shader "Custom/PixelArtEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Float) = 4.0
        _PaletteStrength ("Palette Strength", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "PixelArt"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;
            float _PaletteStrength;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // Texel splatting: snap UV to pixel grid
                float2 screenSize = _ScreenParams.xy;
                float2 pixelatedSize = floor(screenSize / _PixelSize);

                float2 snappedUV = floor(uv * pixelatedSize) / pixelatedSize;

                // Sample center of the snapped texel (texel splatting)
                float2 texelCenter = (floor(uv * pixelatedSize) + 0.5) / pixelatedSize;

                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, texelCenter);

                // Optional: reduce color palette (posterize)
                if (_PaletteStrength > 0.0)
                {
                    float levels = lerp(256.0, 8.0, _PaletteStrength);
                    col.rgb = floor(col.rgb * levels + 0.5) / levels;
                }

                return col;
            }
            ENDHLSL
        }
    }
}