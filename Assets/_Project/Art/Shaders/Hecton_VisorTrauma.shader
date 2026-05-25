Shader "Hidden/Hecton8/VisorTrauma"
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

            #define VISOR_TRAUMA_BLOOD 1u
            #define VISOR_TRAUMA_ACID 2u
            #define VISOR_TRAUMA_HULL_DENT 3u
            #define VISOR_TRAUMA_GLASS_CRACK 4u
            #define VISOR_TRAUMA_BURN 5u

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

            struct TraumaDecalData
            {
                float4 LocalToWorldC0;
                float4 LocalToWorldC1;
                float4 LocalToWorldC2;
                float4 LocalToWorldC3;
                uint DecalTypeHash;
                float Opacity01;
                float BirthTime;
                uint Flags;
            };

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D_ARRAY(_GlobalVisorTraumaAtlas);
            SAMPLER(sampler_GlobalVisorTraumaAtlas);

            StructuredBuffer<TraumaDecalData> _GlobalVisorTrauma;
            int _GlobalVisorTraumaCount;
            float4 _GlobalVisorTraumaParams; // x atlas slices, y quality, z intensity, w atlas enabled
            float4 _GlobalVisorTraumaRefractionParams; // x normal refraction intensity, y max active, z thermal, w spare
            float4 _GlobalVisorTraumaTint;
            float4 _GlobalVisorTraumaCameraWS;

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

            float3 ResolveTraumaLocalPosition(TraumaDecalData trauma, float3 cameraRelativePosition)
            {
                float3 origin = trauma.LocalToWorldC3.xyz;
                float3 relative = cameraRelativePosition - origin;
                float3 xAxis = trauma.LocalToWorldC0.xyz;
                float3 yAxis = trauma.LocalToWorldC1.xyz;
                float3 zAxis = trauma.LocalToWorldC2.xyz;
                float xLenSq = max(dot(xAxis, xAxis), 0.0001);
                float yLenSq = max(dot(yAxis, yAxis), 0.0001);
                float zLenSq = max(dot(zAxis, zAxis), 0.0001);
                return float3(
                    dot(relative, xAxis) / xLenSq,
                    dot(relative, yAxis) / yLenSq,
                    dot(relative, zAxis) / zLenSq);
            }

            half4 SampleProceduralVisorTrauma(TraumaDecalData trauma, float3 localPosition)
            {
                float2 projectorUv = localPosition.xy + 0.5;
                float2 centered = projectorUv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float quality = saturate(_GlobalVisorTraumaParams.y);
                uint packedPayload = trauma.DecalTypeHash & 255u;
                uint traumaType = packedPayload & 15u;
                float birthPhase = frac(trauma.BirthTime * 0.000244140625) * 4096.0;

                float branchNoise = sin(centered.x * 43.0 + centered.y * 17.0 + birthPhase * 0.13);
                float ringNoise = 0.72 + 0.28 * sin(centered.x * 37.0 + centered.y * 19.0 + packedPayload * 1.71);
                float bloodAlpha = radial * radial * lerp(1.0, ringNoise, quality);
                float mainCrack = saturate(1.0 - abs(centered.y + branchNoise * 0.045) * lerp(26.0, 44.0, quality));
                float crossCrack = saturate(1.0 - abs(centered.x - branchNoise * 0.035) * lerp(34.0, 64.0, quality));
                float crackAlpha = saturate((mainCrack + crossCrack * 0.45) * radial);
                float tornEdge = smoothstep(0.12, 0.62, radial) * (1.0 - smoothstep(0.62, 0.98, radial));

                half3 scorch = half3(0.08h, 0.055h, 0.035h);
                half3 blood = half3(0.24h, 0.010h, 0.006h);
                half3 acid = half3(0.13h, 0.30h, 0.055h);
                half3 dent = half3(0.16h, 0.15h, 0.13h);
                half3 glass = half3(0.72h, 0.86h, 0.92h);
                half3 burn = half3(0.13h, 0.045h, 0.018h);

                if (traumaType == VISOR_TRAUMA_GLASS_CRACK)
                    return half4(glass, half(saturate(crackAlpha + tornEdge * 0.16)));

                if (traumaType == VISOR_TRAUMA_BLOOD)
                    return half4(blood, half(bloodAlpha));

                if (traumaType == VISOR_TRAUMA_ACID)
                    return half4(acid, half(bloodAlpha * 0.92 + tornEdge * 0.2));

                if (traumaType == VISOR_TRAUMA_HULL_DENT)
                    return half4(dent, half(tornEdge * 0.7));

                if (traumaType == VISOR_TRAUMA_BURN)
                    return half4(burn, half(bloodAlpha * 0.8 + tornEdge * 0.35));

                return half4(scorch, half(bloodAlpha));
            }

            half4 SampleVisorTrauma(TraumaDecalData trauma, float3 localPosition)
            {
                float2 projectorUv = localPosition.xy + 0.5;
                if (_GlobalVisorTraumaParams.w > 0.5)
                {
                    uint sliceCount = (uint)max(_GlobalVisorTraumaParams.x, 1.0);
                    uint packedPayload = trauma.DecalTypeHash & 255u;
                    uint slice = ((packedPayload >> 4) & 15u) % sliceCount;
                    return SAMPLE_TEXTURE2D_ARRAY(_GlobalVisorTraumaAtlas, sampler_GlobalVisorTraumaAtlas, projectorUv, slice);
                }

                return SampleProceduralVisorTrauma(trauma, localPosition);
            }

            void ProjectVisorTrauma(float3 scenePositionWS, out half3 accumulated, out float2 refractOffset)
            {
                accumulated = half3(0.0h, 0.0h, 0.0h);
                refractOffset = float2(0.0, 0.0);
                float3 cameraRelativePosition = scenePositionWS - _GlobalVisorTraumaCameraWS.xyz;
                float quality = saturate(_GlobalVisorTraumaParams.y);

                [loop]
                for (int traumaIndex = 0; traumaIndex < 128; traumaIndex++)
                {
                    if (traumaIndex >= _GlobalVisorTraumaCount)
                        break;

                    TraumaDecalData trauma = _GlobalVisorTrauma[traumaIndex];
                    if ((trauma.Flags & 1u) == 0u || trauma.Opacity01 <= 0.0001)
                        continue;

                    float3 localPosition = ResolveTraumaLocalPosition(trauma, cameraRelativePosition);
                    if (any(abs(localPosition) > float3(0.5, 0.5, 0.5)))
                        continue;

                    half4 traumaSample = SampleVisorTrauma(trauma, localPosition);
                    half depthWeight = lerp(1.15h, 2.0h, half(quality));
                    half depthFade = saturate(1.0h - abs(localPosition.z) * depthWeight);
                    half weight = traumaSample.a * half(trauma.Opacity01) * depthFade * half(_GlobalVisorTraumaParams.z);
                    half4 traumaTint = half4(_GlobalVisorTraumaTint);
                    accumulated += traumaSample.rgb * traumaTint.rgb * (weight * traumaTint.a);

                    if ((trauma.DecalTypeHash & 15u) == VISOR_TRAUMA_GLASS_CRACK)
                    {
                        float2 fractureSeed = localPosition.xy + float2(0.0003, -0.0007);
                        float2 fractureNormal = fractureSeed * rsqrt(max(dot(fractureSeed, fractureSeed), 0.0001));
                        float refractionGain = max(0.0, _GlobalVisorTraumaRefractionParams.x);
                        refractOffset += fractureNormal * (float)weight * refractionGain * lerp(0.0015, 0.0065, quality);
                    }
                }
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 sourceColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.screenUV);
                if (_GlobalVisorTraumaCount <= 0)
                    return sourceColor;

                if (!TryResolveScenePosition(input.screenUV, out float3 scenePositionWS))
                    return sourceColor;

                ProjectVisorTrauma(scenePositionWS, out half3 traumaColor, out float2 refractOffset);
                float refractWeight = saturate(length(refractOffset) * 180.0);
                if (refractWeight > 0.001)
                {
                    float2 refractUv = clamp(input.screenUV + refractOffset, float2(0.001, 0.001), float2(0.999, 0.999));
                    half3 refractedColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refractUv).rgb;
                    sourceColor.rgb = lerp(sourceColor.rgb, refractedColor, half(refractWeight));
                }

                return half4(sourceColor.rgb + traumaColor, sourceColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
