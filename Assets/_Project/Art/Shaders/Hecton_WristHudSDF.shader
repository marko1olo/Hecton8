Shader "Hecton8/UI/WristHudSDF"
{
    Properties
    {
        _FontAtlas ("Font Atlas", 2D) = "white" {}
        _BaseIntensity ("Base Intensity", Float) = 1.65
        _GlitchMultiplier ("Glitch Multiplier", Float) = 1
        _HectonDiegeticGlitchQualityWeight ("Global Quality Weight", Range(0, 1)) = 1
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
            Name "WristHudSDF"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define SPECIAL_DEPTH_BAR 4294967041u
            #define SPECIAL_PDA_GRID 4294967042u
            #define SPECIAL_VIGNETTE 4294967043u
            #define SPECIAL_RADAR_BLIP 4294967044u
            #define SPECIAL_COMPASS 4294967045u

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localUv : TEXCOORD1;
                float4 color : COLOR0;
                nointerpolation uint code : TEXCOORD2;
                float glitch : TEXCOORD3;
                float seed : TEXCOORD4;
                float4 payload : TEXCOORD5;
            };

            struct WristHudQuadData
            {
                float4x4 matrix;
                float4 color;
                float4 uvRect;
                uint characterCode;
                float glitchIntensity;
                uint pad0;
                uint pad1;
            };

            TEXTURE2D(_FontAtlas);
            SAMPLER(sampler_FontAtlas);
            StructuredBuffer<WristHudQuadData> _WristHudQuads;
            float _BaseIntensity;
            float _GlitchMultiplier;
            float _HectonDiegeticGlitchIntensity;
            float _HectonDiegeticGlitchQualityWeight;

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ResolveLinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) / max(edge1 - edge0, 0.000001));
            }

            float ResolveLinearRampInv01(float edge0, float edge1, float value)
            {
                return 1.0 - ResolveLinearRamp01(edge0, edge1, value);
            }

            float DistanceSq2(float2 a, float2 b)
            {
                float2 delta = a - b;
                return dot(delta, delta);
            }

            Varyings Vert(Attributes input, uint instanceId : SV_InstanceID)
            {
                WristHudQuadData data = _WristHudQuads[instanceId];
                Varyings output;
                float qualityWeight = saturate(_HectonDiegeticGlitchQualityWeight);
                float qualityCurve = qualityWeight * qualityWeight * (3.0 - 2.0 * qualityWeight);
                float glitchScale = lerp(0.5, 1.15, qualityCurve);
                float glitch = saturate(data.glitchIntensity * _GlitchMultiplier + _HectonDiegeticGlitchIntensity * 0.35) * glitchScale;
                float jitter = (Hash12(float2(instanceId, _Time.y * 43.17)) - 0.5) * glitch * 0.006;
                float4 local = float4(input.positionOS.xy + jitter.xx, input.positionOS.z, 1.0);
                float3 world = mul(data.matrix, local).xyz;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = data.uvRect.xy + input.uv * data.uvRect.zw;
                output.localUv = input.uv;
                output.color = data.color;
                output.code = data.characterCode;
                output.glitch = glitch;
                output.seed = instanceId;
                output.payload = data.uvRect;
                return output;
            }

            float GlyphAlpha(float2 uv, float glitch, float seed)
            {
                float lineNoise = Hash12(float2(floor(uv.y * 64.0), seed + _Time.y * 19.0));
                float2 shiftedUv = uv;
                shiftedUv.x += (lineNoise - 0.5) * glitch * 0.018;
                float sdf = SAMPLE_TEXTURE2D(_FontAtlas, sampler_FontAtlas, shiftedUv).a;
                return ResolveLinearRamp01(0.42, 0.58, sdf);
            }

            float SpecialAlpha(uint code, float2 localUv, float4 uvRect)
            {
                if (code == SPECIAL_DEPTH_BAR)
                {
                    float edge = min(min(localUv.x, 1.0 - localUv.x), min(localUv.y, 1.0 - localUv.y));
                    return ResolveLinearRamp01(0.0, 0.12, edge) * saturate(uvRect.x);
                }

                if (code == SPECIAL_PDA_GRID)
                {
                    float2 edgeDistance = abs(localUv - 0.5);
                    float border = ResolveLinearRamp01(0.46, 0.49, max(edgeDistance.x, edgeDistance.y));
                    float scan = ResolveLinearRampInv01(0.0, 0.02, abs(frac(localUv.y * 5.0 + _Time.y * 0.9) - 0.5));
                    return saturate(border + scan * 0.12);
                }

                if (code == SPECIAL_VIGNETTE)
                {
                    float radialSq = DistanceSq2(localUv, 0.5);
                    return ResolveLinearRamp01(0.0484, 0.5476, radialSq) * saturate(uvRect.x);
                }

                if (code == SPECIAL_RADAR_BLIP)
                {
                    float radialSq = DistanceSq2(localUv, 0.5);
                    float core = ResolveLinearRampInv01(0.0324, 0.25, radialSq);
                    float ring = ResolveLinearRampInv01(0.1296, 0.2025, radialSq) * ResolveLinearRamp01(0.0484, 0.0961, radialSq);
                    return saturate(core + ring * 0.65);
                }

                if (code == SPECIAL_COMPASS)
                {
                    float ticks = ResolveLinearRampInv01(0.0, 0.08, abs(frac((localUv.x + uvRect.x) * 24.0) - 0.5));
                    float center = ResolveLinearRampInv01(0.0, 0.025, abs(localUv.x - 0.5));
                    float band = ResolveLinearRampInv01(0.34, 0.5, abs(localUv.y - 0.5));
                    return saturate((ticks * 0.7 + center) * band);
                }

                return 1.0;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float alpha;
                float3 color = input.color.rgb;

                if (input.code >= SPECIAL_DEPTH_BAR)
                {
                    alpha = SpecialAlpha(input.code, input.localUv, input.payload);
                }
                else
                {
                    alpha = GlyphAlpha(input.uv, input.glitch, input.seed);
                    float split = input.glitch * (Hash12(float2(input.seed, _Time.y * 71.0)) - 0.5);
                    color.r += split * 0.35;
                    color.b -= split * 0.25;
                }

                float flicker = lerp(1.0, Hash12(float2(input.seed * 3.1, _Time.y * 55.0)) * 0.65 + 0.35, saturate(input.glitch));
                alpha *= input.color.a * flicker;
                color *= _BaseIntensity;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
