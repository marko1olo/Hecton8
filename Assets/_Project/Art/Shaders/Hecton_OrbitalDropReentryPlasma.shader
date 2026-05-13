Shader "HECTON/Prologue/OrbitalDropReentryPlasma"
{
    Properties
    {
        _PlasmaHeat ("Plasma Heat", Range(0, 1)) = 0
        _PlasmaOpacity ("Plasma Opacity", Range(0, 1)) = 0
        _PlasmaVelocity ("Plasma Velocity", Range(0, 1)) = 0
        _PlasmaAltitude01 ("Altitude Scalar", Range(0, 1)) = 1
        _PlasmaLowTier ("Low Tier Solid Fade", Range(0, 1)) = 1
        _HectonReentryPhase ("Reentry Phase", Float) = 0
        [HDR]_PlasmaCoreColor ("Core Plasma", Color) = (5.2, 1.35, 0.22, 1)
        [HDR]_PlasmaEdgeColor ("Edge Plasma", Color) = (2.6, 0.12, 1.75, 1)
        [HDR]_RayleighColor ("Rayleigh Cloud", Color) = (0.18, 0.78, 1.45, 1)
        _VoronoiScale ("Voronoi Scale", Range(4, 80)) = 28
        _VoronoiSpeed ("Voronoi Speed", Range(0, 24)) = 13
        _CloudScatter ("Cloud Scatter", Range(0, 3)) = 1.15
        _SharedNoiseTex ("Shared Abyssal Noise", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "OrbitalDropReentryPlasma"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_SharedNoiseTex);
            SAMPLER(sampler_SharedNoiseTex);
            TEXTURE3D(_HectonPrebakedVectorNoise3D);
            SAMPLER(sampler_HectonPrebakedVectorNoise3D);

            CBUFFER_START(UnityPerMaterial)
                half _PlasmaHeat;
                half _PlasmaOpacity;
                half _PlasmaVelocity;
                half _PlasmaAltitude01;
                half _PlasmaLowTier;
                half _HectonReentryPhase;
                half4 _PlasmaCoreColor;
                half4 _PlasmaEdgeColor;
                half4 _RayleighColor;
                half _VoronoiScale;
                half _VoronoiSpeed;
                half _CloudScatter;
                float4 _SharedNoiseTex_ST;
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
            };

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return frac(float2(n, n * 34.37) + float2(0.13, 0.73));
            }

            half Voronoi(float2 uv)
            {
                float2 grid = floor(uv);
                float2 local = frac(uv);
                float best = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 cellPoint = Hash22(grid + offset);
                        cellPoint = 0.5 + 0.5 * sin((_Time.y * (0.35 + _PlasmaVelocity)) + 6.28318 * cellPoint);
                        float2 delta = offset + cellPoint - local;
                        best = min(best, dot(delta, delta));
                    }
                }

                return (half)saturate(1.0 - best);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half opacity = saturate(_PlasmaOpacity);
                half heat = saturate(_PlasmaHeat);
                half whiteout = smoothstep(0.82h, 1.0h, opacity);
                half3 whiteHot = half3(1.0h, 0.72h, 0.36h) * 4.5h;
                half3 lowTierColor = lerp(_PlasmaCoreColor.rgb * (0.35h + heat), whiteHot, whiteout);

                if (_PlasmaLowTier > 0.5h)
                    return half4(lowTierColor, opacity);

                float2 centered = input.uv * 2.0 - 1.0;
                half radial = (half)saturate(dot(centered, centered));
                float speed = _Time.y * (0.45 + _VoronoiSpeed * (0.12 + _PlasmaVelocity));
                float2 sharedUv = TRANSFORM_TEX(input.uv, _SharedNoiseTex);
                half sharedNoise = SAMPLE_TEXTURE2D(_SharedNoiseTex, sampler_SharedNoiseTex, sharedUv + speed * 0.015).r;
                half sharedVectorNoise = SAMPLE_TEXTURE3D(
                    _HectonPrebakedVectorNoise3D,
                    sampler_HectonPrebakedVectorNoise3D,
                    float3(frac(input.uv * 1.71 + speed * 0.017), frac(speed * 0.027))).r;

                float2 flowUv = input.uv * _VoronoiScale + float2(speed, -speed * 0.37);
                half cells0 = Voronoi(flowUv);
                half cells1 = Voronoi(flowUv * 1.73 + float2(19.1, -7.6) - speed * 0.21);
                half plasma = saturate(cells0 * 0.60h + cells1 * 0.30h + sharedNoise * 0.07h + sharedVectorNoise * 0.10h);
                half edge = smoothstep(0.18h, 0.96h, radial);
                half altitudeScatter = saturate(1.0h - _PlasmaAltitude01);
                half cloudBase = saturate(1.0h - radial * 0.72h);
                half cloud = cloudBase * cloudBase * _CloudScatter * (0.35h + altitudeScatter);
                half shock = smoothstep(0.42h, 0.98h, plasma + edge * 0.22h);

                half3 plasmaColor = lerp(_PlasmaEdgeColor.rgb, _PlasmaCoreColor.rgb, plasma);
                plasmaColor *= 0.55h + heat * 2.8h;
                plasmaColor += _RayleighColor.rgb * cloud * opacity;
                plasmaColor += _PlasmaEdgeColor.rgb * edge * shock * heat;
                plasmaColor = lerp(plasmaColor, whiteHot, whiteout);

                half alpha = saturate(opacity * (0.72h + plasma * 0.28h + edge * 0.18h));
                alpha = lerp(alpha, 1.0h, whiteout);
                return half4(plasmaColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
