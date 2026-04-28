Shader "Hidden/Hecton8/VisorFluidDistortion"
{
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
            CBUFFER_END

            TEXTURE2D_X(_BlitTexture);
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
                float2 scaledUV = uv * float2(
                    max(2.0, _HectonVisorFluidDropletScale * (1.0 + wetness * 1.6 + forwardStretch * 0.45)),
                    max(4.0, _HectonVisorFluidDropletScale * (2.35 + wetness * 1.25 + hullStress * 0.9 + forwardStretch)));
                float2 cellId = floor(scaledUV);
                float2 cellUV = frac(scaledUV) - 0.5;
                float seed = Hash21(cellId + 0.13);
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
                float2 flowDirection = float2(
                    _HectonVisorFluidLocalVelocity.x * _HectonVisorFluidLateralStreakStrength,
                    -1.0 - abs(_HectonVisorFluidLocalVelocity.z) * _HectonVisorFluidForwardStretchStrength);
                float dropletMask = ComputeDropletMask(input.screenUV, flowDirection, wetness, hullStress);
                float edgeMask = ComputeVisorEdgeMask(input.screenUV);
                float combinedMask = saturate(dropletMask * edgeMask * intensity);

                float2 refractedUV = saturate(input.screenUV + ComputeRefractionOffset(input.screenUV, combinedMask, wetness, hullStress));
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refractedUV);
                half sheen = (half)saturate(combinedMask * (0.08 + wetness * 0.06 + hullStress * 0.05));
                color.rgb = max(color.rgb, color.rgb + sheen * half3(0.018, 0.025, 0.03));
                return color;
            }
            ENDHLSL
        }
    }
}
