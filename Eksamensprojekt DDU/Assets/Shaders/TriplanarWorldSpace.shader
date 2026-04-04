Shader "Custom/TriplanarWorldSpace"
{
    Properties
    {
        [Header(Textures)]
        _MainTex          ("Albedo",                      2D)          = "white" {}
        _BumpMap          ("Normal Map",                  2D)          = "bump"  {}
        _OcclusionMap     ("Occlusion (AO)",              2D)          = "white" {}
        _HeightMap        ("Height / Displacement",       2D)          = "gray"  {}
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D)          = "black" {}

        [Header(Surface)]
        _Color            ("Albedo Tint",                 Color)       = (1,1,1,1)
        _Brightness       ("Brightness",                  Range(0,4))  = 1.0
        _NormalStrength   ("Normal Strength",             Range(0,3))  = 1.0
        _Smoothness       ("Smoothness",                  Range(0,1))  = 0.5
        _SmoothnessScale  ("Smoothness Map Scale",        Range(0,1))  = 1.0
        _Metallic         ("Metallic",                    Range(0,1))  = 0.0
        _OcclusionStrength("AO Strength",                 Range(0,1))  = 1.0

        [Header(Parallax)]
        [Toggle(_USEPARALLAX_ON)] _UseParallax("Use Parallax",         Float)       = 0
        _ParallaxStrength ("Parallax Strength",           Range(0,0.1))= 0.02

        [Header(Triplanar)]
        _Tiling           ("World Tiling",                Float)       = 1.0
        _BlendSharpness   ("Blend Sharpness",             Range(1,16)) = 4.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP lighting variants — these are what enable spot/point lights
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma shader_feature_local _USEPARALLAX_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Textures ──────────────────────────────────────────────────────
            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_HeightMap);        SAMPLER(sampler_HeightMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Brightness;
                float  _Tiling;
                float  _BlendSharpness;
                float  _NormalStrength;
                float  _Smoothness;
                float  _SmoothnessScale;
                float  _Metallic;
                float  _OcclusionStrength;
                float  _ParallaxStrength;
            CBUFFER_END

            // ── Structs ───────────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float4 tangentOS   : TANGENT;
                float2 lightmapUV  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 tangentWS    : TEXCOORD2;
                float3 bitangentWS  : TEXCOORD3;
                float4 shadowCoord  : TEXCOORD4;
                float3 viewDirTS    : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
                half4  fogFactorAndVertexLight : TEXCOORD7;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Triplanar helpers ─────────────────────────────────────────────
            float3 BlendWeights(float3 nWS, float sharpness)
            {
                float3 w = pow(abs(nWS), sharpness);
                return w / (w.x + w.y + w.z + 1e-4);
            }

            float4 TriplanarSample(TEXTURE2D_PARAM(tex, samp), float3 posWS, float3 weights, float tiling)
            {
                float4 xS = SAMPLE_TEXTURE2D(tex, samp, posWS.zy * tiling);
                float4 yS = SAMPLE_TEXTURE2D(tex, samp, posWS.xz * tiling);
                float4 zS = SAMPLE_TEXTURE2D(tex, samp, posWS.xy * tiling);
                return xS * weights.x + yS * weights.y + zS * weights.z;
            }

            float3 TriplanarNormal(float3 posWS, float3 nWS, float3 weights, float tiling, float strength)
            {
                float3 tnX = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, posWS.zy * tiling));
                float3 tnY = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, posWS.xz * tiling));
                float3 tnZ = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, posWS.xy * tiling));

                tnX.xy *= strength;
                tnY.xy *= strength;
                tnZ.xy *= strength;

                tnX = float3(tnX.xy + nWS.zy, abs(tnX.z) * nWS.x);
                tnY = float3(tnY.xy + nWS.xz, abs(tnY.z) * nWS.y);
                tnZ = float3(tnZ.xy + nWS.xy, abs(tnZ.z) * nWS.z);

                return normalize(tnX.zyx * weights.x + tnY.xzy * weights.y + tnZ.xyz * weights.z);
            }

            // ── Vertex ────────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                // Fog + vertex lights baked into one interpolator
                half3 vertexLight = VertexLighting(vpi.positionWS, vni.normalWS);
                half  fogFactor   = ComputeFogFactor(vpi.positionCS.z);

                OUT.positionCS      = vpi.positionCS;
                OUT.positionWS      = vpi.positionWS;
                OUT.normalWS        = vni.normalWS;
                OUT.tangentWS       = vni.tangentWS;
                OUT.bitangentWS     = vni.bitangentWS;
                OUT.shadowCoord     = GetShadowCoord(vpi);
                OUT.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(vni.normalWS, OUT.vertexSH);

                float3 viewDirWS = GetWorldSpaceViewDir(vpi.positionWS);
                OUT.viewDirTS = float3(
                    dot(viewDirWS, vni.tangentWS),
                    dot(viewDirWS, vni.bitangentWS),
                    dot(viewDirWS, vni.normalWS)
                );

                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float3 nWS     = normalize(IN.normalWS);
                float3 weights = BlendWeights(nWS, _BlendSharpness);
                float3 posWS   = IN.positionWS;

                // Parallax
                #if _USEPARALLAX_ON
                {
                    float3 vTS = normalize(IN.viewDirTS);
                    float h;
                    if (weights.x > weights.y && weights.x > weights.z) {
                        h = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, posWS.zy * _Tiling).r;
                        posWS.zy += (h * _ParallaxStrength - _ParallaxStrength * 0.5) * vTS.xy;
                    } else if (weights.y >= weights.z) {
                        h = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, posWS.xz * _Tiling).r;
                        posWS.xz += (h * _ParallaxStrength - _ParallaxStrength * 0.5) * vTS.xy;
                    } else {
                        h = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, posWS.xy * _Tiling).r;
                        posWS.xy += (h * _ParallaxStrength - _ParallaxStrength * 0.5) * vTS.xy;
                    }
                }
                #endif

                // Albedo
                float4 albedo = TriplanarSample(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), posWS, weights, _Tiling);
                albedo.rgb *= _Color.rgb * _Brightness;

                // Metallic + Smoothness
                float4 mg         = TriplanarSample(TEXTURE2D_ARGS(_MetallicGlossMap, sampler_MetallicGlossMap), posWS, weights, _Tiling);
                float  metallic   = saturate(mg.r + _Metallic);
                float  smoothness = saturate(mg.a * _SmoothnessScale + _Smoothness);

                // AO
                float4 aoSample  = TriplanarSample(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), posWS, weights, _Tiling);
                float  occlusion = lerp(1.0, aoSample.r, _OcclusionStrength);

                // Normal (world space)
                float3 bumpedNormal = TriplanarNormal(posWS, nWS, weights, _Tiling, _NormalStrength);

                // ── Build InputData the correct URP way ───────────────────────
                InputData inputData = (InputData)0;
                inputData.positionWS        = IN.positionWS;
                inputData.positionCS        = IN.positionCS;
                inputData.normalWS          = bumpedNormal;
                inputData.viewDirectionWS   = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord       = IN.shadowCoord;
                inputData.fogCoord          = IN.fogFactorAndVertexLight.x;
                inputData.vertexLighting    = IN.fogFactorAndVertexLight.yzw;
                inputData.bakedGI           = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, bumpedNormal) * occlusion;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask        = SAMPLE_SHADOWMASK(IN.lightmapUV);

                // ── SurfaceData ───────────────────────────────────────────────
                SurfaceData surface         = (SurfaceData)0;
                surface.albedo              = albedo.rgb;
                surface.alpha               = 1.0;
                surface.metallic            = metallic;
                surface.smoothness          = smoothness;
                surface.occlusion           = occlusion;
                surface.normalTS            = float3(0, 0, 1);
                surface.emission            = 0;
                surface.specular            = 0;
                surface.clearCoatMask       = 0;
                surface.clearCoatSmoothness = 0;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ── Shadow Caster ─────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttr { float4 posOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVary { float4 posCS : SV_POSITION; };

            ShadowVary vertShadow(ShadowAttr IN)
            {
                ShadowVary OUT;
                float3 posWS  = TransformObjectToWorld(IN.posOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, ld));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.posCS = posCS;
                return OUT;
            }
            half4 fragShadow(ShadowVary IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ── Depth Only ────────────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex   vertDepth
            #pragma fragment fragDepth
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttr { float4 posOS : POSITION; };
            struct DepthVary { float4 posCS : SV_POSITION; };

            DepthVary vertDepth(DepthAttr IN)
            {
                DepthVary OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                return OUT;
            }
            half fragDepth(DepthVary IN) : SV_Target { return IN.posCS.z; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
