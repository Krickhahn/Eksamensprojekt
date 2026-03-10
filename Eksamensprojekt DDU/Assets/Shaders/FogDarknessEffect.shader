Shader "Custom/FogDarknessEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.1, 0.1, 0.15, 1)
        _FogDensity ("Fog Density", Range(0, 1)) = 0.3
        _FogStart ("Fog Start (depth)", Range(0, 1)) = 0.0
        _FogEnd ("Fog End (depth)", Range(0, 1)) = 0.95
        _Darkness ("Darkness", Range(0, 1)) = 0.3
        _VignetteStrength ("Vignette Strength", Range(0, 2)) = 0.8
        _VignetteRadius ("Vignette Radius", Range(0, 1)) = 0.5

        // Lommelygte
        _FlashlightPos ("Flashlight Screen Pos", Vector) = (0.5, 0.5, 0, 0)
        _FlashlightRadius ("Flashlight Radius", Range(0, 1)) = 0.25
        _FlashlightSoftness ("Flashlight Softness", Range(0.01, 0.5)) = 0.15
        _FlashlightIntensity ("Flashlight Intensity", Range(0, 3)) = 1.2
        _FlashlightColor ("Flashlight Color", Color) = (1.0, 0.95, 0.8, 1)
        _FlashlightEnabled ("Flashlight Enabled", Float) = 1.0
        _FlashlightFlicker ("Flashlight Flicker", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "FogDarkness"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 _FogColor;
            float  _FogDensity;
            float  _FogStart;
            float  _FogEnd;
            float  _Darkness;
            float  _VignetteStrength;
            float  _VignetteRadius;

            float4 _FlashlightPos;
            float  _FlashlightRadius;
            float  _FlashlightSoftness;
            float  _FlashlightIntensity;
            float4 _FlashlightColor;
            float  _FlashlightEnabled;
            float  _FlashlightFlicker;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // ── Darkness ───────────────────────────────────────
                col.rgb *= (1.0 - _Darkness);

                // ── Vignette ───────────────────────────────────────
                float2 vigUV = uv * 2.0 - 1.0;
                float vignette = length(vigUV);
                vignette = smoothstep(_VignetteRadius, _VignetteRadius + 0.5, vignette);
                col.rgb = lerp(col.rgb, float3(0, 0, 0), vignette * _VignetteStrength);

                // ── Depth Fog ──────────────────────────────────────
                float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                float depth = Linear01Depth(rawDepth, _ZBufferParams);
                float fogFactor = smoothstep(_FogStart, _FogEnd, depth) * _FogDensity;
                col.rgb = lerp(col.rgb, _FogColor.rgb, saturate(fogFactor));

                // ── Lommelygte ─────────────────────────────────────
                if (_FlashlightEnabled > 0.5)
                {
                    // Kompenser for skærmens aspect ratio
                    float aspect = _ScreenParams.x / _ScreenParams.y;
                    float2 flashPos = _FlashlightPos.xy;
                    float2 diff = uv - flashPos;
                    diff.x *= aspect;

                    float dist = length(diff);

                    // Blød cirkel med falloff
                    float inner = _FlashlightRadius;
                    float outer = _FlashlightRadius + _FlashlightSoftness;
                    float spotlight = 1.0 - smoothstep(inner, outer, dist);

                    // Flicker
                    spotlight *= (1.0 - _FlashlightFlicker * 0.15);

                    // Lys op området under lygten
                    float3 lightColor = _FlashlightColor.rgb * _FlashlightIntensity;
                    col.rgb += col.rgb * spotlight * lightColor * (1.0 - _Darkness);
                    col.rgb = lerp(col.rgb, col.rgb, 1.0); // clamp handled below

                    // Reducer fog i lommelygteområdet
                    col.rgb = lerp(col.rgb, col.rgb + _FogColor.rgb * fogFactor * spotlight, -spotlight * 0.5);
                }

                col.rgb = saturate(col.rgb);
                return col;
            }
            ENDHLSL
        }
    }
}