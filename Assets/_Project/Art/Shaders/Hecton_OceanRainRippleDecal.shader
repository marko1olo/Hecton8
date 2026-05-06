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
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "OceanRainRippleDecal"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

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

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return frac(float2(n, Hash21(p + n + 19.19)));
            }

            float VoronoiNearest(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float nearest = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 point = offset + Hash22(cell + offset);
                        nearest = min(nearest, dot(point - local, point - local));
                    }
                }

                return sqrt(nearest);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float rain = saturate(_RainIntensity);
                float surfaceFade = saturate(1.0 - abs(input.positionWS.y - _CurrentWaterLevelY) * 4.0);
                float2 wind = _GlobalWind.xz;
                float windLenSq = max(dot(wind, wind), 0.0001);
                float2 windDir = wind * rsqrt(windLenSq);
                float windSpeed = saturate(_GlobalWind.w * 0.08);
                float2 uv = input.positionWS.xz * (0.18 * max(_RippleScale, 0.001));
                uv += windDir * (_Time.y * _RippleSpeed * (0.18 + windSpeed * 0.42));
                uv.y += _Time.y * _RippleSpeed * 0.36;

                float nearest = VoronoiNearest(uv);
                float ring = smoothstep(0.112, 0.076, abs(nearest - 0.18));
                float impactCore = smoothstep(0.055, 0.0, nearest);
                float ripple = saturate(ring * 0.78 + impactCore * 0.42) * rain * surfaceFade;
                half alpha = (half)(ripple * _RippleTint.a * _RippleStrength);
                return half4(_RippleTint.rgb * (half)(ripple * _RippleStrength), alpha);
            }
            ENDHLSL
        }
    }
}
