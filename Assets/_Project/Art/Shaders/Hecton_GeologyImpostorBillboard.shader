Shader "Hecton8/Environment/Hecton_GeologyImpostorBillboard"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.45
        _AmbientFloor ("Ambient Floor", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "ForwardUnlitAtlas"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AlphaClipThreshold;
                half _AmbientFloor;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float HectonGeologyFiniteOr(float value, float fallbackValue)
            {
                return isfinite(value) ? value : fallbackValue;
            }

            float2 HectonGeologyFiniteUv(float2 value)
            {
                return all(isfinite(value)) ? saturate(value) : float2(0.5, 0.5);
            }

            half4 HectonGeologyFiniteColor(half4 value, half4 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(safePositionOS);
                output.positionCS = positionInputs.positionCS;
                output.uv = HectonGeologyFiniteUv(input.uv);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseColor = HectonGeologyFiniteColor(_BaseColor, half4(1.0h, 1.0h, 1.0h, 1.0h));
                half4 albedoSample = HectonGeologyFiniteColor(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, HectonGeologyFiniteUv(input.uv)), half4(0.0h, 0.0h, 0.0h, 0.0h)) * baseColor;
                half alphaClipThreshold = (half)saturate(HectonGeologyFiniteOr(_AlphaClipThreshold, 0.45));
                clip(albedoSample.a - alphaClipThreshold);

                half ambientFloor = (half)saturate(HectonGeologyFiniteOr(_AmbientFloor, 0.18));
                half3 ambient = half3(ambientFloor, ambientFloor, ambientFloor);
                half3 color = albedoSample.rgb * ambient;
                color = MixFog(color, input.fogFactor);
                return HectonGeologyFiniteColor(half4(color, albedoSample.a), half4(0.0h, 0.0h, 0.0h, 0.0h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
