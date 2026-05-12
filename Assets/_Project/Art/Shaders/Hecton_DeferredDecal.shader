Shader "Hidden/Hecton8/DeferredDecal"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Composite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            struct DecalData
            {
                float4 Row0;
                float4 Row1;
                float4 Row2;
                float4 Row3;
                float4 AtlasRect;
                float4 Tint;
            };

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D(_HectonDeferredDecalAtlas);
            SAMPLER(sampler_HectonDeferredDecalAtlas);

            StructuredBuffer<DecalData> _HectonDeferredDecals;
            int _HectonDeferredDecalCount;
            float4 _HectonDeferredDecalAtlasParams;
            float4 _HectonDeferredDecalTint;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.screenUV = float2((input.vertexID << 1) & 2, input.vertexID & 2);
                output.positionCS = float4(output.screenUV * 2.0 - 1.0, 0.0, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                output.screenUV.y = 1.0 - output.screenUV.y;
            #endif
                return output;
            }

            bool TryResolveScenePosition(float2 screenUV, out float3 positionWS)
            {
                float rawDepth = SampleSceneDepth(screenUV);
            #if UNITY_REVERSED_Z
                bool validDepth = rawDepth > 0.0001;
            #else
                bool validDepth = rawDepth < 0.9999;
            #endif
                if (!validDepth)
                {
                    positionWS = 0.0.xxx;
                    return false;
                }

                positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                return true;
            }

            half3 ProjectDeferredDecals(float3 scenePositionWS)
            {
                half3 accumulated = 0.0h.xxx;
                [loop]
                for (int decalIndex = 0; decalIndex < 256; decalIndex++)
                {
                    if (decalIndex >= _HectonDeferredDecalCount)
                        break;

                    DecalData decal = _HectonDeferredDecals[decalIndex];
                    float4x4 worldToDecal = float4x4(decal.Row0, decal.Row1, decal.Row2, decal.Row3);
                    float3 localPosition = mul(worldToDecal, float4(scenePositionWS, 1.0)).xyz;
                    if (any(abs(localPosition) > 0.5.xxx))
                        continue;

                    float2 projectorUv = localPosition.xy + 0.5;
                    float2 atlasUv = decal.AtlasRect.xy + projectorUv * decal.AtlasRect.zw;
                    half4 decalSample = SAMPLE_TEXTURE2D(_HectonDeferredDecalAtlas, sampler_HectonDeferredDecalAtlas, atlasUv);
                    half depthFade = saturate(1.0h - abs(localPosition.z) * 2.0h);
                    half4 decalTint = half4(decal.Tint);
                    accumulated += decalSample.rgb * decalTint.rgb * (decalSample.a * decalTint.a * depthFade * _HectonDeferredDecalAtlasParams.z);
                }

                return accumulated;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
                if (_HectonDeferredDecalCount <= 0)
                    return sourceColor;

                if (!TryResolveScenePosition(input.screenUV, out float3 scenePositionWS))
                    return sourceColor;

                half3 decalColor = ProjectDeferredDecals(scenePositionWS);
                return half4(sourceColor.rgb + decalColor, sourceColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
