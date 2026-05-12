// File: Shaders/SuitVisor.shader
Shader "NASAPunk/SuitVisor"
{
    Properties
    {
        [Header(Glass)]
        _BaseColor ("Glass Tint", Color) = (0.05, 0.08, 0.06, 0.15)
        _GlassAlpha ("Glass Base Alpha", Range(0, 1)) = 0.12
        _IOR ("Index of Refraction", Range(1.0, 1.5)) = 1.05

        [Header(HUD)]
        _HUD_RenderTexture ("HUD Render Texture", 2D) = "black" {}
        _HUD_Intensity ("HUD Brightness", Range(0, 5)) = 3.0
        _HUD_Color ("HUD Tint", Color) = (0.82, 0.96, 1.0, 0.14)
        _HUD_ScratchBleed ("HUD Scratch Light Bleed", Range(0, 2)) = 0.8
        _HUD_CurveStrength ("HUD Curvature", Range(0, 1)) = 0.45
        _HUD_Scale ("HUD Scale", Range(0.4, 1.2)) = 0.68
        _HUD_EdgeFade ("HUD Edge Fade", Range(0.01, 0.5)) = 0.25
        _HUD_Offset ("HUD Offset", Vector) = (0, 0, 0, 0)
        _ToolBatteryNormalized ("Tool Battery Normalized", Range(0, 1)) = 0

        [Header(Imperfections)]
        _ScratchNormalMap ("Scratch Normal Map", 2D) = "bump" {}
        _ScratchNormalStrength ("Scratch Normal Strength", Range(0, 2)) = 0.6
        _FingerprintTex ("Fingerprint Smudge (R=mask)", 2D) = "black" {}
        _FingerprintStrength ("Fingerprint Strength", Range(0, 1)) = 0.3
        _LensGrimeIntensity ("Blue Noise Lens Grime", Range(0, 2)) = 1

        [Header(Water Runoff)]
        _WaterRunoffStrength ("Water Runoff Strength", Range(0, 1)) = 0
        _DropletAlpha ("Droplet Alpha", Range(0, 1)) = 0
        _WaterRunoffSpeed ("Water Runoff Speed", Range(0.5, 4)) = 1.35
        _WaterRunoffDistortion ("Water Runoff Distortion", Range(0, 0.05)) = 0.012
        _WaterDropletDensity ("Water Droplet Density", Range(0, 2)) = 1
        _WaterDropletScale ("Water Droplet Scale", Range(0.5, 12)) = 5
        _WaterRunoffNormalTex ("Water Runoff Normal", 2D) = "bump" {}
        _WaterRunoffNormalStrength ("Water Runoff Normal Strength", Range(0, 2)) = 0.85
        _WaterDropletMaskTex ("Water Droplet Mask", 2D) = "black" {}
        _WaterDropletMaskInfluence ("Water Droplet Mask Influence", Range(0, 1)) = 1

        [Header(Condensation)]
        _CondensationStrength ("Condensation Strength", Range(0, 1)) = 0
        _CondensationDistortion ("Condensation Distortion", Range(0, 0.05)) = 0.008
        _CondensationEdgeExponent ("Condensation Edge Exponent", Range(0.5, 6)) = 2.35
        _CondensationDriftSpeed ("Condensation Drift Speed", Range(0, 2)) = 0.18

        [Header(Frost)]
        _ScreenFrostStrength ("Screen Frost Strength", Range(0, 1)) = 0
        _FrostBlueNoiseDither ("Frost IGN Dither", Range(0, 1)) = 0.35

        [Header(Projection Failure)]
        _HudCloseOcclusionDistance ("HUD Close Occlusion Distance", Range(0.01, 0.5)) = 0.18

        [Header(Refraction Distortion)]
        _DistortionStrength ("Edge Distortion", Range(0, 0.1)) = 0.02
        _DistortionFalloff ("Distortion Edge Falloff", Range(0.5, 5)) = 2.0
        _LensEdgeRefraction ("Lens Edge Refraction", Range(0, 0.08)) = 0.028
        _ChromaticAberration ("Structural Chromatic Aberration", Range(0, 0.02)) = 0
        _StaticNoise ("Structural Static Noise", Range(0, 1)) = 0
        _HectonVisorRefractionScale ("Scalable Refraction Scale", Range(0, 1)) = 1
        _HectonVisorChromaticScale ("Scalable Chromatic Scale", Range(0, 1)) = 1
        _HectonVisorLowTierDither ("Low Tier Dither Vignette", Range(0, 1)) = 0
        _HypoxiaLevel ("HUD Hypoxia Failure", Range(0, 1)) = 0
        _HullStressFlicker ("Pressure Flicker", Range(0, 1)) = 0
        _PressureLensCrackIntensity ("Pressure Lens Crack Intensity", Range(0, 1)) = 0
        _PressureCrackParallaxDepth ("Pressure Crack Parallax Depth", Range(0, 0.08)) = 0.028
        _PressureCrackNormalStrength ("Pressure Crack Normal Strength", Range(0, 2)) = 0.75
        _HazardRadiationLevel ("Radiation Glitch Level", Range(0, 1)) = 0
        _HazardThermalLevel ("Thermal Glitch Level", Range(0, 1)) = 0
        _HazardToxicLevel ("Toxic Glitch Level", Range(0, 1)) = 0
        _HazardGlitchLevel ("Composite Hazard Glitch", Range(0, 1)) = 0
        _BiosRecoveryMode ("BIOS Recovery Mode", Range(0, 1)) = 0
        [HideInInspector] _HectonVisualStaticGlitch ("Visual Static Glitch", Range(0, 1)) = 0
        [HideInInspector] _HectonVisualStaticGlitchSeed ("Visual Static Glitch Seed", Float) = 0

        [Header(Fresnel)]
        _FresnelColor ("Fresnel Rim Color", Color) = (0.4, 0.6, 0.8, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.6

        [Header(Environment Reflection)]
        _EnvReflStrength ("Environment Reflection", Range(0, 1)) = 0.15
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0.0

        [Header(Exosuit Breathing)]
        _BreathingChestAmplitude ("Breathing Chest Amplitude", Range(0, 0.04)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        ZWrite On
        ZTest LEqual
        Blend Off
        AlphaToMask On
        Cull Front

        Pass
        {
            Name "VisorForward"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                WriteMask 255
            }

            HLSLPROGRAM
            #pragma vertex VisorVert
            #pragma fragment VisorFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #pragma multi_compile _ _HUD_PHOSPHOR_MODE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _GlassAlpha;
                float  _IOR;

                float4 _HUD_RenderTexture_ST;
                float  _HUD_Intensity;
                float4 _HUD_Color;
                float  _HUD_ScratchBleed;
                float  _HUD_CurveStrength;
                float  _HUD_Scale;
                float  _HUD_EdgeFade;
                float4 _HUD_Offset;
                float  _ToolBatteryNormalized;

                float4 _ScratchNormalMap_ST;
                float  _ScratchNormalStrength;
                float4 _FingerprintTex_ST;
                float  _FingerprintStrength;
                float  _LensGrimeIntensity;

                float  _WaterRunoffStrength;
                float  _DropletAlpha;
                float  _WaterRunoffSpeed;
                float  _WaterRunoffDistortion;
                float  _WaterDropletDensity;
                float  _WaterDropletScale;
                float4 _WaterRunoffNormalTex_ST;
                float  _WaterRunoffNormalStrength;
                float4 _WaterDropletMaskTex_ST;
                float  _WaterDropletMaskInfluence;

                float  _CondensationStrength;
                float  _CondensationDistortion;
                float  _CondensationEdgeExponent;
                float  _CondensationDriftSpeed;
                float  _ScreenFrostStrength;
                float  _FrostBlueNoiseDither;

                float  _DistortionStrength;
                float  _DistortionFalloff;
                float  _LensEdgeRefraction;
                float  _ChromaticAberration;
                float  _StaticNoise;
                float  _HectonVisorRefractionScale;
                float  _HectonVisorChromaticScale;
                float  _HectonVisorLowTierDither;
                float  _HypoxiaLevel;
                float  _HullStressFlicker;
                float  _PressureLensCrackIntensity;
                float  _PressureCrackParallaxDepth;
                float  _PressureCrackNormalStrength;
                float  _HazardRadiationLevel;
                float  _HazardThermalLevel;
                float  _HazardToxicLevel;
                float  _HazardGlitchLevel;
                float  _BiosRecoveryMode;
                float  _HectonVisualStaticGlitch;
                float  _HectonVisualStaticGlitchSeed;
                float  _HudCloseOcclusionDistance;
                float4 _VisorCameraForwardWS;
                float4 _VisorStrongestLightDirectionWS;

                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;

                float  _EnvReflStrength;
                float  _Smoothness;
                float  _Metallic;
                float  _BreathingChestAmplitude;
            CBUFFER_END

            TEXTURE2D(_HUD_RenderTexture); SAMPLER(sampler_HUD_RenderTexture);
            TEXTURE2D(_ScratchNormalMap); SAMPLER(sampler_ScratchNormalMap);
            TEXTURE2D(_FingerprintTex); SAMPLER(sampler_FingerprintTex);
            TEXTURE2D(_WaterRunoffNormalTex); SAMPLER(sampler_WaterRunoffNormalTex);
            TEXTURE2D(_WaterDropletMaskTex); SAMPLER(sampler_WaterDropletMaskTex);
            float4 _HectonHudFogPerturbation;
            float4 _HectonSuitHealthGlitch;
            float _PlayerStress01;
            float _HectonHudStressChromaticAberration;
            float _HectonHudStressVignette;
            float4 _HectonHudFogFrost;
            float _HectonVRSomaticCondensation;
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortSway;
            float4 _HectonVrComfortMotion;
            float4 _HectonXRFoveatedParams;
            float4 _HectonXRFoveatedCenterRadius;
            float _BreathingPhase;
            float4 _SonarRevealOriginWS;
            float4 _SonarRevealWaveParams;
            float _SonarWaveFront;
            float4 _SonarGridParams0;
            float4 _SonarGridHardColor;
            float4 _SonarGridOrganicColor;
            float4 _SonarGridAbyssalColor;
            float _SonarRevealExpireTime;
            float _HectonHudFocusBlur;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
                float  fogCoord    : TEXCOORD6;
                float3 viewDirWS   : TEXCOORD7;
                float3 positionOS  : TEXCOORD8;
                float2 glareData   : TEXCOORD9;
            };

            float ApproximateMagnitude2D(float2 value)
            {
                float2 delta = abs(value);
                return max(delta.x, delta.y) + min(delta.x, delta.y) * 0.375;
            }

            float ApproximateMagnitude3D(float3 value)
            {
                float3 delta = abs(value);
                float maxAxis = max(delta.x, max(delta.y, delta.z));
                float minAxis = min(delta.x, min(delta.y, delta.z));
                float midAxis = delta.x + delta.y + delta.z - maxAxis - minAxis;
                return maxAxis + midAxis * 0.375 + minAxis * 0.125;
            }

            float2 NormalizeApprox2D(float2 value)
            {
                return value * rcp(max(0.0001, ApproximateMagnitude2D(value)));
            }

            float3 NormalizeApprox3D(float3 value)
            {
                return value * rcp(max(0.0001, ApproximateMagnitude3D(value)));
            }

            float3 DominantAxisNormalOS(float3 normalOS)
            {
                float3 axisAbs = abs(normalOS);
                float useX = step(axisAbs.y, axisAbs.x) * step(axisAbs.z, axisAbs.x);
                float useY = (1.0 - useX) * step(axisAbs.z, axisAbs.y);
                float useZ = 1.0 - useX - useY;
                float3 axisSign = step(0.0, normalOS) * 2.0 - 1.0;
                return axisSign * float3(useX, useY, useZ);
            }

            float2 DominantAxis2D(float2 value)
            {
                float2 axisAbs = abs(value);
                float useX = step(axisAbs.y, axisAbs.x);
                float2 axisSign = step(0.0, value) * 2.0 - 1.0;
                return axisSign * float2(useX, 1.0 - useX);
            }

            float FastPowerCurve01(float value, float exponent)
            {
                float v = saturate(value);
                float v2 = v * v;
                float v4 = v2 * v2;
                float v8 = v4 * v4;
                float low = lerp(v, v2, saturate(exponent - 1.0));
                float high = lerp(v2, v8, saturate((exponent - 2.0) * 0.16666667));
                return lerp(low, high, step(2.0, exponent));
            }

            float FastRootCurve01(float value)
            {
                float v = saturate(value);
                return saturate(v * (1.85 - 0.85 * v));
            }

            float ApproximateNormalZ(float2 xy)
            {
                return saturate(1.0 - dot(xy, xy));
            }

            float3 UnpackScaledNormal(float4 packedNormal, float scale)
            {
                float3 n;
                n.xy = (packedNormal.rg * 2.0 - 1.0) * scale;
                n.z = ApproximateNormalZ(n.xy);
                return n;
            }

            float EdgeMask(float2 uv, float falloff)
            {
                float2 centered = uv * 2.0 - 1.0;
                return FastPowerCurve01(dot(centered, centered), falloff * 0.5);
            }

            float Bayer4x4(float2 pixelCoord)
            {
                float2 cell = floor(frac(pixelCoord * 0.25) * 4.0);

                if (cell.y < 0.5)
                {
                    if (cell.x < 0.5) return 0.0 / 16.0;
                    if (cell.x < 1.5) return 8.0 / 16.0;
                    if (cell.x < 2.5) return 2.0 / 16.0;
                    return 10.0 / 16.0;
                }

                if (cell.y < 1.5)
                {
                    if (cell.x < 0.5) return 12.0 / 16.0;
                    if (cell.x < 1.5) return 4.0 / 16.0;
                    if (cell.x < 2.5) return 14.0 / 16.0;
                    return 6.0 / 16.0;
                }

                if (cell.y < 2.5)
                {
                    if (cell.x < 0.5) return 3.0 / 16.0;
                    if (cell.x < 1.5) return 11.0 / 16.0;
                    if (cell.x < 2.5) return 1.0 / 16.0;
                    return 9.0 / 16.0;
                }

                if (cell.x < 0.5) return 15.0 / 16.0;
                if (cell.x < 1.5) return 7.0 / 16.0;
                if (cell.x < 2.5) return 13.0 / 16.0;
                return 5.0 / 16.0;
            }

            float ComputeToolBatteryLedMask(float2 hudUv, float battery01, out float activeMask)
            {
                float2 localUv = (hudUv - float2(0.765, 0.845)) / float2(0.17, 0.055);
                float activeSegmentCount = ceil(saturate(battery01) * 4.0 - 0.0001);
                float mask = 0.0;
                activeMask = 0.0;

                [unroll(4)]
                for (int segmentIndex = 0; segmentIndex < 4; segmentIndex++)
                {
                    float segmentMin = 0.08 + (segmentIndex * 0.22);
                    float segmentMax = segmentMin + 0.14;
                    float segmentMask =
                        step(segmentMin, localUv.x) *
                        step(localUv.x, segmentMax) *
                        step(0.18, localUv.y) *
                        step(localUv.y, 0.82);
                    float segmentActive = step((float)segmentIndex + 0.5, activeSegmentCount);
                    mask += segmentMask;
                    activeMask += segmentMask * segmentActive;
                }

                return mask;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float FastTrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float FastTriangleSigned(float phase)
            {
                return FastTrianglePulse01(phase) * 2.0 - 1.0;
            }

            float ResolveFrostBlueNoise(float2 uv, float timeValue)
            {
                float2 pixel = floor(uv * _ScreenParams.xy);
                return Hash21(pixel + floor(timeValue * 19.0));
            }

            float ResolveLensBlueNoise(float2 uv, float2 tileScale, float timeValue)
            {
                return Hash21(floor(uv * tileScale * 256.0) + floor(timeValue * 13.0));
            }

            void ComputeBlueNoiseLensGrime(float2 uv, float timeValue, out float dustMask, out float moistureMask)
            {
                float edgeBias = smoothstep(0.05, 0.92, EdgeMask(uv, 1.16));
                float topBias = smoothstep(0.08, 0.96, uv.y);

                float dustNoise = ResolveLensBlueNoise(uv + float2(0.17, 0.41), float2(47.0, 31.0), timeValue);
                float dustCluster = ResolveLensBlueNoise(uv + float2(0.63, 0.19), float2(14.0, 18.0), timeValue + 3.0);
                dustMask = step(0.965, dustNoise) * saturate(0.45 + dustCluster * 0.9) * edgeBias;

                float2 dropletSpace = uv * float2(12.0, 18.0) + float2(timeValue * 0.003, timeValue * -0.011);
                float2 dropletCell = frac(dropletSpace) - 0.5;
                float dropletSeed = ResolveLensBlueNoise(uv + float2(0.31, 0.73), float2(12.0, 18.0), timeValue + 7.0);
                float dropletGate = step(0.72, dropletSeed);
                float2 dropletCoreDelta = dropletCell * float2(1.0, 1.45);
                float dropletCoreRadiusSq = dot(dropletCoreDelta, dropletCoreDelta);
                float dropletCore = (1.0 - smoothstep(0.0100, 0.0625, dropletCoreRadiusSq)) * dropletGate;
                float dropletTrail = (1.0 - smoothstep(0.012, 0.045, abs(dropletCell.x)))
                    * (1.0 - smoothstep(-0.28, 0.45, dropletCell.y))
                    * dropletGate
                    * smoothstep(0.38, 0.92, dropletSeed);

                moistureMask = saturate((dropletCore * 0.72 + dropletTrail * 0.58) * topBias * edgeBias);
            }

            float SampleRgbMask(float3 rgb)
            {
                return saturate(dot(rgb, float3(0.2126, 0.7152, 0.0722)));
            }

            float ComputeProceduralScratchMask(float2 uv)
            {
                float2 scratchUV = uv * float2(96.0, 52.0);
                float2 coarseCell = floor(scratchUV * float2(0.22, 0.12));

                float gateA = step(0.72, Hash21(coarseCell + 0.17));
                float gateB = step(0.78, Hash21(coarseCell * 1.23 + 0.61));

                float lineA = abs(frac(scratchUV.x + scratchUV.y * 0.18) - 0.5);
                float lineB = abs(frac(scratchUV.x * 0.74 - scratchUV.y * 0.22 + 0.31) - 0.5);

                float scratchA = (1.0 - smoothstep(0.010, 0.032, lineA)) * gateA;
                float scratchB = (1.0 - smoothstep(0.012, 0.038, lineB)) * gateB;

                float edgeWear = smoothstep(0.10, 0.82, EdgeMask(uv, 1.4));
                float topBias = smoothstep(0.18, 0.96, uv.y);
                return saturate((scratchA * 0.72 + scratchB * 0.58) * edgeWear * topBias * 0.32);
            }

            float ComputePressureLensCrackMask(float2 uv, float intensity, float timeValue)
            {
                float active = saturate(intensity);
                float edgeBias = smoothstep(0.18, 0.96, EdgeMask(uv, 1.08));
                float2 centered = uv - 0.5;
                float pulse = 0.84 + FastTriangleSigned(timeValue * 1.7) * 0.08;

                float branchAPath = centered.y - centered.x * 0.24 - 0.08 * FastTriangleSigned(centered.x * 13.0 + 0.7);
                float branchA = (1.0 - smoothstep(0.004, 0.018, abs(branchAPath)))
                    * step(0.03, centered.x)
                    * step(-0.28, centered.y)
                    * step(centered.y, 0.34);

                float branchBPath = centered.y + centered.x * 0.62 + 0.035 * FastTriangleSigned(centered.y * 19.0 + 1.9);
                float branchB = (1.0 - smoothstep(0.003, 0.016, abs(branchBPath)))
                    * step(centered.x, -0.05)
                    * step(-0.36, centered.y)
                    * step(centered.y, 0.28);

                float branchCPath = centered.x - 0.18 * FastTriangleSigned(centered.y * 11.0 + 2.3);
                float branchC = (1.0 - smoothstep(0.003, 0.014, abs(branchCPath)))
                    * step(0.12, centered.y)
                    * step(centered.y, 0.46);

                float shardNoise = Hash21(floor(uv * float2(18.0, 14.0)) + floor(timeValue * 3.0));
                float2 shardDelta = frac(uv * float2(18.0, 14.0)) - 0.5;
                float shardRadiusSq = dot(shardDelta, shardDelta);
                float shard = step(0.88, shardNoise)
                    * (1.0 - smoothstep(0.000324, 0.0049, shardRadiusSq))
                    * edgeBias;

                return saturate((branchA * 0.9 + branchB * 0.72 + branchC * 0.62 + shard * 0.28) * edgeBias * active * pulse);
            }

            void ComputePressureCrackParallax(float2 uv, float intensity, float timeValue, out float crackMask, out float2 parallaxOffset)
            {
                float active = saturate(intensity);
                crackMask = ComputePressureLensCrackMask(uv, active, timeValue);

                float sampleStep = 0.0018;
                float crackDx =
                    ComputePressureLensCrackMask(uv + float2(sampleStep, 0.0), active, timeValue) -
                    ComputePressureLensCrackMask(uv - float2(sampleStep, 0.0), active, timeValue);
                float crackDy =
                    ComputePressureLensCrackMask(uv + float2(0.0, sampleStep), active, timeValue) -
                    ComputePressureLensCrackMask(uv - float2(0.0, sampleStep), active, timeValue);

                float2 crackGradient = float2(crackDx, crackDy);
                float gradientMagnitude = saturate(ApproximateMagnitude2D(crackGradient) * 12.0);
                float2 crackNormal = DominantAxis2D(crackGradient);
                float2 eyeParallax = (uv - 0.5) * (0.45 + gradientMagnitude);
                float shardDepth = crackMask * active * _PressureCrackParallaxDepth;
                parallaxOffset = (crackNormal * _PressureCrackNormalStrength * gradientMagnitude + eyeParallax) * shardDepth;
            }

            float ComputeProceduralSmudgeMask(float2 uv)
            {
                float2 smudgeUV = uv * float2(5.6, 7.4);
                float2 cellId = floor(smudgeUV);
                float2 cell = frac(smudgeUV) - 0.5;

                float seedA = Hash21(cellId + 0.37);
                float seedB = Hash21(cellId * 1.29 + 0.91);

                float2 smearADelta = cell * float2(1.7, 0.7);
                float smearARadiusSq = dot(smearADelta, smearADelta);
                float2 smearBDelta = (cell + float2(seedA - 0.5, seedB - 0.5) * 0.22) * float2(0.9, 1.5);
                float smearBRadiusSq = dot(smearBDelta, smearBDelta);
                float smearA = step(0.64, seedA)
                    * (1.0 - smoothstep(0.0100, 0.1444, smearARadiusSq));
                float smearB = step(0.76, seedB)
                    * (1.0 - smoothstep(0.0064, 0.1156, smearBRadiusSq));

                float edgeBias = smoothstep(0.08, 0.74, EdgeMask(uv, 1.1));
                float topBias = smoothstep(0.08, 0.86, uv.y);
                return saturate((smearA * 0.66 + smearB * 0.52) * edgeBias * topBias * 0.28);
            }

            float ComputeProceduralFrostMask(float2 uv, float edgeDist, float timeValue)
            {
                float edgeWarpNoise = ResolveFrostBlueNoise(uv + float2(timeValue * 0.003, timeValue * -0.005), timeValue + 5.0);
                float frostEdgeBase = saturate(smoothstep(0.05, 0.96, edgeDist + (edgeWarpNoise - 0.5) * 0.18));
                float frostEdge = frostEdgeBase * lerp(1.0, frostEdgeBase, 0.42);
                float2 baseUv = uv * float2(11.5, 17.0) + float2(timeValue * 0.004, timeValue * -0.006);
                float2 sampleUv = TRANSFORM_TEX(baseUv, _FingerprintTex);
                float4 packedNoise = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, sampleUv);
                float crystalSeed = packedNoise.r;
                float grainSeed = packedNoise.g;
                float shardBands = step(0.68, frac(baseUv.x * 6.4 + baseUv.y * 2.7 + grainSeed * 1.9));
                float shardRibs = step(0.74, frac(baseUv.x * 2.1 - baseUv.y * 5.3 + crystalSeed * 2.7));
                float2 lobeDelta = (frac(baseUv) - 0.5) * float2(1.0, 1.7);
                float lobeRadiusSq = dot(lobeDelta, lobeDelta);
                float lobe = 1.0 - smoothstep(0.0324, 0.2116, lobeRadiusSq);
                float crystalMask = saturate(shardBands * 0.55 + shardRibs * 0.45 + lobe * 0.3 + crystalSeed * 0.35 - 0.62);
                float blueNoise = ResolveFrostBlueNoise(uv, timeValue);
                crystalMask = saturate(crystalMask + (blueNoise - 0.5) * _FrostBlueNoiseDither);
                float topBias = smoothstep(0.06, 0.94, uv.y);
                return saturate(crystalMask * frostEdge * topBias);
            }

            float ComputeWaterRunoffMask(float2 uv, float time)
            {
                float2 scaledUV = uv * float2(
                    max(1.0, _WaterDropletScale),
                    max(1.0, _WaterDropletScale * 1.75));
                float2 cellId = floor(scaledUV);
                float2 cellUV = frac(scaledUV) - 0.5;
                float seed = Hash21(cellId);
                float activeCell = step(0.42, seed) * saturate(_WaterDropletDensity);

                float travel = frac(time * (0.18 + seed * 0.47) + seed);
                cellUV.y += (travel - 0.5) * 1.15;
                cellUV.x += (seed - 0.5) * 0.28;

                float radius = lerp(0.14, 0.28, seed);
                float dropletRadiusSq = dot(cellUV, cellUV);
                float radiusSq = radius * radius;
                float droplet = (1.0 - smoothstep(radiusSq * 0.4225, radiusSq, dropletRadiusSq)) * activeCell;
                float streakWidth = lerp(0.02, 0.05, seed);
                float streak = (1.0 - smoothstep(streakWidth, streakWidth * 3.0, abs(cellUV.x)))
                    * (1.0 - smoothstep(-0.35, 0.45, cellUV.y))
                    * activeCell;
                float topBias = smoothstep(0.15, 1.0, uv.y);
                return saturate((droplet * 0.85 + streak * 0.75) * topBias);
            }

            float2 ComputeCurvedHudUV(float2 meshUV, float3 positionOS, out float edgeFade)
            {
                float2 visorCoord = positionOS.xy * (2.0 * _HUD_Scale);
                float visorRadius = ApproximateMagnitude2D(visorCoord);
                float visorRadiusClamped = saturate(visorRadius);
                float r2 = visorRadiusClamped * visorRadiusClamped;
                float r4 = r2 * r2;
                float curveAmount = 1.0 + r2 * _HUD_CurveStrength + r4 * _HUD_CurveStrength * 0.5;
                float2 curvedCoord = visorCoord * curveAmount;
                float2 curvedUV = curvedCoord * 0.5 + 0.5 + _HUD_Offset.xy;

                float2 fromCenter = curvedUV - 0.5;
                float ellipseR = ApproximateMagnitude2D(fromCenter * float2(1.0, 0.85));
                float fadeStart = max(0.01, 1.0 - _HUD_EdgeFade);
                edgeFade = 1.0 - smoothstep(fadeStart * 0.7, fadeStart, ellipseR);
                return curvedUV;
            }

            float3 SampleSceneWorldPosition(float2 screenUV, out float rawDepth, out float validMask)
            {
                rawDepth = SampleSceneDepth(screenUV);
#if UNITY_REVERSED_Z
                validMask = step(0.0001, rawDepth);
#else
                validMask = step(rawDepth, 0.9999);
#endif
                return ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
            }

            float ComputeSonarContourMask(float2 screenUV, float rawDepth)
            {
                float2 texel = 1.0 / _ScaledScreenParams.xy;
                float depthDx = SampleSceneDepth(screenUV + float2(texel.x, 0.0));
                float depthDy = SampleSceneDepth(screenUV + float2(0.0, texel.y));
                float depthGradient = abs(depthDx - rawDepth) + abs(depthDy - rawDepth);
                return saturate(depthGradient * max(1.0, _SonarGridParams0.w) * 180.0);
            }

            float ComputeSonarGridMask(float3 sceneWorldPos)
            {
                float lineScale = max(0.1, _SonarGridParams0.y);
                float lineWidth = max(0.001, _SonarGridParams0.z);
                float2 cell = abs(frac(sceneWorldPos.xz * lineScale) - 0.5);
                return 1.0 - smoothstep(lineWidth, lineWidth * 2.5, min(cell.x, cell.y));
            }

            Varyings VisorVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 safePositionOS = all(isfinite(IN.positionOS.xyz)) ? IN.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float chestBreathMask = saturate(IN.color.r);
                safePositionOS += DominantAxisNormalOS(IN.normalOS) * (_BreathingPhase * _BreathingChestAmplitude * chestBreathMask);
                VertexPositionInputs posInputs = GetVertexPositionInputs(safePositionOS);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = nrmInputs.normalWS;
                OUT.tangentWS = nrmInputs.tangentWS;
                OUT.bitangentWS = nrmInputs.bitangentWS;
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(posInputs.positionCS);
                OUT.fogCoord = ComputeFogFactor(posInputs.positionCS.z);
                OUT.viewDirWS = NormalizeApprox3D(GetWorldSpaceViewDir(posInputs.positionWS));
                OUT.positionOS = safePositionOS;

                float3 cameraForwardWS = _VisorCameraForwardWS.xyz;
                float cameraForwardValid = step(0.0001, dot(cameraForwardWS, cameraForwardWS));
                cameraForwardWS = NormalizeApprox3D(lerp(-OUT.viewDirWS, cameraForwardWS, cameraForwardValid));
                float3 strongestLightDirectionWS = _VisorStrongestLightDirectionWS.xyz;
                float strongestLightValid = step(0.0001, dot(strongestLightDirectionWS, strongestLightDirectionWS)) * step(0.0001, _VisorStrongestLightDirectionWS.w);
                strongestLightDirectionWS = NormalizeApprox3D(lerp(cameraForwardWS, strongestLightDirectionWS, strongestLightValid));
                float cameraLightDot = saturate(dot(cameraForwardWS, strongestLightDirectionWS));
                float lightIntensity01 = saturate(_VisorStrongestLightDirectionWS.w);
                float cameraLightDotSq = cameraLightDot * cameraLightDot;
                OUT.glareData.x = 1.0 + smoothstep(0.9, 1.0, cameraLightDot) * lightIntensity01 * 1.35;
                OUT.glareData.y = cameraLightDotSq * cameraLightDot * lightIntensity01;
                return OUT;
            }

            float4 VisorFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                float2 scratchUV = TRANSFORM_TEX(IN.uv, _ScratchNormalMap);
                float4 scratchPacked = SAMPLE_TEXTURE2D(_ScratchNormalMap, sampler_ScratchNormalMap, scratchUV);
                float3 scratchNormalTS = UnpackScaledNormal(scratchPacked, _ScratchNormalStrength);
                float scratchTextureMask = ApproximateMagnitude2D(scratchNormalTS.xy);
                float proceduralScratchMask = ComputeProceduralScratchMask(IN.uv);
                float2 proceduralScratchXY = clamp(
                    float2(ddx(proceduralScratchMask), ddy(proceduralScratchMask)) * (_ScratchNormalStrength * 12.0),
                    -0.22,
                    0.22);
                float proceduralScratchBlend = saturate(1.0 - scratchTextureMask * 3.0);
                scratchNormalTS.xy = clamp(
                    scratchNormalTS.xy + proceduralScratchXY * proceduralScratchBlend,
                    -0.48,
                    0.48);
                scratchNormalTS.z = ApproximateNormalZ(scratchNormalTS.xy);
                float scratchMask = saturate(max(scratchTextureMask, proceduralScratchMask));

                float3x3 TBN = float3x3(
                    NormalizeApprox3D(IN.tangentWS),
                    NormalizeApprox3D(IN.bitangentWS),
                    NormalizeApprox3D(IN.normalWS)
                );
                float3 normalWS = NormalizeApprox3D(mul(scratchNormalTS, TBN));

                float runoffStrength = saturate(max(_WaterRunoffStrength, _DropletAlpha));
                float visorGlobalFog01 = saturate(_HectonHudFogFrost.x);
                float visorGlobalFrost01 = saturate(_HectonHudFogFrost.y);
                float condensationStrength = saturate(_CondensationStrength + visorGlobalFog01 + _HectonVRSomaticCondensation);
                float frostStrength = saturate(_ScreenFrostStrength + visorGlobalFrost01);

                float proceduralSmudgeMask = 0.0;
                float fingerprint = 0.0;
                float smudgeOpacity = 0.0;
                float2 fpUV = TRANSFORM_TEX(IN.uv, _FingerprintTex);
                float smudgeConsumer = max(max(saturate(_FingerprintStrength), runoffStrength), condensationStrength);
                [branch]
                if (smudgeConsumer > 0.001)
                {
                    float fingerprintSample = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, fpUV).r;
                    proceduralSmudgeMask = ComputeProceduralSmudgeMask(IN.uv);
                    fingerprint = max(fingerprintSample, proceduralSmudgeMask) * _FingerprintStrength;
                    smudgeOpacity = fingerprint * 0.4;
                }

                float3 viewDir = NormalizeApprox3D(IN.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDir));
                float fresnel = 0.0;
                float3 fresnelColor = 0.0;
                [branch]
                if (_FresnelIntensity > 0.0001)
                {
                    fresnel = FastPowerCurve01(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                    fresnelColor = _FresnelColor.rgb * fresnel;
                }

                float2 screenUV = IN.screenPos.xy * rcp(max(IN.screenPos.w, 0.0001));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
#endif
                float scalableRefractionScale = saturate(_HectonVisorRefractionScale);
                float scalableChromaticScale = saturate(_HectonVisorChromaticScale);
                float lowTierDitherScale = saturate(_HectonVisorLowTierDither);
                float edgeDist = EdgeMask(IN.uv, _DistortionFalloff);
                float blueNoiseDustMask = 0.0;
                float blueNoiseMoistureMask = 0.0;
                float blueNoiseGrimeMask = 0.0;
                float lensGrimeConsumer = max(saturate(_LensGrimeIntensity), runoffStrength);
                [branch]
                if (lensGrimeConsumer > 0.001)
                {
                    ComputeBlueNoiseLensGrime(IN.uv, _Time.y, blueNoiseDustMask, blueNoiseMoistureMask);
                    blueNoiseGrimeMask = saturate((blueNoiseDustMask * 0.55 + blueNoiseMoistureMask * 0.75) * _LensGrimeIntensity);
                    fingerprint = saturate(fingerprint + blueNoiseGrimeMask * 0.42);
                    smudgeOpacity = saturate(fingerprint * 0.4 + blueNoiseDustMask * 0.08);
                }
                float2 distortionOffset = scratchNormalTS.xy * _DistortionStrength;
                distortionOffset += edgeDist * normalWS.xy * _DistortionStrength * 0.5;
                float2 radialScreenOffset = screenUV - 0.5;
                float2 radialAbs = abs(radialScreenOffset);
                float radialApprox = max(radialAbs.x, radialAbs.y) + min(radialAbs.x, radialAbs.y) * 0.375;
                float radialMagnitude = saturate(radialApprox * 1.75);
                distortionOffset += radialScreenOffset * (radialMagnitude * radialMagnitude) * _LensEdgeRefraction;

                float runoffMask = 0.0;
                if (runoffStrength > 0.001)
                {
                    float runoffTime = _Time.y * _WaterRunoffSpeed;
                    float proceduralRunoffMask = ComputeWaterRunoffMask(IN.uv, runoffTime);
                    float2 dropletMaskUV = TRANSFORM_TEX(IN.uv + float2(0.0, runoffTime * -0.035), _WaterDropletMaskTex);
                    float authoredDropletMask = SampleRgbMask(
                        SAMPLE_TEXTURE2D(_WaterDropletMaskTex, sampler_WaterDropletMaskTex, dropletMaskUV).rgb);
                    runoffMask = lerp(
                        proceduralRunoffMask,
                        max(proceduralRunoffMask, authoredDropletMask),
                        saturate(_WaterDropletMaskInfluence));
                    runoffMask = saturate(max(
                        runoffMask * runoffStrength * (1.0 + fingerprint * 0.5),
                        blueNoiseMoistureMask * runoffStrength * 0.62));

                    float2 runoffNormalUV = TRANSFORM_TEX(IN.uv + float2(0.0, runoffTime * -0.08), _WaterRunoffNormalTex);
                    float4 runoffNormalPacked = SAMPLE_TEXTURE2D(_WaterRunoffNormalTex, sampler_WaterRunoffNormalTex, runoffNormalUV);
                    float3 runoffNormalTS = UnpackScaledNormal(runoffNormalPacked, _WaterRunoffNormalStrength);
                    float2 runoffDistortion = (scratchNormalTS.xy * 0.35 + runoffNormalTS.xy) * _WaterRunoffDistortion;
                    distortionOffset += runoffDistortion * runoffMask;
                    distortionOffset.y -= runoffMask * _WaterRunoffDistortion * (0.35 + abs(runoffNormalTS.y) * 0.25);
                }

                float condensationMask = 0.0;
                if (condensationStrength > 0.001)
                {
                    float condensationTime = _Time.y * _CondensationDriftSpeed;
                    float2 condensationUV = fpUV + float2(condensationTime * 0.021, condensationTime * -0.047);
                    float condensationTextureMask = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, condensationUV).r;
                    float condensationProceduralMask = ComputeProceduralSmudgeMask(
                        IN.uv + float2(condensationTime * 0.012, condensationTime * -0.018));
                    float condensationWarp = (ResolveFrostBlueNoise(IN.uv + float2(condensationTime * 0.014, condensationTime * -0.021), _Time.y + 9.0) - 0.5) * 0.16;
                    float condensationEdge = FastPowerCurve01(
                        saturate(smoothstep(0.04, 0.96, edgeDist + condensationWarp)),
                        max(0.5, _CondensationEdgeExponent));
                    condensationMask = saturate(
                        max(condensationTextureMask, condensationProceduralMask * 1.2)
                        * condensationEdge
                        * condensationStrength);

                    float2 condensationDistortion = (scratchNormalTS.xy * 0.4 + normalWS.xy * 0.25) * _CondensationDistortion;
                    distortionOffset += condensationDistortion * condensationMask;
                    distortionOffset.y -= condensationMask * _CondensationDistortion * 0.28;
                }

                float frostMask = 0.0;
                if (frostStrength > 0.001)
                {
                    float authoredFrost = ComputeProceduralFrostMask(IN.uv, edgeDist, _Time.y);
                    frostMask = saturate(authoredFrost * frostStrength);
                    distortionOffset += (scratchNormalTS.xy * 0.2 + radialScreenOffset * 0.06) * frostMask * 0.006;
                }

                float pressureCrackMask = 0.0;
                float2 pressureCrackParallaxOffset = 0.0;
                float pressureCrackIntensity = saturate(_PressureLensCrackIntensity);
                [branch]
                if (pressureCrackIntensity > 0.001)
                {
                    ComputePressureCrackParallax(IN.uv, pressureCrackIntensity, _Time.y, pressureCrackMask, pressureCrackParallaxOffset);
                    distortionOffset += (scratchNormalTS.xy * 0.16 + radialScreenOffset * 0.10) * pressureCrackMask * 0.004;
                    distortionOffset += pressureCrackParallaxOffset;
                }
                float vrComfortEnabled = saturate(_HectonVrComfortSignals.w);
                float vrComfortBounce = saturate(_HectonVrComfortSignals.y) * vrComfortEnabled;
                float vrComfortEdge = 0.0;
                [branch]
                if (vrComfortEnabled > 0.0001)
                {
                    vrComfortEdge = smoothstep(0.24, 1.0, radialMagnitude);
                    [branch]
                    if (vrComfortBounce > 0.0001)
                    {
                        float vrComfortBounceNoise = Hash21(floor(screenUV * _ScreenParams.xy * 0.125) + floor(_Time.y * 90.0));
                        float vrComfortBounceNoiseY = Hash21(floor(screenUV.yx * _ScreenParams.yx * 0.125) + floor(_Time.y * 91.0));
                        distortionOffset += float2(
                            (vrComfortBounceNoise - 0.5) * 0.0035,
                            (vrComfortBounceNoiseY - 0.5) * 0.0015) * vrComfortBounce * vrComfortEdge;
                    }
                }
                distortionOffset += _HectonVrComfortSway.xy * 0.010 * vrComfortEnabled * vrComfortEdge;

                float criticalHealthGlitch = saturate(_HectonSuitHealthGlitch.x);
                float criticalPayload = max(max(criticalHealthGlitch, abs(_HectonSuitHealthGlitch.z)), abs(_HectonSuitHealthGlitch.w));
                float criticalSpikeGate = 0.0;
                [branch]
                if (criticalPayload > 0.0001)
                {
                    float criticalBand = floor(screenUV.y * lerp(84.0, 260.0, criticalHealthGlitch) + floor(_Time.y * (17.0 + criticalHealthGlitch * 31.0)));
                    float criticalSpikeNoise = Hash21(float2(criticalBand, floor(_Time.y * (11.0 + criticalHealthGlitch * 43.0))));
                    criticalSpikeGate = step(0.74 - criticalHealthGlitch * 0.36, criticalSpikeNoise);
                    float criticalMicroGate = step(
                        0.56 - criticalHealthGlitch * 0.18,
                        Hash21(float2(criticalBand * 1.37, floor(_Time.y * 53.0))));
                    float criticalTear = (criticalSpikeNoise - 0.5) * _HectonSuitHealthGlitch.z * criticalSpikeGate;
                    distortionOffset.x += criticalTear;
                    distortionOffset.y += (criticalMicroGate - 0.5) * criticalHealthGlitch * 0.0035;
                }

                float staticNoise = 0.0;
                [branch]
                if (abs(_StaticNoise) > 0.0001)
                    staticNoise = (Hash21(floor(screenUV * _ScaledScreenParams.xy * 0.35 + _Time.y * 32.0)) - 0.5) * 2.0;
                float hazardRadiation = saturate(_HazardRadiationLevel);
                float hazardThermal = saturate(_HazardThermalLevel);
                float hazardToxic = saturate(_HazardToxicLevel);
                float hazardGlitch = saturate(_HazardGlitchLevel);
                float biosRecoveryMode = saturate(_BiosRecoveryMode);
                float biosRecoverySwitch = step(0.5, biosRecoveryMode);
                [branch]
                if (hazardRadiation > 0.0001)
                {
                    float radiationSceneBand = floor((screenUV.y + _Time.y * (8.0 + hazardRadiation * 17.0)) * lerp(96.0, 340.0, hazardRadiation));
                    float radiationSceneNoise = ResolveFrostBlueNoise(screenUV + float2(0.17, hazardRadiation * 0.13), _Time.y + 17.0);
                    float radiationSceneGate = step(0.62 - hazardRadiation * 0.28, radiationSceneNoise);
                    distortionOffset.x += (radiationSceneNoise - 0.5) * hazardRadiation * 0.018 * radiationSceneGate;
                    distortionOffset.y += (Hash21(float2(radiationSceneBand * 1.23, floor(_Time.y * 29.0))) - 0.5) * hazardRadiation * 0.004 * radiationSceneGate;
                }
                float2 refractedUV = screenUV;
                [branch]
                if (scalableRefractionScale > 0.001)
                {
                    distortionOffset *= scalableRefractionScale;
                    refractedUV = screenUV + distortionOffset;
                }
                else
                {
                    float2 staticCell = floor(screenUV * _ScaledScreenParams.xy * 0.0625);
                    float2 staticOffset = float2(Hash21(staticCell), Hash21(staticCell.yx + 17.0)) - 0.5;
                    refractedUV = screenUV + staticOffset * lerp(0.00035, 0.0011, lowTierDitherScale);
                }

                float2 hazardSceneSplit = float2(hazardRadiation * 0.006 + hazardGlitch * 0.003, 0.0) * scalableChromaticScale;
                float2 criticalSceneSplit = float2(_HectonSuitHealthGlitch.w * criticalSpikeGate * (0.5 + radialMagnitude), 0.0) * scalableChromaticScale;
                float stressHudChromaticRaw = saturate(max(_PlayerStress01, _HectonHudStressChromaticAberration));
                float stressHudPhase = frac(_Time.y * lerp(0.39788736, 1.75070437, stressHudChromaticRaw));
                float stressHudTriangle = 1.0 - abs(stressHudPhase * 2.0 - 1.0);
                float stressHudPulse = stressHudChromaticRaw * (0.74 + 0.26 * (stressHudTriangle * stressHudTriangle));
                float stressHudChromatic = saturate(stressHudPulse);
                float chromaStrength = max(_ChromaticAberration * scalableChromaticScale, stressHudChromatic * (0.004 + radialMagnitude * 0.018) * scalableChromaticScale);
                float2 chromaOffset = radialScreenOffset * chromaStrength + hazardSceneSplit + criticalSceneSplit;
                float sceneSurrogateNoise = Hash21(floor(refractedUV * _ScaledScreenParams.xy * 0.125) + floor(_Time.y * 9.0));
                float sceneSurrogateEdge = smoothstep(0.18, 1.0, radialMagnitude);
                float sceneSurrogateGlare = saturate(IN.glareData.x * 0.28 + IN.glareData.y * 0.22 + sceneSurrogateEdge * 0.18);
                float3 sceneColor =
                    _BaseColor.rgb * (0.38 + _BaseColor.a * 0.18) +
                    fresnelColor * (0.05 + sceneSurrogateEdge * 0.08 + runoffMask * 0.035) +
                    _HUD_Color.rgb * (0.012 + sceneSurrogateEdge * 0.018) +
                    float3(0.012, 0.020, 0.024);
                sceneColor += (sceneSurrogateNoise - 0.5) * (0.018 + scalableRefractionScale * 0.014);
                sceneColor += sceneSurrogateGlare * float3(0.026, 0.034, 0.036);
                sceneColor = max(sceneColor, float3(0.0015, 0.0022, 0.0030));

                float chromaticConsumer = max(chromaStrength, max(hazardGlitch, criticalHealthGlitch) * scalableChromaticScale);
                if (chromaticConsumer > 0.0001)
                {
                    float2 chromaAbs = abs(chromaOffset);
                    float chromaApprox = max(chromaAbs.x, chromaAbs.y) + min(chromaAbs.x, chromaAbs.y) * 0.375;
                    float chromaMask = saturate(chromaApprox * 24.0 + (hazardGlitch * 0.35 + criticalHealthGlitch * 0.28) * scalableChromaticScale);
                    float chromaSign = chromaOffset.x >= 0.0 ? 1.0 : -1.0;
                    float3 splitScene = lerp(sceneColor.bgr, sceneColor.gbr, step(0.0, chromaSign));
                    sceneColor = lerp(sceneColor, splitScene, chromaMask * 0.16);
                    sceneColor += float3(chromaSign, -0.25, -chromaSign) * (chromaMask * 0.015);
                }
                float vrComfortBlur = 0.0;
                float vrComfortBlurSignal = saturate(_HectonVrComfortSignals.z) * vrComfortEnabled;
                [branch]
                if (vrComfortBlurSignal > 0.0001)
                    vrComfortBlur = vrComfortBlurSignal * smoothstep(0.38, 1.0, radialMagnitude);
                if (vrComfortBlur > 0.0001)
                {
                    float motionEnergy = saturate(_HectonVrComfortMotion.z) * vrComfortEnabled;
                    float sceneLuma = dot(sceneColor, float3(0.2126, 0.7152, 0.0722));
                    float3 comfortScene = lerp(sceneColor, sceneLuma.xxx, 0.22 + motionEnergy * 0.18);
                    comfortScene = lerp(comfortScene, comfortScene.gbr, motionEnergy * 0.055);
                    sceneColor = lerp(sceneColor, comfortScene * 0.94 + 0.025, vrComfortBlur * 0.72);
                }

                float sonarOverlayMask = 0.0;
                float3 sonarOverlayColor = 0.0;

                float sonarGridIntensity = saturate(_SonarGridParams0.x);
                float sonarWaveSpeed = max(0.01, _SonarRevealWaveParams.y);
                float sonarFadeDuration = max(0.05, _SonarRevealWaveParams.z);
                float sonarContactLifetimeMask = step(
                    _Time.y,
                    _SonarRevealWaveParams.x + (_SonarRevealOriginWS.w / sonarWaveSpeed) + sonarFadeDuration);
                [branch]
                if (sonarGridIntensity > 0.0001 && sonarContactLifetimeMask > 0.5)
                {
                    float sonarSceneDepth;
                    float sonarDepthValid;
                    float3 sonarSceneWorldPos = SampleSceneWorldPosition(screenUV, sonarSceneDepth, sonarDepthValid);
                    if (sonarDepthValid > 0.5)
                    {
                        float3 sonarDelta = abs(sonarSceneWorldPos - _SonarRevealOriginWS.xyz);
                        float sonarMaxAxis = max(max(sonarDelta.x, sonarDelta.y), sonarDelta.z);
                        float sonarMinAxis = min(min(sonarDelta.x, sonarDelta.y), sonarDelta.z);
                        float sonarMidAxis = sonarDelta.x + sonarDelta.y + sonarDelta.z - sonarMaxAxis - sonarMinAxis;
                        float distanceToOrigin = sonarMaxAxis + sonarMidAxis * 0.375 + sonarMinAxis * 0.1875;
                        float timeSinceArrival = _Time.y - (_SonarRevealWaveParams.x + distanceToOrigin / sonarWaveSpeed);
                        float arrivalMask = step(0.0, timeSinceArrival);
                        float terrainFade = arrivalMask * saturate(1.0 - (timeSinceArrival / sonarFadeDuration));
                        float waveRadius = max(0.0, _SonarWaveFront);
                        float waveBandWidth = lerp(6.0, 2.0, saturate(_SonarRevealWaveParams.w));
                        float waveFront = 1.0 - smoothstep(waveBandWidth, waveBandWidth * 2.0, abs(distanceToOrigin - waveRadius));
                        float contourMask = ComputeSonarContourMask(screenUV, sonarSceneDepth);
                        float gridMask = ComputeSonarGridMask(sonarSceneWorldPos);
                        float activeTerrainMask = step(_Time.y, _SonarRevealExpireTime);
                        float terrainGrid = gridMask * max(contourMask, 0.14) * max(terrainFade, waveFront * 0.85) * activeTerrainMask;

                        float hardAccum = terrainGrid * 0.55;
                        float organicAccum = terrainGrid * 0.18;
                        float abyssalAccum = 0.0;

                        float hardStrength = saturate(hardAccum);
                        float organicStrength = saturate(organicAccum);
                        float abyssalStrength = saturate(abyssalAccum);
                        sonarOverlayColor =
                            (_SonarGridHardColor.rgb * hardStrength) +
                            (_SonarGridOrganicColor.rgb * organicStrength) +
                            (_SonarGridAbyssalColor.rgb * abyssalStrength);
                        sonarOverlayMask = sonarGridIntensity * saturate(max(max(hardStrength, organicStrength), abyssalStrength) + waveFront * contourMask * 0.4);
                    }
                }

                float hudEdgeFade;
                float2 hudUV = ComputeCurvedHudUV(IN.uv, IN.positionOS, hudEdgeFade);
                hudUV = TRANSFORM_TEX(hudUV, _HUD_RenderTexture);
                float2 hudDistortedUV = hudUV + distortionOffset * 0.3;
                hudDistortedUV -= _HectonVrComfortSway.xy * 0.018 * vrComfortEnabled;
                float hullStressFlicker = saturate(_HullStressFlicker);
                float pressureFlickerGate = 0.0;
                [branch]
                if (hullStressFlicker > 0.0001)
                {
                    float2 pressureNoiseSeed = floor(hudDistortedUV * _ScreenParams.xy * (0.6 + hullStressFlicker * 2.1));
                    float pressureNoiseA = Hash21(pressureNoiseSeed + float2(floor(_Time.y * 36.0), floor(_Time.y * 17.0)));
                    float pressureNoiseB = Hash21(pressureNoiseSeed.yx + float2(floor(_Time.y * -23.0), floor(_Time.y * 29.0)));
                    pressureFlickerGate = step(0.44 - hullStressFlicker * 0.18, frac(_Time.y * (18.0 + hullStressFlicker * 42.0) + hudDistortedUV.y * 46.0));
                    float2 pressureFlickerOffset = float2(
                        (pressureNoiseA - 0.5) * 0.0075,
                        (pressureNoiseB - 0.5) * 0.0025) * hullStressFlicker * pressureFlickerGate;
                    hudDistortedUV += pressureFlickerOffset * scalableChromaticScale;
                }
                float tearBands = 0.0;
                float tearBandConsumer = max(max(hazardGlitch, hazardThermal), max(hazardToxic, biosRecoverySwitch));
                [branch]
                if (tearBandConsumer > 0.0001)
                {
                    tearBands = floor(hudDistortedUV.y * 120.0);
                    [branch]
                    if (max(max(hazardGlitch, hazardThermal), hazardToxic) > 0.0001)
                    {
                        tearBands = floor((hudDistortedUV.y + _Time.y * (7.0 + hazardThermal * 9.0)) * lerp(120.0, 260.0, hazardGlitch));
                        float tearNoise = Hash21(float2(tearBands, floor(_Time.y * 18.0)));
                        float tearGate = step(0.58 - hazardGlitch * 0.26, tearNoise);
                        hudDistortedUV.x += (tearNoise - 0.5) * hazardGlitch * 0.048 * tearGate * scalableChromaticScale;
                        hudDistortedUV.y += (Hash21(float2(tearBands * 1.31, floor(_Time.y * 11.0))) - 0.5) * hazardToxic * 0.012 * scalableChromaticScale;
                    }
                }
                [branch]
                if (hazardRadiation > 0.0001)
                {
                    float radiationHudBands = floor((hudDistortedUV.y + _Time.y * (10.0 + hazardRadiation * 21.0)) * lerp(104.0, 380.0, hazardRadiation));
                    float radiationHudNoise = ResolveFrostBlueNoise(hudDistortedUV + float2(0.29, 0.0), _Time.y + 23.0);
                    float radiationHudGate = step(0.66 - hazardRadiation * 0.31, radiationHudNoise);
                    hudDistortedUV.x += (radiationHudNoise - 0.5) * hazardRadiation * 0.034 * radiationHudGate * scalableChromaticScale;
                    hudDistortedUV.y += (Hash21(float2(radiationHudBands * 1.37, floor(_Time.y * 37.0))) - 0.5) * hazardRadiation * 0.006 * radiationHudGate * scalableChromaticScale;
                }
                float criticalHudGate = 0.0;
                [branch]
                if (criticalPayload > 0.0001)
                {
                    float criticalHudBands = floor((hudDistortedUV.y + _Time.y * (18.0 + criticalHealthGlitch * 24.0)) * lerp(180.0, 420.0, criticalHealthGlitch));
                    float criticalHudNoise = Hash21(float2(criticalHudBands, floor(_Time.y * (31.0 + criticalHealthGlitch * 53.0))));
                    criticalHudGate = step(0.66 - criticalHealthGlitch * 0.33, criticalHudNoise);
                    hudDistortedUV.x += (criticalHudNoise - 0.5) * criticalHealthGlitch * 0.078 * criticalHudGate * scalableChromaticScale;
                    hudDistortedUV.y += (Hash21(float2(criticalHudBands * 1.19, floor(_Time.y * 71.0))) - 0.5) * criticalHealthGlitch * 0.009 * criticalHudGate * scalableChromaticScale;
                }

                float hypoxiaLevel = saturate(_HypoxiaLevel);
                float criticalHypoxia = smoothstep(0.0, 0.35, hypoxiaLevel);
                float criticalHypoxiaEdgeVignette = 0.0;
                if (criticalHypoxia > 0.0001)
                {
                    criticalHypoxiaEdgeVignette = smoothstep(0.22, 0.88, EdgeMask(IN.uv, 1.12));
                    float hypoxiaSceneLuma = dot(sceneColor, float3(0.2126, 0.7152, 0.0722));
                    float3 hypoxiaScene = lerp(sceneColor, hypoxiaSceneLuma.xxx, criticalHypoxia * 0.58);
                    hypoxiaScene *= 1.0 - criticalHypoxia * criticalHypoxiaEdgeVignette * 0.22;
                    sceneColor = lerp(sceneColor, hypoxiaScene, criticalHypoxiaEdgeVignette);
                }
                float2 hudHypoxiaOffset = criticalHypoxia > 0.0001 ? float2(hypoxiaLevel * 0.0045, 0.0) * scalableChromaticScale : float2(0.0, 0.0);
                float stressHudBandNoise = 0.5;
                [branch]
                if (stressHudChromatic > 0.0001)
                    stressHudBandNoise = Hash21(float2(floor(screenUV.y * 128.0), floor(_Time.y * 17.0)));
                float2 hudStressSplit = float2(
                    stressHudChromatic * (0.006 + radialMagnitude * 0.008),
                    stressHudChromatic * 0.0015 * ((stressHudBandNoise - 0.5) * 2.0)) * scalableChromaticScale;
                float2 hudDecaySplit = float2(
                    hazardRadiation * 0.015 + hazardGlitch * 0.008 + _HectonSuitHealthGlitch.w * criticalHudGate * 1.8,
                    hazardThermal * 0.0025) * scalableChromaticScale + hudStressSplit;
                float criticalHypoxiaAlphaDissolve = 0.0;
                float hudAlpha = 0.0;
                float3 hudColor = 0.0;
#if defined(_HUD_PHOSPHOR_MODE)
                float2 insideRT = step(0.0, hudDistortedUV) * step(hudDistortedUV, 1.0);
                float rtMask = insideRT.x * insideRT.y;
                float phosphorScan = abs(frac(hudDistortedUV.y * _ScreenParams.y * 0.32 + _Time.y * 14.0) - 0.5);
                float phosphorPulse = 0.82 + FastTriangleSigned(_Time.y * 2.1) * 0.06;
                float phosphorCoverage = saturate((hudEdgeFade * rtMask * 0.72 + (1.0 - smoothstep(0.0, 0.5, phosphorScan)) * 0.18) * phosphorPulse);
                float ditherAlpha = step(Bayer4x4(floor(IN.positionCS.xy) + float2(floor(_Time.y * 16.0), 0.0)), phosphorCoverage);
                hudAlpha = ditherAlpha;
                return half4(0.0h, 1.0h, 0.0h, (half)hudAlpha);
#else
                float2 insideRT = step(0.0, hudDistortedUV) * step(hudDistortedUV, 1.0);
                float rtMask = insideRT.x * insideRT.y;
                float hudVisibleMask = rtMask * hudEdgeFade;
                float4 hudBaseSample = 0.0;
                [branch]
                if (hudVisibleMask > 0.0001)
                    hudBaseSample = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV);
                float hudFocusBlur = saturate(_HectonHudFocusBlur);
                if (hudFocusBlur > 0.0001 && hudVisibleMask > 0.0001)
                {
                    float2 hudFocusBlurStep = float2(0.0018, 0.0012) * hudFocusBlur;
                    hudBaseSample += SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + hudFocusBlurStep);
                    hudBaseSample += SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV - hudFocusBlurStep);
                    hudBaseSample *= 0.33333334;
                }
                float4 hudSample = hudBaseSample;
                float2 hudSplitOffset = hudHypoxiaOffset + hudDecaySplit;
                float hudSplitMagnitude = max(abs(hudSplitOffset.x), abs(hudSplitOffset.y));
                if (hudSplitMagnitude > 0.0001 && hudVisibleMask > 0.0001)
                {
                    float4 hudSampleR = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + hudSplitOffset);
                    float4 hudSampleB = SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV - hudSplitOffset);
                    hudSample.rgb = float3(hudSampleR.r, hudSample.g, hudSampleB.b);
                }
                hudAlpha = hudSample.a * hudEdgeFade * rtMask;
                float hudTintStrength = saturate(_HUD_Color.a);
                hudColor = lerp(hudSample.rgb, hudSample.rgb * _HUD_Color.rgb, hudTintStrength) * _HUD_Intensity;
                float batteryActiveMask = 0.0;
                float batteryLedMask = 0.0;
                [branch]
                if (hudVisibleMask > 0.0001)
                {
                    batteryLedMask = ComputeToolBatteryLedMask(hudDistortedUV, _ToolBatteryNormalized, batteryActiveMask);
                    float batteryInactiveMask = saturate(batteryLedMask - batteryActiveMask);
                    float3 batteryActiveColor = _HUD_Color.rgb * (_HUD_Intensity * 1.35);
                    float3 batteryInactiveColor = _HUD_Color.rgb * (_HUD_Intensity * 0.18);
                    hudColor += (batteryActiveColor * batteryActiveMask) + (batteryInactiveColor * batteryInactiveMask);
                    hudAlpha = saturate(hudAlpha + (batteryLedMask * 0.9));
                }
                float hudLuminance = dot(hudColor, float3(0.2126, 0.7152, 0.0722));
                float hudFogBleed = saturate(_HectonHudFogPerturbation.x * hudAlpha * 0.08);
                sceneColor += _HUD_Color.rgb * hudFogBleed;
                hudColor = lerp(hudColor, hudLuminance.xxx, hypoxiaLevel * 0.78);
                float decayMask = 0.0;
                [branch]
                if (hazardGlitch > 0.0001)
                {
                    float decayNoise = Hash21(floor(hudDistortedUV * _ScreenParams.xy * (0.16 + hazardGlitch * 0.24)) + float2(floor(_Time.y * 26.0), tearBands));
                    decayMask = step(0.46 - hazardGlitch * 0.22, decayNoise) * hazardGlitch;
                }
                hudColor = lerp(hudColor, hudColor.bgr, hazardRadiation * 0.34);
                hudColor += decayMask.xxx * (hazardToxic * 0.22);
                hudColor += pressureFlickerGate.xxx * hullStressFlicker * 0.045;
                float hypoxiaStaticStrength = saturate((hypoxiaLevel - 0.33333334) * 1.5);
                float hypoxiaAlphaDissolve = 0.0;
                [branch]
                if (hypoxiaStaticStrength > 0.0001)
                {
                    float hypoxiaStatic = (Hash21(floor(hudDistortedUV * _ScreenParams.xy * 0.85) + floor(_Time.y * 24.0)) - 0.5) * hypoxiaStaticStrength;
                    hudColor += hypoxiaStatic.xxx * 0.22;
                    float hypoxiaAlphaNoise = Hash21(floor(hudDistortedUV * _ScreenParams.xy) + float2(floor(_Time.y * 43.0), floor(_Time.y * 29.0)));
                    hypoxiaAlphaDissolve = saturate((hypoxiaAlphaNoise - 0.45) * 1.9) * hypoxiaStaticStrength;
                }
                [branch]
                if (criticalHypoxia > 0.0001)
                {
                    float criticalHypoxiaAlphaNoise = Hash21(
                        floor((hudDistortedUV + float2(_Time.y * 0.31, _Time.y * -0.27)) * _ScreenParams.xy * 3.2)
                        + float2(floor(_Time.y * 81.0), floor(_Time.y * 53.0)));
                    criticalHypoxiaAlphaDissolve = saturate((criticalHypoxiaAlphaNoise - 0.32) * 1.45) * criticalHypoxia;
                }
                hudAlpha *= 1.0 - (hypoxiaAlphaDissolve * 0.68);
                hudAlpha *= 1.0 - (criticalHypoxiaAlphaDissolve * criticalHypoxiaEdgeVignette * 0.52);
                if (biosRecoverySwitch > 0.5 && hudVisibleMask > 0.0001)
                {
                    float2 phosphorTrailOffset = float2(-(0.0015 + hazardRadiation * 0.004 + hazardGlitch * 0.002), 0.0);
                    float trailLuminanceA = dot(
                        SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + phosphorTrailOffset).rgb,
                        float3(0.2126, 0.7152, 0.0722));
                    float trailLuminanceB = dot(
                        SAMPLE_TEXTURE2D(_HUD_RenderTexture, sampler_HUD_RenderTexture, hudDistortedUV + phosphorTrailOffset * 2.5).rgb,
                        float3(0.2126, 0.7152, 0.0722));
                    float rawHudLuminance = dot(hudBaseSample.rgb, float3(0.2126, 0.7152, 0.0722));
                    float trailLuminance = max(rawHudLuminance, max(trailLuminanceA * 0.72, trailLuminanceB * 0.46));
                    float biosNoise = ResolveFrostBlueNoise(hudDistortedUV + float2(tearBands * 0.0007, 0.0), _Time.y + 5.0);
                    float biosScan = abs(frac(hudDistortedUV.y * _ScreenParams.y * 0.28 + _Time.y * 16.0) - 0.5);
                    float phosphorLineMask = step(0.24, frac(hudDistortedUV.y * _ScreenParams.y * 0.32));
                    float biosThreshold = 0.38 + biosNoise * 0.16 + biosScan * 0.12;
                    float biosPrimaryBit = step(biosThreshold, trailLuminance);
                    float biosTrailBit = step(biosThreshold + 0.08, max(trailLuminanceA * 0.82, trailLuminanceB * 0.64));
                    float phosphorPulse = 0.82 + FastTriangleSigned(_Time.y * 2.1) * 0.06;
                    float phosphorScanGlow = (1.0 - smoothstep(0.0, 0.5, biosScan)) * 0.16;
                    float phosphorCore = biosPrimaryBit * phosphorLineMask;
                    float phosphorTrail = biosTrailBit * (1.0 - phosphorLineMask) * 0.55;
                    float phosphorLevel = saturate(phosphorCore + phosphorTrail + phosphorScanGlow);
                    float3 biosColor = float3(0.0, phosphorLevel * phosphorPulse, 0.0) * _HUD_Intensity;
                    hudColor = biosColor;
                    hudAlpha = saturate(max(hudBaseSample.a * hudEdgeFade * rtMask * 0.3, phosphorLevel * rtMask * hudEdgeFade));
                }
#endif

                float fragRawDepth = saturate(IN.positionCS.z * rcp(max(IN.positionCS.w, 0.0001)));
                float sceneRawDepth = SampleSceneDepth(screenUV);
#if UNITY_REVERSED_Z
                float sceneDepthValid = step(0.0001, sceneRawDepth);
#else
                float sceneDepthValid = step(sceneRawDepth, 0.9999);
#endif
                float linearSceneDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                [branch]
                if (hudAlpha > 0.001)
                {
                    float linearFragDepth = LinearEyeDepth(fragRawDepth, _ZBufferParams);
                    float hudOccluded = sceneDepthValid * step(linearSceneDepth + 0.002, linearFragDepth);
                    float closeDepthDelta = max(0.0, linearFragDepth - linearSceneDepth);
                    float closeOcclusionRange = max(0.001, _HudCloseOcclusionDistance);
                    float hudCloseOcclusion = hudOccluded * (1.0 - smoothstep(closeOcclusionRange * 0.45, closeOcclusionRange, closeDepthDelta));
                    float occlusionFrame = floor(_Time.y * 18.0);
                    float occlusionBayer = Bayer4x4(floor(IN.positionCS.xy) + float2(occlusionFrame, occlusionFrame));
                    float occlusionKeep = lerp(1.0, lerp(0.35, 1.0, step(occlusionBayer, saturate(hudAlpha * 0.72 + 0.18))), hudCloseOcclusion);
                    hudAlpha *= occlusionKeep;
                }

                float wetImperfectionBoost = 1.0 + runoffMask * 0.9;
                float boostedFingerprint = saturate(fingerprint * wetImperfectionBoost);
                float scratchBleed = scratchMask * _HUD_ScratchBleed * hudAlpha * wetImperfectionBoost;
                float3 hudScratchGlow = hudColor * scratchBleed * 0.5;
                float3 hudFingerprintGlow = hudColor * boostedFingerprint * hudAlpha * 0.3;

                float3 envRefl = 0.0;
                [branch]
                if (_EnvReflStrength > 0.0001)
                {
                    float edgeReflection = saturate((1.0 - NdotV) * (0.52 + scratchMask * 0.16 + runoffMask * 0.22));
                    float sceneReflectionLuma = dot(sceneColor, float3(0.2126, 0.7152, 0.0722));
                    float smoothReflection = lerp(0.65, 1.0, saturate(_Smoothness));
                    envRefl = (fresnelColor * 0.62 + sceneColor * (0.08 + sceneReflectionLuma * 0.05) + _BaseColor.rgb * 0.04) *
                        (edgeReflection * smoothReflection * _EnvReflStrength);
                }

                Light mainLight = GetMainLight();
                float mainLightLuminance = saturate(dot(mainLight.color, float3(0.2126, 0.7152, 0.0722)));
                float3 specular = 0.0;
                float directLightGlint = 0.0;
                [branch]
                if (mainLightLuminance > 0.0001)
                {
                    float normalLightFacing = saturate(dot(normalWS, mainLight.direction));
                    float grazingGlare = saturate((1.0 - NdotV) * 0.55 + IN.glareData.y * 0.45);
                    float specularBase = saturate(normalLightFacing * (0.62 + grazingGlare * 0.38));
                    float specular2 = specularBase * specularBase;
                    float specular4 = specular2 * specular2;
                    float specular8 = specular4 * specular4;
                    float smoothness01 = saturate(_Smoothness);
                    float cinematicTightGlint = specular8 * lerp(specular4, specular8, smoothness01);
                    specular = mainLight.color * cinematicTightGlint * 0.3;
                    directLightGlint = specular8 * lerp(0.35, 1.0, smoothness01) * mainLightLuminance;
                }
                float wetHazeMask = saturate(runoffMask * (0.45 + proceduralSmudgeMask * 0.55) + scratchMask * runoffMask * 0.35);
                float condensationHazeMask = saturate(condensationMask * (0.52 + proceduralSmudgeMask * 0.3 + scratchMask * 0.18));
                float3 runoffSheen = (fresnelColor * 0.55 + specular * 0.25 + mainLight.color * 0.04) * runoffMask;
                float sceneLuminance = dot(sceneColor, float3(0.2126, 0.7152, 0.0722));
                float glareDepthOccluder = sceneDepthValid *
                    (1.0 - smoothstep(1.5, 12.0, linearSceneDepth)) *
                    smoothstep(0.72, 1.0, IN.glareData.y);
                float glareDepthVisibility = saturate(1.0 - glareDepthOccluder);
                float cameraLightGlare = (IN.glareData.y + saturate(mainLightLuminance * 0.25) * directLightGlint) * glareDepthVisibility;
                float brightLightGlare = saturate((sceneLuminance - 0.62) * 1.85 + directLightGlint * 0.32 + cameraLightGlare * 0.65);
                float diegeticDirtGlareBoost = lerp(1.0, IN.glareData.x, glareDepthVisibility);
                float imperfectionGlareMask = saturate(
                    scratchMask * 0.52 +
                    boostedFingerprint * 0.36 +
                    runoffMask * 0.28 +
                    condensationMask * 0.18 +
                    frostMask * 0.10 +
                    pressureCrackMask * 0.48 +
                    blueNoiseGrimeMask * 0.44);
                float lensDirtGlare = brightLightGlare * imperfectionGlareMask * diegeticDirtGlareBoost * smoothstep(0.16, 0.98, edgeDist);
                float3 lensDirtGlareColor = (fresnelColor + mainLight.color * 0.08 + _HUD_Color.rgb * 0.04) *
                    lensDirtGlare *
                    (1.0 + runoffMask * 0.8 + blueNoiseMoistureMask * 0.35);
                float3 pressureCrackColor = lerp(float3(0.48, 0.72, 0.82), _HUD_Color.rgb, 0.35);

                float3 finalColor = 0.0;
                finalColor += sceneColor * (1.0 - _BaseColor.a);
                finalColor += _BaseColor.rgb * _GlassAlpha;
                finalColor = lerp(finalColor, finalColor * 0.85 + 0.02, smudgeOpacity);
                finalColor += envRefl;
                finalColor += specular;
                finalColor += fresnelColor;
                finalColor += hudColor * hudAlpha;
                finalColor += hudScratchGlow;
                finalColor += hudFingerprintGlow;
                finalColor = lerp(finalColor, sceneColor * 0.86 + 0.04 + fresnelColor * 0.2, runoffMask * 0.35);
                finalColor = lerp(finalColor, finalColor * 0.78 + sceneColor * 0.18 + fresnelColor * 0.12, wetHazeMask * 0.22);
                finalColor = lerp(
                    finalColor,
                    finalColor * 0.74 + sceneColor * 0.14 + fresnelColor * 0.18 + float3(0.055, 0.07, 0.075),
                    condensationHazeMask * 0.42);
                finalColor = lerp(
                    finalColor,
                    finalColor * 0.55 + float3(0.72, 0.78, 0.82) * 0.38 + sceneColor * 0.08,
                    frostMask * 0.65);
                finalColor += runoffSheen;
                finalColor += sonarOverlayColor * sonarOverlayMask;
                finalColor += staticNoise * (_StaticNoise * 0.045);
                finalColor += lensDirtGlareColor;
                finalColor += pressureCrackColor * pressureCrackMask * (0.35 + hullStressFlicker * 0.28);
                finalColor = lerp(finalColor, finalColor.brg, criticalHealthGlitch * criticalSpikeGate * 0.12);
                float noirVignetteMask = smoothstep(0.34, 1.04, radialMagnitude);
                float noirVignetteNoise = 0.5;
                [branch]
                if (noirVignetteMask > 0.0001)
                {
                    noirVignetteNoise = ResolveFrostBlueNoise(
                        screenUV * 1.73 + float2(_Time.y * 0.009, _Time.y * -0.011),
                        _Time.y + 31.0);
                }
                float noirVignetteStrength = saturate(
                    noirVignetteMask *
                    (0.58 + noirVignetteNoise * 0.22 + stressHudChromatic * 0.22 + saturate(_HectonHudStressVignette) * 0.35 + criticalHealthGlitch * 0.18));
                finalColor *= 1.0 - noirVignetteStrength * 0.52;
                finalColor += _HUD_Color.rgb * noirVignetteStrength * hudAlpha * 0.025;
                [branch]
                if (lowTierDitherScale > 0.0001)
                {
                    float lowTierEdge = smoothstep(0.30, 1.0, radialMagnitude);
                    float lowTierFault = saturate(max(max(hazardGlitch, criticalHealthGlitch), stressHudChromatic));
                    float lowTierCoverage = saturate(lowTierEdge * (0.16 + lowTierFault * 0.34) * lowTierDitherScale);
                    float lowTierDither = step(Bayer4x4(floor(IN.positionCS.xy)), lowTierCoverage);
                    float3 lowTierTint = finalColor * 0.82 + _HUD_Color.rgb * (0.018 + lowTierFault * 0.028);
                    finalColor = lerp(finalColor, lowTierTint, lowTierDither * lowTierEdge * lowTierDitherScale);
                }
                float vrComfortVignette = saturate(_HectonVrComfortSignals.x) * vrComfortEnabled;
                float vrComfortVelocitySq = saturate(_HectonVrComfortMotion.z) * vrComfortEnabled;
                float vrComfortTunnel = saturate(max(vrComfortVignette, vrComfortVelocitySq));
                [branch]
                if (vrComfortTunnel > 0.0001)
                {
                    float vrComfortIgn = frac(52.9829189 * frac(dot(floor(screenUV * _ScreenParams.xy) + floor(_Time.y * 37.0), float2(0.06711056, 0.00583715))));
                    float vrComfortInner = lerp(0.74, 0.30, vrComfortTunnel);
                    float vrComfortMask = smoothstep(vrComfortInner, 1.02, radialMagnitude);
                    float vrComfortDither = step(vrComfortIgn, saturate(vrComfortTunnel * 0.92 + vrComfortMask * 0.08));
                    float vrComfortStrength = vrComfortMask * vrComfortTunnel * lerp(0.58, 1.0, vrComfortDither);
                    finalColor *= 1.0 - vrComfortStrength * 0.68;
                    finalColor += _HUD_Color.rgb * vrComfortStrength * hudAlpha * 0.045;
                }

                float visualStaticGlitch = saturate(_HectonVisualStaticGlitch);
                [branch]
                if (visualStaticGlitch > 0.0001)
                {
                    float2 staticPixel = floor(screenUV * _ScreenParams.xy);
                    float seed = floor(_Time.y * 60.0 + _HectonVisualStaticGlitchSeed);
                    float ign = frac(52.9829189 * frac(dot(staticPixel + seed, float2(0.06711056, 0.00583715))));
                    float staticBit = step(0.5, ign);
                    float scanGate = step(0.22, frac(staticPixel.y * 0.5));
                    float staticValue = staticBit * (0.68 + scanGate * 0.32);
                    finalColor = lerp(finalColor, staticValue.xxx, visualStaticGlitch * 0.92);
                }

                float foveatedEdge = 0.0;
                [branch]
                if (_HectonXRFoveatedParams.x > 0.5)
                {
                    float2 foveatedDelta = radialScreenOffset - _HectonXRFoveatedCenterRadius.xy;
                    float2 foveatedAbs = abs(foveatedDelta);
                    float foveatedApprox = max(foveatedAbs.x, foveatedAbs.y) + min(foveatedAbs.x, foveatedAbs.y) * 0.375;
                    float foveatedRadial = saturate(foveatedApprox * 1.75);
                    float foveatedInner = max(_HectonXRFoveatedCenterRadius.z, 0.32);
                    float foveatedOuter = max(_HectonXRFoveatedCenterRadius.w, foveatedInner + 0.001);
                    foveatedEdge = smoothstep(foveatedInner, foveatedOuter, foveatedRadial) * saturate(_HectonXRFoveatedParams.y);
                    [branch]
                    if (foveatedEdge > 0.0001)
                    {
                        float foveatedIgn = ResolveFrostBlueNoise(
                            screenUV * 2.11 + frac(_Time.y * float2(0.7548777, 0.5698403)),
                            _Time.y + 47.0);
                        float foveatedLevels = lerp(192.0, 48.0, foveatedEdge);
                        float3 foveatedQuantized = floor(max(finalColor, 0.0) * foveatedLevels + foveatedIgn) / foveatedLevels;
                        finalColor = lerp(finalColor, foveatedQuantized + (foveatedIgn - 0.5) * 0.006, foveatedEdge * 0.42);
                    }
                }

                float finalAlpha = _GlassAlpha
                    + hudAlpha * 0.9
                    + fresnel * 0.4
                    + smudgeOpacity
                    + scratchBleed * 0.2
                    + runoffMask * 0.18
                    + wetHazeMask * 0.08
                    + condensationHazeMask * 0.16
                    + frostMask * 0.22
                    + pressureCrackMask * 0.16
                    + sonarOverlayMask * 0.08
                    + lensDirtGlare * 0.08
                    + blueNoiseGrimeMask * 0.04;
                finalAlpha *= 1.0 - (criticalHypoxiaAlphaDissolve * criticalHypoxiaEdgeVignette * 0.18);
                float sceneDepthCutoutFade = sceneDepthValid *
                    saturate((linearSceneDepth - LinearEyeDepth(fragRawDepth, _ZBufferParams)) * rcp(max(_HudCloseOcclusionDistance, 0.01)));
                finalAlpha *= lerp(1.0, max(sceneDepthCutoutFade, 0.2), sceneDepthValid);
                finalAlpha = saturate(finalAlpha);

                finalColor = MixFog(finalColor, IN.fogCoord);
                if (biosRecoverySwitch > 0.5 && hudVisibleMask > 0.0001)
                {
                    float biosSceneDither = ResolveFrostBlueNoise(screenUV, _Time.y + 11.0);
                    float biosSceneScan = abs(frac(screenUV.y * _ScreenParams.y * 0.31 + _Time.y * 11.0) - 0.5);
                    float biosSceneLuminance = FastRootCurve01(dot(finalColor, float3(0.2126, 0.7152, 0.0722)));
                    float biosSceneThreshold = 0.42 + (biosSceneDither - 0.5) * 0.28 + biosSceneScan * 0.16;
                    float biosSceneBit = step(biosSceneThreshold, biosSceneLuminance);
                    float biosSceneLine = step(0.22, frac(screenUV.y * _ScreenParams.y * 0.42));
                    finalColor = float3(0.0, biosSceneBit * biosSceneLine * (0.82 + FastTriangleSigned(_Time.y * 2.1) * 0.04), 0.0);
                    finalAlpha = saturate(max(finalAlpha, 0.72));
                }
                clip(finalAlpha - Bayer4x4(IN.positionCS.xy));
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
