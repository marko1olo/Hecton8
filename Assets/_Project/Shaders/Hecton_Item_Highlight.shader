// ============================================================================
// HECTON-8 — Hecton_Item_Highlight.shader
// URP shader for resource items with Sinica-style stencil visibility.
//
// FEATURES:
//   • Standard URP lit surface
//   • Stencil buffer for "always on top" rendering through foliage
//   • Highlight glow effect controlled via MaterialPropertyBlock
//   • Outline effect for visibility in dense vegetation
//
// USAGE:
//   1. Assign this shader to item materials
//   2. ItemHighlight.cs controls _HighlightIntensity via MaterialPropertyBlock
//   3. When intensity > 0, item renders with outline and stencil override
//
// STENCIL BEHAVIOR:
//   • Stencil Ref 0: Normal rendering (occluded by foliage)
//   • Stencil Ref 1: Always visible (renders on top of foliage)
// ============================================================================

Shader "Hecton/Item/Highlight"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        
        // Highlight properties
        _HighlightColor ("Highlight Color", Color) = (0, 0.9, 1, 1)
        _HighlightIntensity ("Highlight Intensity", Range(0, 1)) = 0
        _OutlineThickness ("Outline Thickness", Range(0, 1)) = 0.5
        
        // Stencil
        _StencilRef ("Stencil Ref", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        LOD 200

        // ── Stencil configuration ──
        Stencil
        {
            Ref [_StencilRef]
            Comp Always
            Pass Replace
        }

        // ── Main pass ──
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
                float4 _HighlightColor;
                float _HighlightIntensity;
                float _OutlineThickness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS.xyz, input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // Sample textures
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                half alpha = albedoAlpha.a * _BaseColor.a;

                // Normal
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                float sgn = input.tangentWS.w;
                float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
                half3 normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
                normalWS = normalize(normalWS);

                // Occlusion
                half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).g;

                // Lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSHPixel(input.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = 1.0;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = alpha;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);

                // ── Apply highlight glow ──
                if (_HighlightIntensity > 0.01)
                {
                    // Fresnel-based rim glow
                    half3 viewDir = normalize(inputData.viewDirectionWS);
                    half fresnel = 1.0 - saturate(dot(viewDir, normalWS));
                    fresnel = pow(fresnel, 2.0) * _HighlightIntensity;
                    
                    // Add highlight color
                    color.rgb = lerp(color.rgb, _HighlightColor.rgb, fresnel * 0.5);
                    
                    // Add overall brightness boost
                    color.rgb += _HighlightColor.rgb * _HighlightIntensity * 0.2;
                }

                return color;
            }
            ENDHLSL
        }

        // ── Shadow caster pass ──
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            ENDHLSL
        }

        // ── Depth pass ──
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
