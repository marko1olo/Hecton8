Shader "Hidden/Hecton8/VisorFluidDistortion"
{
    Properties
    {
        [NoScaleOffset] _HectonVisorFluidBlueNoiseTex ("Blue Noise", 2D) = "gray" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "VisorFluid"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _HectonVisorFluidIntensity;
                float _HectonVisorFluidWetness;
                float _HectonVisorFluidHullStress;
                float _HectonVisorFluidDistortionStrength;
                float _HectonVisorFluidRunoffSpeed;
                float _HectonVisorFluidDropletScale;
                float _HectonVisorFluidLateralStreakStrength;
                float _HectonVisorFluidForwardStretchStrength;
                float _HectonVisorFluidEdgeStreakStrength;
                float _HectonVisorFluidEdgeFadeExponent;
                float _HectonVisorFluidSpeed;
                float4 _HectonVisorFluidLocalVelocity;
                float _HectonVisorFluidAmbientLight;
                float _HectonVisorFluidDustStrength;
                float _HectonVisorFluidAmbientDustResponse;
                float _HectonVisorFluidBlueNoiseTilePixels;
                float _HectonVisorFluidHasBlueNoise;
            CBUFFER_END

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D(_HectonVisorFluidBlueNoiseTex);
            SAMPLER(sampler_HectonVisorFluidBlueNoiseTex);
            float4 _BlitTexture_TexelSize;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screenUV : TEXCOORD0;
            };

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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float ResolveBlueNoise(float2 uv, float2 offset)
            {
                float hashNoise = Hash21(floor(uv * _ScreenParams.xy) + offset);
                if (_HectonVisorFluidHasBlueNoise < 0.5)
                    return hashNoise;

                float2 blueNoiseUv = frac((uv * _ScreenParams.xy + offset) / max(_HectonVisorFluidBlueNoiseTilePixels, 16.0));
                float sampledNoise = SAMPLE_TEXTURE2D_LOD(_HectonVisorFluidBlueNoiseTex, sampler_HectonVisorFluidBlueNoiseTex, blueNoiseUv, 0).r;
                return sampledNoise;
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                float freq = 1.0;

                [unroll(3)]
                for (int octave = 0; octave < 3; octave++)
                {
                    sum += ValueNoise(p * freq) * amp;
                    freq *= 2.07;
                    amp *= 0.5;
                }

                return sum;
            }

            float ComputeVisorEdgeMask(float2 uv)
            {
                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(dot(centered, centered));
                float rim = pow(radial, max(0.1, _HectonVisorFluidEdgeFadeExponent));
                return saturate(0.28 + rim * 0.72);
            }

            float ComputeDropletMask(float2 uv, float2 flowDirection, float wetness, float hullStress)
            {
                float lateralStreak = _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength;
                float forwardStretch = abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength;
                float2 cellScale = float2(
                    max(2.0, _HectonVisorFluidDropletScale * (1.0 + wetness * 1.6 + forwardStretch * 0.45)),
                    max(4.0, _HectonVisorFluidDropletScale * (2.35 + wetness * 1.25 + hullStress * 0.9 + forwardStretch)));
                float2 scaledUV = uv * cellScale;
                float2 cellId = floor(scaledUV);
                float2 cellUV = frac(scaledUV) - 0.5;
                float seed = lerp(
                    Hash21(cellId + 0.13),
                    ResolveBlueNoise((cellId + 0.5) / max(cellScale, float2(1.0, 1.0)), float2(31.0, 17.0)),
                    saturate(_HectonVisorFluidHasBlueNoise));
                float activeCell = step(0.34 - wetness * 0.12 - hullStress * 0.08, seed);

                float travel = frac(_Time.y * _HectonVisorFluidRunoffSpeed * (0.22 + seed * 0.48) + seed + scaledUV.x * 0.015);
                cellUV.y += (travel - 0.5) * (1.15 + wetness * 0.32 + hullStress * 0.24);
                cellUV.x += lateralStreak * 0.22 + (seed - 0.5) * 0.25;

                float radius = lerp(0.10, 0.24, seed);
                float droplet = (1.0 - smoothstep(radius * 0.62, radius, length(cellUV * float2(1.0, 1.45)))) * activeCell;
                float streakWidth = lerp(0.016, 0.052, seed);
                float streak = (1.0 - smoothstep(streakWidth, streakWidth * 3.0, abs(cellUV.x)))
                    * smoothstep(0.48, -0.36, cellUV.y)
                    * activeCell;

                float filmNoise = saturate(Fbm(uv * float2(7.0, 13.0) + flowDirection * (_Time.y * 0.35)) - 0.52);
                float hullFilm = filmNoise * hullStress * (0.4 + abs(lateralStreak) * 0.4);
                float condensationMask = saturate(
                    Fbm(uv * float2(11.0, 19.0) - flowDirection * (_Time.y * 0.12) + hullStress * 2.0) -
                    (0.72 - hullStress * 0.18));
                float topBias = smoothstep(0.08, 1.0, uv.y);
                return saturate((droplet * 0.86 + streak * 0.74 + hullFilm + condensationMask * hullStress * 0.55) * topBias);
            }

            float ComputeDustMask(float2 uv, float edgeMask)
            {
                float ambientReveal = saturate(_HectonVisorFluidAmbientLight * _HectonVisorFluidDustStrength * _HectonVisorFluidAmbientDustResponse);
                if (ambientReveal <= 0.0001)
                    return 0.0;

                float blueNoise = ResolveBlueNoise(uv, float2(0.0, 0.0));
                float specks = smoothstep(1.0 - ambientReveal * 0.62, 1.0 - ambientReveal * 0.18, blueNoise);
                float scratchNoise = Fbm(uv * float2(47.0, 83.0) + float2(7.3, 19.1));
                float scratch = smoothstep(0.72, 0.97, scratchNoise) * ambientReveal;
                float centerProtection = smoothstep(0.0, 0.22, abs(uv.x - 0.5) + abs(uv.y - 0.5));
                return saturate((specks * (0.32 + edgeMask * 0.68) + scratch * 0.35) * centerProtection);
            }

            float2 ComputeRefractionOffset(float2 uv, float mask, float wetness, float hullStress)
            {
                float2 flowDirection = float2(
                    _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength * 0.6,
                    -1.0 - abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength * 0.4);
                float2 noiseUV = uv * float2(10.0, 16.0) + flowDirection * (_Time.y * _HectonVisorFluidRunoffSpeed * 0.5);
                float noiseX = Fbm(noiseUV + float2(0.0, 13.1)) - 0.5;
                float noiseY = Fbm(noiseUV + float2(17.3, 4.7)) - 0.5;
                float downwardPull = saturate(abs(_HectonVisorFluidLocalVelocity.y) * 0.15 + wetness * 0.35 + hullStress * 0.2);
                float2 centered = uv * 2.0 - 1.0;
                float2 edgeDirection = normalize(centered + float2(0.0001, 0.0001));
                float edgePush = _HectonVisorFluidSpeed * _HectonVisorFluidEdgeStreakStrength * (0.25 + hullStress * 0.75);
                float2 offset = float2(noiseX + flowDirection.x * 0.18, noiseY - downwardPull * 0.2);
                offset += edgeDirection * edgePush * (0.25 + saturate(length(centered)) * 0.75);
                return offset * (_HectonVisorFluidDistortionStrength * mask);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float wetness = saturate(_HectonVisorFluidWetness);
                float hullStress = saturate(_HectonVisorFluidHullStress);
                float intensity = saturate(_HectonVisorFluidIntensity);
                float glitchAmount = saturate((hullStress - 0.52) * 2.08);
                float2 flowDirection = float2(
                    _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength,
                    -1.0 - abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength);
                float dropletMask = ComputeDropletMask(input.screenUV, flowDirection, wetness, hullStress);
                float edgeMask = ComputeVisorEdgeMask(input.screenUV);
                float dustMask = ComputeDustMask(input.screenUV, edgeMask);
                float combinedMask = saturate(dropletMask * edgeMask * intensity);

                float2 refractedUV = saturate(input.screenUV + ComputeRefractionOffset(input.screenUV, combinedMask, wetness, hullStress));
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refractedUV);
                if (glitchAmount > 0.001)
                {
                    float2 chromaOffset = float2(
                        (Fbm(input.screenUV * float2(91.0, 47.0) + _Time.y * 3.2) - 0.5) * 0.0035 * glitchAmount,
                        (ValueNoise(input.screenUV * float2(53.0, 29.0) - _Time.y * 2.4) - 0.5) * 0.0018 * glitchAmount);
                    half red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refractedUV + chromaOffset)).r;
                    half blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(refractedUV - chromaOffset)).b;
                    color.r = red;
                    color.b = blue;

                    float staticNoise = saturate(ValueNoise(input.screenUV * _ScreenParams.xy * 0.08 + _Time.y * 18.0) - 0.68) * glitchAmount;
                    color.rgb += staticNoise * half3(0.055, 0.08, 0.1);
                }
                half sheen = (half)saturate(combinedMask * (0.08 + wetness * 0.06 + hullStress * 0.05));
                color.rgb = max(color.rgb, color.rgb + sheen * half3(0.018, 0.025, 0.03));
                half3 dustTint = lerp(half3(0.018, 0.022, 0.018), half3(0.11, 0.13, 0.10), saturate(_HectonVisorFluidAmbientLight));
                color.rgb = lerp(color.rgb, max(color.rgb - dustTint * 0.55h, half3(0.0h, 0.0h, 0.0h)), (half)(dustMask * 0.55));
                color.rgb += dustTint * (half)(dustMask * 0.18);
                return color;
            }
            ENDHLSL
        }
    }
}
