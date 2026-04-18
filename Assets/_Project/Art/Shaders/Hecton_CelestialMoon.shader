Shader "HECTON/Celestial/Hecton_CelestialMoon"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Shadow Side)]
        [HDR] _ShadowTint ("Shadow Tint", Color) = (0.16, 0.2, 0.3, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 2)) = 0.8

        [Header(Terminator)]
        _TerminatorWidth ("Terminator Width", Range(0.01, 0.4)) = 0.14
        [HDR] _TerminatorTintColor ("Terminator Tint", Color) = (0.95, 0.6, 0.32, 1)
        _TerminatorTintStrength ("Terminator Tint Strength", Range(0, 2)) = 0.45

        [Header(Rim)]
        [HDR] _RimColor ("Rim Color", Color) = (0.55, 0.68, 0.95, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.4
        _RimPower ("Rim Power", Range(1, 12)) = 4

        [Header(Aegir Fill)]
        [HDR] _AegirFillColor ("Aegir Fill Color", Color) = (0.45, 0.5, 0.72, 1)
        _AegirFillStrength ("Aegir Fill Strength", Range(0, 2)) = 0.32
        _AegirFillWrap ("Aegir Fill Wrap", Range(0, 1)) = 0.25

        [Header(Daylight Presence)]
        _DayDiskLift ("Day Disk Lift", Range(0, 1)) = 0.18
        _DayShadowSkyLift ("Day Shadow Sky Lift", Range(0, 1)) = 0.32

        [Header(Atmosphere)]
        _AtmosphereTransmittanceWeight ("Atmosphere Transmittance Weight", Range(0, 1.5)) = 1.0
        _AtmosphereInscatterWeight ("Atmosphere Inscatter Weight", Range(0, 2.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
        }

        LOD 150

        Pass
        {
            Name "CelestialMoonForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hecton_CelestialAtmosphere.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half4 _TerminatorTintColor;
                half4 _RimColor;
                half4 _AegirFillColor;
                half _ShadowStrength;
                half _TerminatorWidth;
                half _TerminatorTintStrength;
                half _RimStrength;
                half _RimPower;
                half _AegirFillStrength;
                half _AegirFillWrap;
                half _DayDiskLift;
                half _DayShadowSkyLift;
                half _AtmosphereTransmittanceWeight;
                half _AtmosphereInscatterWeight;
            CBUFFER_END

            float4 _SunDirection;
            float4 _AegirDirection;
            float4 _SkyColorZenith;
            float4 _SkyColorHorizon;
            float4 _SkyColorNadir;
            float _NightBlend;
            float _EclipseOcclusion;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SafeNormalizeDir(float3 value, float3 fallback)
            {
                float lenSq = dot(value, value);
                if (lenSq <= 0.000001)
                    return normalize(fallback);

                return value * rsqrt(lenSq);
            }

            float3 GetSkyColor(float3 rayDir)
            {
                float upMask = saturate(rayDir.y);
                float downMask = saturate(-rayDir.y);
                float horizonMask = saturate(1.0 - upMask - downMask);

                return _SkyColorZenith.rgb * upMask +
                       _SkyColorHorizon.rgb * horizonMask +
                       _SkyColorNadir.rgb * downMask;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = positionInputs.positionWS - GetCameraPositionWS();
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;

                float3 N = SafeNormalizeDir(input.normalWS, float3(0.0, 1.0, 0.0));
                float3 viewRay = SafeNormalizeDir(input.positionWS - GetCameraPositionWS(), float3(0.0, 1.0, 0.0));
                float3 V = SafeNormalizeDir(GetCameraPositionWS() - input.positionWS, float3(0.0, 0.0, 1.0));
                float3 toSun = SafeNormalizeDir(-_SunDirection.xyz, float3(0.0, 1.0, 0.0));
                float3 toAegir = SafeNormalizeDir(_AegirDirection.xyz, float3(0.0, 1.0, 0.0));

                float rawSun = dot(N, toSun);
                float litMask = smoothstep(-_TerminatorWidth, _TerminatorWidth, rawSun);
                float shadowMask = 1.0 - litMask;

                float terminatorBand = 1.0 - saturate(abs(rawSun) / max(_TerminatorWidth, 0.001));
                terminatorBand *= terminatorBand;

                float3 viewSkyColor = GetSkyColor(viewRay);
                float3 skyAmbient = max(GetSkyColor(N), viewSkyColor * 0.8);
                float3 shadowColor = albedo * _ShadowTint.rgb * _ShadowStrength;
                float3 dayColor = albedo * litMask;
                float dayAmbientBlend = saturate(1.0 - _NightBlend);
                float3 daytimeDiskLift = albedo * viewSkyColor * _DayDiskLift * dayAmbientBlend * saturate(0.35 + shadowMask * 0.65);
                float3 daylightShadowFill = max(skyAmbient, viewSkyColor * 0.75) * lerp(0.18, 0.42, dayAmbientBlend);
                float3 darkColor = lerp(shadowColor, daylightShadowFill, lerp(0.35, 0.68, dayAmbientBlend)) * shadowMask;
                darkColor += skyAmbient * shadowMask * _DayShadowSkyLift * dayAmbientBlend * saturate(0.45 + 0.55 * (1.0 - litMask));
                float3 terminatorColor = _TerminatorTintColor.rgb * terminatorBand * _TerminatorTintStrength;

                float aegirWrap = saturate((dot(N, toAegir) + _AegirFillWrap) / (1.0 + _AegirFillWrap));
                float aegirNightBoost = saturate(_NightBlend + _EclipseOcclusion * 0.8 + 0.2);
                float3 aegirFill = _AegirFillColor.rgb * (aegirWrap * aegirWrap) * shadowMask * _AegirFillStrength * aegirNightBoost;

                float rim = pow(1.0 - saturate(dot(N, V)), _RimPower);
                float rimNightBoost = saturate(0.35 + _NightBlend + _EclipseOcclusion);
                float3 rimColor = _RimColor.rgb * rim * _RimStrength * rimNightBoost;

                float3 shaded = dayColor + darkColor + daytimeDiskLift + terminatorColor + aegirFill + rimColor;
                float4 atmosphereSample = SampleHectonCelestialAtmosphere(
                    viewRay,
                    _SkyColorHorizon.rgb,
                    _SkyColorZenith.rgb);
                float horizonDissolve = 1.0 - atmosphereSample.a;
                float nightAtmosphereFade = lerp(1.0, 0.12, _NightBlend);
                float transmittanceWeight = saturate(
                    (_AtmosphereTransmittanceWeight +
                    shadowMask * 0.28 +
                    terminatorBand * 0.12 +
                    horizonDissolve * 0.55) *
                    lerp(1.0, 0.42, _NightBlend));
                float inscatteringWeight = max(
                    _AtmosphereInscatterWeight * (0.65 + shadowMask * 0.75 + horizonDissolve * 0.7) * nightAtmosphereFade,
                    shadowMask * 0.18 * nightAtmosphereFade + horizonDissolve * lerp(1.35, 0.16, _NightBlend));
                shaded = ApplyHectonCelestialAtmosphere(
                    shaded,
                    atmosphereSample,
                    transmittanceWeight,
                    inscatteringWeight);

                return half4(shaded, 1.0);
            }
            ENDHLSL
        }
    }
}
