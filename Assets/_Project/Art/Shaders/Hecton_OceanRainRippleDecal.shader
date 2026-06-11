Shader "Hecton/Weather/Ocean Rain Ripple Decal"
{
    Properties
    {
        _RippleTint ("Ripple Tint", Color) = (0.62, 0.78, 0.82, 0.34)
        _RippleStrength ("Ripple Strength", Range(0.0, 2.0)) = 0.72
        _RippleScale ("Ripple Scale", Range(0.5, 12.0)) = 4.2
        _RippleSpeed ("Ripple Speed", Range(0.0, 8.0)) = 2.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "OceanRainRippleDecal"
            Blend Off
            Cull Off
            ZWrite Off
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RippleTint;
                half _RippleStrength;
                half _RippleScale;
                half _RippleSpeed;
            CBUFFER_END

            float _RainIntensity;
            float _CurrentWaterLevelY;
            float4 _GlobalWind;
            float4 _HectonSurfaceSplashImpulse;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float CheapRainCellRipple01(float2 p, float timePhase)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float seed = Hash21(cell);
                float2 center = frac(float2(seed, Hash21(cell + seed + 19.19))) - 0.5;
                float2 delta = local - 0.5 - center * 0.34;
                float distSq = dot(delta, delta);
                float phase = frac(timePhase * 0.19 + seed);
                float radius = lerp(0.012, 0.21, phase);
                float ring = smoothstep(0.021, 0.0, abs(distSq - radius * radius));
                float core = smoothstep(0.014, 0.0, distSq) * (1.0 - phase);
                float dropGate = smoothstep(0.58, 0.96, seed);
                return saturate((ring * 0.78 + core * 0.35) * dropGate);
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float rain = saturate(_RainIntensity);
                float surfaceFade = saturate(1.0 - abs(input.positionWS.y - _CurrentWaterLevelY) * 4.0);
                float impulseAge = saturate(_Time.y - _HectonSurfaceSplashImpulse.z);
                float impulseLife = saturate(1.0 - impulseAge * 1.65) * saturate(_HectonSurfaceSplashImpulse.w);
                [branch]
                if ((rain <= 0.001 && impulseLife <= 0.001) || surfaceFade <= 0.001)
                {
                    clip(-1.0);
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                float2 wind = _GlobalWind.xz;
                float windLenSq = max(dot(wind, wind), 0.0001);
                float2 windDir = wind * rsqrt(windLenSq);
                float windSpeed = saturate(_GlobalWind.w * 0.08);
                float2 uv = input.positionWS.xz * (0.18 * max(_RippleScale, 0.001));
                uv += windDir * (_Time.y * _RippleSpeed * (0.18 + windSpeed * 0.42));
                uv.y += _Time.y * _RippleSpeed * 0.36;

                float cellRipple = CheapRainCellRipple01(uv, _Time.y * _RippleSpeed);
                float2 impulseDelta = input.positionWS.xz - _HectonSurfaceSplashImpulse.xy;
                float impulseDistSq = dot(impulseDelta, impulseDelta);
                float impulseRadius = lerp(0.45, 3.2, impulseAge);
                float impulseRadiusSq = impulseRadius * impulseRadius;
                float impulseBand = lerp(0.16, 0.85, impulseAge) * max(impulseRadius, 0.001);
                float impulseRing = smoothstep(impulseBand, 0.0, abs(impulseDistSq - impulseRadiusSq));
                float impulseCore = smoothstep(0.18, 0.0, impulseDistSq) * (1.0 - impulseAge);
                float impulseRipple = saturate(impulseRing * 0.82 + impulseCore * 0.35) * impulseLife * surfaceFade;
                float telemetryGlitch = step(0.992, frac(dot(input.positionWS.xz, float2(0.071, 0.113)) + _Time.y * 23.0)) * impulseLife;
                float ripple = saturate(cellRipple * rain * surfaceFade + impulseRipple + telemetryGlitch * 0.16 * surfaceFade);
                half alpha = (half)(ripple * _RippleTint.a * _RippleStrength);
                clip(alpha - max((half)HectonDitherCoverage(input.positionCS.xy), 0.0005h));
                return half4(_RippleTint.rgb * (half)(ripple * _RippleStrength), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
