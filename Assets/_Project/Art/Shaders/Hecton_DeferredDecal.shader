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
                float4 LocalToWorldC0;
                float4 LocalToWorldC1;
                float4 LocalToWorldC2;
                float4 LocalToWorldC3;
                uint MaterialHash;
                float Opacity01;
                float LifetimeSeconds;
                uint Flags;
            };

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_ARRAY(_HectonDeferredDecalAtlas);
            SAMPLER(sampler_HectonDeferredDecalAtlas);

            StructuredBuffer<DecalData> _HectonDeferredDecals;
            int _HectonDeferredDecalCount;
            float4 _HectonDeferredDecalAtlasParams;
            float4 _HectonDeferredDecalTint;
            float4 _HectonDeferredDecalCameraWS;

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
                    positionWS = float3(0.0, 0.0, 0.0);
                    return false;
                }

                positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                return true;
            }

            float3 ResolveDecalLocalPosition(DecalData decal, float3 cameraRelativePosition)
            {
                float3 origin = decal.LocalToWorldC3.xyz;
                float3 relative = cameraRelativePosition - origin;
                float3 xAxis = decal.LocalToWorldC0.xyz;
                float3 yAxis = decal.LocalToWorldC1.xyz;
                float3 zAxis = decal.LocalToWorldC2.xyz;
                float xLenSq = max(dot(xAxis, xAxis), 0.0001);
                float yLenSq = max(dot(yAxis, yAxis), 0.0001);
                float zLenSq = max(dot(zAxis, zAxis), 0.0001);
                return float3(
                    dot(relative, xAxis) / xLenSq,
                    dot(relative, yAxis) / yLenSq,
                    dot(relative, zAxis) / zLenSq);
            }

            half4 SampleDeferredDecal(DecalData decal, float3 localPosition)
            {
                float2 projectorUv = localPosition.xy + 0.5;
                if (_HectonDeferredDecalAtlasParams.w > 0.5)
                {
                    uint sliceCount = (uint)max(_HectonDeferredDecalAtlasParams.x, 1.0);
                    uint slice = decal.MaterialHash % sliceCount;
                    return SAMPLE_TEXTURE2D_ARRAY(_HectonDeferredDecalAtlas, sampler_HectonDeferredDecalAtlas, projectorUv, slice);
                }

                float2 centered = projectorUv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float quality = saturate(_HectonDeferredDecalAtlasParams.y);
                float proceduralNoise = 0.72 + 0.28 * sin((centered.x * 37.0) + (centered.y * 19.0) + decal.MaterialHash * 1.71);
                float brokenRing = lerp(1.0, proceduralNoise, quality);
                float alpha = saturate(radial * radial * brokenRing);
                half3 scorch = half3(0.08h, 0.055h, 0.035h);
                half3 blood = half3(0.22h, 0.015h, 0.01h);
                half3 acid = half3(0.18h, 0.32h, 0.08h);
                half dentWeight = saturate(1.0h - abs(half((float)(decal.MaterialHash & 3u)) - 3.0h));
                half materialT = half((float)(decal.MaterialHash & 3u) * (1.0 / 2.0));
                half3 dent = half3(0.16h, 0.15h, 0.13h);
                half3 tint = lerp(scorch, blood, saturate(materialT));
                tint = lerp(tint, acid, saturate(materialT - 0.5h));
                tint = lerp(tint, dent, dentWeight);
                return half4(tint, alpha);
            }

            half3 ProjectDeferredDecals(float3 scenePositionWS)
            {
                half3 accumulated = half3(0.0h, 0.0h, 0.0h);
                float3 cameraRelativePosition = scenePositionWS - _HectonDeferredDecalCameraWS.xyz;
                [loop]
                for (int decalIndex = 0; decalIndex < 1024; decalIndex++)
                {
                    if (decalIndex >= _HectonDeferredDecalCount)
                        break;

                    DecalData decal = _HectonDeferredDecals[decalIndex];
                    if ((decal.Flags & 1u) == 0u || decal.Opacity01 <= 0.0001)
                        continue;

                    float3 localPosition = ResolveDecalLocalPosition(decal, cameraRelativePosition);
                    if (any(abs(localPosition) > float3(0.5, 0.5, 0.5)))
                        continue;

                    half4 decalSample = SampleDeferredDecal(decal, localPosition);
                    half depthWeight = lerp(1.15h, 2.0h, half(saturate(_HectonDeferredDecalAtlasParams.y)));
                    half depthFade = saturate(1.0h - abs(localPosition.z) * depthWeight);
                    half4 decalTint = half4(_HectonDeferredDecalTint);
                    accumulated += decalSample.rgb * decalTint.rgb * (decalSample.a * decalTint.a * half(decal.Opacity01) * depthFade * _HectonDeferredDecalAtlasParams.z);
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
