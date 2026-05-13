Shader "Hecton8/Environment/MarauderOutpostIndirect"
{
    Properties
    {
        _BaseColor("Base Hull", Color) = (0.28, 0.31, 0.30, 1)
        _PanelColor("Panel Variation", Color) = (0.15, 0.18, 0.18, 1)
        _RustColor("Rust", Color) = (0.78, 0.24, 0.06, 1)
        _SiltColor("Silt", Color) = (0.18, 0.23, 0.21, 1)
        _Metallic("Metallic", Range(0, 1)) = 0.12
        _Smoothness("Smoothness", Range(0, 1)) = 0.34
        _RustStrength("Rust Strength", Range(0, 1)) = 0.86
        _SiltStrength("Silt Strength", Range(0, 1)) = 0.48
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            StructuredBuffer<float4x4> _OutpostMatrices;
            StructuredBuffer<uint> _OutpostCellTypes;
            float _OutpostAge01;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _PanelColor;
                float4 _RustColor;
                float4 _SiltColor;
                float _Metallic;
                float _Smoothness;
                float _RustStrength;
                float _SiltStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                uint typePacked : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 ResolveTypeColor(uint kind, half3 baseColor, half3 panelColor)
            {
                half corridor = kind == 1 ? 1.0h : 0.0h;
                half door = kind == 5 ? 1.0h : 0.0h;
                half data = kind == 4 ? 1.0h : 0.0h;
                half pillar = kind == 7 ? 1.0h : 0.0h;
                half3 color = lerp(baseColor, panelColor, corridor * 0.65h + pillar * 0.45h);
                color = lerp(color, half3(0.36h, 0.38h, 0.34h), door);
                color = lerp(color, half3(0.12h, 0.23h, 0.22h), data * 0.5h);
                return color;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                float4x4 outpostMatrix = _OutpostMatrices[instanceID];
                float4 positionWS = mul(outpostMatrix, float4(HectonCoreLitSanitizePositionOS(input.positionOS.xyz), 1.0));
                output.positionWS = positionWS.xyz;
                output.normalWS = (half3)HectonCoreLitSafeNormalize(mul((float3x3)outpostMatrix, input.normalOS));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.typePacked = _OutpostCellTypes[instanceID];
                return output;
            }

            half3 EvaluateOutpostLighting(float3 positionWS, float4 positionCS, half3 normalWS, half3 albedo, half metallic, half smoothness)
            {
                half3 color = SampleSH(normalWS) * albedo;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half nDotL = saturate(dot(normalWS, (half3)mainLight.direction));
                half specularBase = 0.04h + metallic * 0.18h;
                half specular = pow(max(nDotL, 0.0h), lerp(8.0h, 42.0h, smoothness)) * specularBase * smoothness;
                color += (albedo * nDotL + specular) * mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                color += HectonCoreLitEvaluateProjectedCausticsScattering(positionWS, normalWS) * albedo;
                return color;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                uint kind = input.typePacked & 15u;
                half encodedAge = (half)(((input.typePacked >> 24) & 255u) / 255.0);
                half age01 = saturate(max((half)_OutpostAge01, encodedAge));
                half3 normalWS = (half3)HectonCoreLitSafeNormalize(input.normalWS);
                half3 albedo = ResolveTypeColor(kind, (half3)_BaseColor.rgb, (half3)_PanelColor.rgb);
                half metallic = (half)_Metallic;
                half smoothness = (half)_Smoothness;

                half sideWear = saturate(1.0h - abs(normalWS.y));
                half doorWear = kind == 5u ? 0.35h : 0.0h;
                half pillarWear = kind == 7u ? 0.45h : 0.0h;
                half edgeWear = saturate(sideWear * 0.55h + age01 * 0.35h + doorWear + pillarWear);
                HectonCoreLitApplyProceduralRustSilt(
                    input.positionWS,
                    normalWS,
                    normalWS,
                    edgeWear,
                    age01,
                    (half)_SiltStrength,
                    (half)_RustStrength,
                    (half3)_SiltColor.rgb,
                    (half3)_RustColor.rgb,
                    albedo,
                    metallic,
                    smoothness);

                half3 lit = EvaluateOutpostLighting(input.positionWS, input.positionCS, normalWS, albedo, metallic, smoothness);
                half3 finalColor = HectonCoreLitApplyNoirFog(lit, input.fogFactor, input.positionWS);
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
