Shader "Hidden/Hecton8/OceanDepthFoam"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(HectonOceanVisualOverrides)
            float4 _H8OceanFoamAndShadowParams;
            float4 _H8OceanShorelineDepthParams;
        CBUFFER_END

        TEXTURE2D_X_FLOAT(_H8OceanSourceDepth);

        struct ShorelineFoamParamsData
        {
            float4 FoamIntensityAndFalloff;
            float4 QualityAndLimits;
        };

        StructuredBuffer<ShorelineFoamParamsData> _GlobalShorelineFoam;
        int _GlobalShorelineFoamCount;
        float4 _GlobalShorelineFoamRuntime;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 screenUV : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
            output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
        #if UNITY_UV_STARTS_AT_TOP
            output.screenUV.y = 1.0 - output.screenUV.y;
        #endif
            return output;
        }

        float H8DepthValid(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return step(0.0001, rawDepth);
        #else
            return step(rawDepth, 0.9999);
        #endif
        }

        half4 Frag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 screenUV = UnityStereoTransformScreenSpaceTex(input.screenUV);
            float rawDepth = SAMPLE_TEXTURE2D_X(_H8OceanSourceDepth, sampler_PointClamp, screenUV).r;
            float valid = H8DepthValid(rawDepth);
            float3 worldPos = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            float fadeMeters = max(_H8OceanShorelineDepthParams.x, 0.1);
            float seaLevelMeters = _H8OceanShorelineDepthParams.y;
            float foamEnergy = 1.0;
            float quality = saturate(_H8OceanFoamAndShadowParams.w);

            int rowCount = min(_GlobalShorelineFoamCount, 16);
            if (rowCount > 0)
            {
                float weightedSeaLevel = 0.0;
                float weightedFalloff = 0.0;
                float weightedIntensity = 0.0;
                float weightSum = 0.0;
                [loop]
                for (int i = 0; i < rowCount; i++)
                {
                    ShorelineFoamParamsData row = _GlobalShorelineFoam[i];
                    float rowOpacity = saturate(row.FoamIntensityAndFalloff.w * row.QualityAndLimits.w);
                    float rowQuality = saturate(row.QualityAndLimits.x);
                    float rowWeight = rowOpacity * max(rowQuality, 0.001);
                    weightedSeaLevel += (_GlobalShorelineFoamRuntime.x + row.FoamIntensityAndFalloff.z) * rowWeight;
                    weightedFalloff += max(row.FoamIntensityAndFalloff.y, 0.1) * rowWeight;
                    weightedIntensity += max(row.FoamIntensityAndFalloff.x, 0.0) * rowWeight;
                    weightSum += rowWeight;
                }

                float blend = saturate(weightSum);
                float invWeight = rcp(max(weightSum, 0.0001));
                seaLevelMeters = lerp(seaLevelMeters, weightedSeaLevel * invWeight, blend);
                fadeMeters = lerp(fadeMeters, weightedFalloff * invWeight, blend);
                foamEnergy = lerp(foamEnergy, weightedIntensity * invWeight, blend);
                quality = max(quality, saturate(_GlobalShorelineFoamRuntime.z));
            }

            float depthBelowWater = max(0.0, seaLevelMeters - worldPos.y);
            float shoreline = saturate(1.0 - depthBelowWater / fadeMeters) * valid;
            shoreline = saturate(shoreline * foamEnergy);
            float absorption = saturate(depthBelowWater / max(fadeMeters * 4.0, 0.1)) * valid;
            return half4((half)shoreline, (half)absorption, (half)quality, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "OceanDepthFoam"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack Off
}
