Shader "Hecton8/UI/Diegetic Panel Depth Fade"
{
    Properties
    {
        _BaseMap ("Panel Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _DepthFadeRange ("Depth Fade Range", Float) = 0.05
        _OcclusionActive ("Occlusion Active", Float) = 1
        _PanelPowerLevel ("Panel Power", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _DepthFadeRange;
                float _OcclusionActive;
                float _PanelPowerLevel;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionVS : TEXCOORD2;
            };

            float ResolveBayerThreshold(float2 screenUV)
            {
                uint2 screenPixel = (uint2)floor(screenUV * _ScreenParams.xy);
                uint column = screenPixel.x & 3u;
                uint row = screenPixel.y & 3u;
                uint index = column | (row << 2);

                const float4 row0 = float4(0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0);
                const float4 row1 = float4(12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0);
                const float4 row2 = float4(3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0);
                const float4 row3 = float4(15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0);

                if (index < 4u)
                    return row0[index];
                if (index < 8u)
                    return row1[index - 4u];
                if (index < 12u)
                    return row2[index - 8u];
                return row3[index - 12u];
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.positionVS = TransformWorldToView(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                baseColor.a *= saturate(_PanelPowerLevel);

                if (_OcclusionActive > 0.5)
                {
                    float sceneDepthRaw = SampleSceneDepth(screenUV);
                    float linearSceneDepth = LinearEyeDepth(sceneDepthRaw, _ZBufferParams);
                    float linearFragmentDepth = -input.positionVS.z;

                    if (isnan(linearSceneDepth) || isinf(linearSceneDepth))
                        linearSceneDepth = linearFragmentDepth;

                    float depthDelta = linearSceneDepth - linearFragmentDepth;
                    float fadeRange = max(_DepthFadeRange, 1e-4);
                    float occlusionFactor = saturate(depthDelta / fadeRange);
                    float ditherThreshold = ResolveBayerThreshold(screenUV);

                    if (occlusionFactor < ditherThreshold)
                        discard;

                    baseColor.a *= occlusionFactor;
                }

                return baseColor;
            }
            ENDHLSL
        }
    }
}
