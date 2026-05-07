Shader "Hecton8/Fabrication/ProgressBeam"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.05, 0.95, 1.0, 0.66)
        _Progress("Progress", Range(0, 1)) = 0
        _CutoffSoftness("Cutoff Softness", Range(0.001, 0.25)) = 0.04
        _BandIntensity("Band Intensity", Range(0, 4)) = 1.7
        _LocalHeightMinMax("Local Height Min Max", Vector) = (0, 1, 0, 0)
        _FlickerSpeed("Flicker Speed", Float) = 31
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "FabricatorProgressBeam"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Progress;
                float _CutoffSoftness;
                float _BandIntensity;
                float4 _LocalHeightMinMax;
                float _FlickerSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float height01 : TEXCOORD2;
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
                HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
                HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                float minHeight = _LocalHeightMinMax.x;
                float maxHeight = max(minHeight + 0.001, _LocalHeightMinMax.y);
                output.height01 = saturate((input.positionOS.y - minHeight) / (maxHeight - minHeight));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float progress = saturate(_Progress);
                float softness = max(0.001, _CutoffSoftness);
                float builtMask = 1.0 - smoothstep(progress, progress + softness, input.height01);
                clip(builtMask - 0.01);

                float band = 1.0 - smoothstep(0.0, softness * 2.0, abs(input.height01 - progress));
                float flicker = frac(sin(_Time.y * _FlickerSpeed) * 43758.5453123);
                float3 normalWS = HectonCoreLitSafeNormalize(input.normalWS);
                float3 viewDirWS = HectonCoreLitSafeNormalize(GetCameraPositionWS() - input.positionWS);
                float rimBase = 1.0 - saturate(abs(dot(normalWS, viewDirWS)));
                float rim = rimBase * rimBase * lerp(1.0, rimBase, 0.4);
                half alpha = (half)saturate(_BaseColor.a * (0.22 + rim + band * _BandIntensity) * lerp(0.7, 1.15, flicker));
                half3 color = _BaseColor.rgb * (half)(1.0 + band * _BandIntensity + rim * 1.4);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
