Shader "Hecton8/Flora/SargassumMaster"
{
    Properties
    {
        [MainColor] _DryColor ("Dry Color", Color) = (0.60, 0.42, 0.18, 1)
        _WetColor ("Wet Color", Color) = (0.34, 0.25, 0.10, 1)
        _BubbleColor ("Bubble Color", Color) = (1.00, 0.78, 0.34, 1)
        _RimColor ("Rim Color", Color) = (0.84, 0.64, 0.28, 1)
        _SSSColor ("SSS Color", Color) = (1.00, 0.88, 0.48, 1)
        _CutEdgeColor ("Cut Edge Color", Color) = (1.00, 0.74, 0.32, 1)
        _BiolumColor ("Biolum Color", Color) = (0.16, 0.86, 0.88, 1)
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.36
        _Smoothness ("Smoothness", Range(0, 1)) = 0.44
        _NormalInfluence ("Normal Influence", Range(0, 1)) = 0.22
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.32
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.2
        _SSSStrength ("SSS Strength", Range(0, 4)) = 1.4
        _SSSPower ("SSS Power", Range(1, 12)) = 5.6
        _BubbleGlow ("Bubble Glow", Range(0, 2)) = 0.28
        _SwayAmplitude ("Sway Amplitude", Range(0, 0.5)) = 0.12
        _SwayFrequency ("Sway Frequency", Range(0, 8)) = 1.8
        _SwaySpeed ("Sway Speed", Range(0, 4)) = 0.82
        _PhaseScale ("Phase Scale", Range(0, 12)) = 6.5
        _BeardSwingMultiplier ("Beard Swing Multiplier", Range(0, 3)) = 1.3
        _PulsationAmplitude ("Pulsation Amplitude", Range(0, 0.5)) = 0.08
        _PulsationFrequency ("Pulsation Frequency", Range(0, 8)) = 1.35
        _PulsationSpeed ("Pulsation Speed", Range(0, 4)) = 0.48
        _WoundCurlStrength ("Wound Curl Strength", Range(0, 1)) = 0.18
        _BiolumStrength ("Biolum Strength", Range(0, 4)) = 0.75
        _BiolumMaskStrength ("Biolum Mask Strength", Range(0, 2)) = 1.15
        _BiolumNightResponse ("Biolum Night Response", Range(0, 2)) = 1.0
        _NoirSignalFlickerStrength ("Noir Signal Flicker Strength", Range(0, 0.35)) = 0.08
        _NoirSignalFlickerScale ("Noir Signal Flicker Scale", Range(0.01, 4)) = 0.42
        _InteractionPosition ("Interaction Position", Vector) = (0,0,0,0)
        _InteractionRadius ("Interaction Radius", Range(0.05, 6)) = 0.8
        _InteractionCutStrength ("Interaction Cut Strength", Range(0, 1)) = 0
        _InteractionEdgeBoost ("Interaction Edge Boost", Range(0, 4)) = 1.2
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DryColor;
                half4 _WetColor;
                half4 _BubbleColor;
                half4 _RimColor;
                half4 _SSSColor;
                half4 _CutEdgeColor;
                half4 _BiolumColor;
                half _AlphaClip;
                half _Smoothness;
                half _NormalInfluence;
                half _RimStrength;
                half _RimPower;
                half _SSSStrength;
                half _SSSPower;
                half _BubbleGlow;
                half _SwayAmplitude;
                half _SwayFrequency;
                half _SwaySpeed;
                half _PhaseScale;
                half _BeardSwingMultiplier;
                half _PulsationAmplitude;
                half _PulsationFrequency;
                half _PulsationSpeed;
                half _WoundCurlStrength;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumNightResponse;
                half _NoirSignalFlickerStrength;
                half _NoirSignalFlickerScale;
                float3 _InteractionPosition;
                half _InteractionRadius;
                half _InteractionCutStrength;
                half _InteractionEdgeBoost;
            CBUFFER_END

            float4 _HectonPropWashPosition;
            float4 _SargassumGlobalDriftOffset;
            half _HectonPropWashForce;
            half4 _HectonOceanBiolumColor;
            half _HectonOceanBiolumStrength;
            float4x4 _GlobalBiolumDearLieGroups;
            float4 _GlobalBiolumParams;
            float4 _GlobalBiolumClock;
            float _HectonTimeOfDay01;
            float _HectonNightFactor;
            float _SargassumBiolumPhaseMultiplier;
            float4 _SargassumBuoyancySinkWorldRect;
            float _SargassumBuoyancySinkDepth;
            float4 _SargassumCutMaskWorldRect;
            float _SargassumCutMaskActive;

            TEXTURE2D(_SargassumBuoyancySinkRT);
            SAMPLER(sampler_SargassumBuoyancySinkRT);
            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half2 uv : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                float3 biolumLocalAupCoord : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float SargassumFiniteOr(float value, float fallbackValue)
            {
                return isfinite(value) ? value : fallbackValue;
            }

            float2 SargassumFiniteOr(float2 value, float2 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float3 SargassumFiniteOr(float3 value, float3 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float4 SargassumFiniteOr(float4 value, float4 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float SargassumWrappedVisualTime()
            {
                float wrapped = SargassumFiniteOr(_GlobalBiolumClock.x, 0.0);
                return max(wrapped, 0.0);
            }

            half SargassumTrianglePulse01(float phase)
            {
                phase = SargassumFiniteOr(phase, 0.0);
                return (half)(1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0));
            }

            half SargassumTriangleSigned(float phase)
            {
                return SargassumTrianglePulse01(phase) * 2.0h - 1.0h;
            }

            float SargassumApproxMagnitude2(float2 value)
            {
                float2 axis = abs(value);
                float major = max(axis.x, axis.y);
                float minor = min(axis.x, axis.y);
                return major + minor * 0.375;
            }

            float SargassumApproxMagnitude3(float3 value)
            {
                float3 axis = abs(value);
                float major = max(max(axis.x, axis.y), axis.z);
                float minor = min(min(axis.x, axis.y), axis.z);
                float mid = axis.x + axis.y + axis.z - major - minor;
                return major + mid * 0.375 + minor * 0.125;
            }

            float2 SargassumSafeNormalize2(float2 value)
            {
                value = SargassumFiniteOr(value, float2(1.0, 0.0));
                float approxLen = SargassumApproxMagnitude2(value);
                return isfinite(approxLen) && approxLen > 0.0001 ? value * rcp(approxLen) : float2(1.0, 0.0);
            }

            float3 SargassumSafeNormalize3(float3 value)
            {
                value = SargassumFiniteOr(value, float3(0.0, 1.0, 0.0));
                float approxLen = SargassumApproxMagnitude3(value);
                return isfinite(approxLen) && approxLen > 0.0001 ? value * rcp(approxLen) : float3(0.0, 1.0, 0.0);
            }

            half SargassumFastPower01(half value, half exponent)
            {
                half v = saturate(value);
                half v2 = v * v;
                half v4 = v2 * v2;
                half v8 = v4 * v4;
                half v16 = v8 * v8;
                half low = lerp(v, v4, saturate((exponent - 1.0h) * 0.33333333h));
                half high = lerp(v4, v16, saturate((exponent - 4.0h) * 0.08333333h));
                return lerp(low, high, step(4.0h, exponent));
            }

            half SargassumFastSpecularPower01(half value, half exponent)
            {
                half v = saturate(value);
                half v2 = v * v;
                half v4 = v2 * v2;
                half v8 = v4 * v4;
                half v16 = v8 * v8;
                half v32 = v16 * v16;
                half low = lerp(v8, v16, saturate((exponent - 8.0h) * 0.125h));
                half high = lerp(v16, v32, saturate((exponent - 16.0h) * 0.0625h));
                return lerp(low, high, step(16.0h, exponent));
            }

            half EvaluateLeafMask(half2 uv, half phase)
            {
                uv = saturate((half2)SargassumFiniteOr((float2)uv, float2(0.5, 0.5)));
                phase = (half)SargassumFiniteOr((float)phase, 0.0);
                half edge = abs(uv.x * 2.0h - 1.0h);
                half phaseScale = (half)SargassumFiniteOr((float)_PhaseScale, 0.0);
                half serration = SargassumTriangleSigned((uv.y * 18.0h + phase * phaseScale) * 6.28318h) * 0.08h;
                return saturate(1.0h - smoothstep(0.46h + serration, 0.94h, edge));
            }

            half EvaluateCutMask(float3 positionWS)
            {
                positionWS = SargassumFiniteOr(positionWS, float3(0.0, 0.0, 0.0));
                float3 interactionPosition = SargassumFiniteOr(_InteractionPosition, positionWS);
                float3 delta = positionWS - interactionPosition;
                float radius = max(abs(SargassumFiniteOr((float)_InteractionRadius, 0.0)), 0.0001);
                float invRadiusSq = rcp(max(radius * radius, 0.0001));
                half normalized = saturate(1.0h - (half)(dot(delta, delta) * invRadiusSq));
                return normalized * normalized * saturate((half)SargassumFiniteOr((float)_InteractionCutStrength, 0.0));
            }

            half EvaluateGlobalCutMask(float3 positionWS)
            {
                if (!isfinite(_SargassumCutMaskActive) || _SargassumCutMaskActive < 0.5 || !all(isfinite(positionWS)))
                    return 0.0h;

                float4 cutRect = SargassumFiniteOr(_SargassumCutMaskWorldRect, float4(0.0, 0.0, 0.0, 0.0));
                float2 uv = float2(
                    (positionWS.x - cutRect.x) * cutRect.z,
                    (positionWS.z - cutRect.y) * cutRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                return (half)SargassumFiniteOr(SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r, 0.0);
            }

            half EvaluateBuoyancySinkOffset(float2 worldXZ)
            {
                float sinkDepth = SargassumFiniteOr(_SargassumBuoyancySinkDepth, 0.0);
                if (sinkDepth <= 0.0001 || !all(isfinite(worldXZ)))
                    return 0.0h;

                float4 sinkRect = SargassumFiniteOr(_SargassumBuoyancySinkWorldRect, float4(0.0, 0.0, 0.0, 0.0));
                float2 driftOffset = SargassumFiniteOr(_SargassumGlobalDriftOffset.xz, float2(0.0, 0.0));
                float2 sampleXZ = worldXZ - driftOffset;
                float2 uv = float2(
                    (sampleXZ.x - sinkRect.x) * sinkRect.z,
                    (sampleXZ.y - sinkRect.y) * sinkRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                half sink01 = (half)SargassumFiniteOr(SAMPLE_TEXTURE2D_LOD(_SargassumBuoyancySinkRT, sampler_SargassumBuoyancySinkRT, uv, 0).r, 0.0);
                return sink01 * (half)sinkDepth;
            }

            float Hash21(float2 value)
            {
                value = SargassumFiniteOr(value, float2(0.0, 0.0));
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float EvaluateOrganicDensity(float2 worldXZ)
            {
                float2 safeWorldXZ = SargassumFiniteOr(worldXZ, float2(0.0, 0.0));
                float2 driftOffset = SargassumFiniteOr(_SargassumGlobalDriftOffset.xz, float2(0.0, 0.0));
                float2 sample = safeWorldXZ * 0.028 + driftOffset * 0.015;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.93 + 17.0));
                float wave = SargassumTrianglePulse01(sample.x * 1.2 + sample.y * 0.94 + SargassumWrappedVisualTime() * 0.1);
                return saturate(coarse * 0.46 + fine * 0.34 + wave * 0.20);
            }

            half3 ResolveSargassumBiolumGroupTint(int stateIndex)
            {
                half3 tint0 = half3(0.18h, 0.88h, 1.00h);
                half3 tint1 = half3(0.32h, 1.00h, 0.62h);
                half3 tint2 = half3(0.74h, 0.38h, 1.00h);
                half3 tint3 = half3(1.00h, 0.72h, 0.32h);
                half idx = (half)stateIndex;
                half3 lowPair = lerp(tint0, tint1, step(0.5h, idx));
                half3 highPair = lerp(tint2, tint3, step(2.5h, idx));
                return lerp(lowPair, highPair, step(1.5h, idx));
            }

            half4 ResolveSargassumGlobalBiolum(float3 localAupCoord)
            {
                if (!all(isfinite(localAupCoord)))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float4 safeParams = all(isfinite(_GlobalBiolumParams)) ? _GlobalBiolumParams : float4(0.0, 0.0, 0.0, 0.0);

                int activeCount = min(max((int)floor(max(safeParams.x, 0.0)), 0), 4);
                if (activeCount <= 0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float selector = frac(abs(localAupCoord.x * 0.041 + localAupCoord.z * 0.033));
                int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
                float4 stateRaw = _GlobalBiolumDearLieGroups[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                const float invTwoPi = 0.159154943091895;
                float frequency = max(abs(state.y), 0.0025);
                float spatialPhase = dot(localAupCoord, float3(0.041, 0.019, 0.033)) + state.w;
                half primaryPulse = (half)(1.0 - abs(frac(state.x * invTwoPi + spatialPhase * frequency) * 2.0 - 1.0));
                half strobe = saturate((half)max(safeParams.z, 0.0));
                half qualityCurve = saturate((half)max(safeParams.y, 0.0));
                qualityCurve = qualityCurve * qualityCurve * (3.0h - 2.0h * qualityCurve);
                int secondaryIndex = stateIndex + 1;
                if (secondaryIndex >= activeCount)
                    secondaryIndex = 0;
                float4 secondaryStateRaw = _GlobalBiolumDearLieGroups[secondaryIndex];
                float4 secondaryState = all(isfinite(secondaryStateRaw)) ? secondaryStateRaw : float4(0.0, 0.0, 0.0, 0.0);
                float secondaryFrequency = max(abs(secondaryState.y), 0.0025);
                float secondarySpatialPhase = dot(localAupCoord, float3(0.031, -0.017, 0.029)) + secondaryState.w;
                half secondaryPulse = (half)(1.0 - abs(frac(secondaryState.x * invTwoPi + secondarySpatialPhase * secondaryFrequency) * 2.0 - 1.0));
                half overdrive = 0.0h;
                half godSpark = 0.0h;
                half godHaze = 0.0h;
                half overPulse = secondaryPulse;
                half filament = (half)(1.0 - abs(frac(state.x * invTwoPi + dot(localAupCoord, float3(0.127, 0.083, 0.167)) * frequency + state.w) * 2.0 - 1.0));
                godHaze = smoothstep(0.42h, 0.92h, overPulse) * (0.50h + filament * 0.50h) * qualityCurve;
                godSpark = smoothstep(0.80h, 0.98h, filament) * overPulse * qualityCurve;
                overdrive = saturate(overPulse * 0.35h + godSpark * 0.22h) * qualityCurve;
                half3 color = lerp(ResolveSargassumBiolumGroupTint(stateIndex), half3(1.0h, 1.0h, 1.0h), strobe);
                half amplitude = (half)max(state.z, 0.0) * (0.63h + primaryPulse * 0.37h);
                half secondaryAmplitude = (half)max(secondaryState.z, 0.0) * (0.63h + secondaryPulse * 0.37h);
                half intensity = clamp(max(amplitude, strobe * 10.0h), 0.0h, 10.0h);
                color = lerp(color, ResolveSargassumBiolumGroupTint(secondaryIndex), overdrive);
                color = saturate(color + godHaze * half3(0.04h, 0.16h, 0.19h));
                intensity = clamp(intensity + secondaryAmplitude * overdrive + godSpark * 0.5h + godHaze * 0.25h, 0.0h, 10.0h);
                return half4(color, intensity);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = SargassumFiniteOr(input.normalOS, float3(0.0, 1.0, 0.0));
                half4 vertexColor = saturate((half4)SargassumFiniteOr((float4)input.color, float4(1.0, 0.0, 0.0, 1.0)));
                float2 uv = saturate(SargassumFiniteOr(input.uv, float2(0.5, 0.5)));
                float timeSeconds = SargassumWrappedVisualTime();
                float3 positionOS = SargassumFiniteOr(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
                half phase = vertexColor.b;
                half rigidity = saturate(vertexColor.a);
                // heightMask is anchor-distance leverage: it scales sway, prop wash, pulsation and cut warp.
                // Gas bladders are the rigid class in 3DMODEL_FLORA_CORAL.md line 24 (sway amplitude 0), and the
                // octasphere writer path bakes a normal projection into uv.y, so on bladder vertices uv.y is not
                // an anchor distance at all - the equator lands on 0.5 while the two poles land on 0 and 1. Left
                // ungated that drove a radial equator pump of |normal.xz| * sway * 0.5 with both poles pinned:
                // measured 0.0423 object units against baked bladder radii of 0.031..0.0985, up to 136% of the
                // radius, which turns the smallest bladders inside out. Gating with the same bubble rule the
                // fragment stage uses keeps bladders rigid for the meshes already baked on disk and for any
                // future re-bake, so writer and shader agree either way. Bladders still travel with the clump:
                // drift offset and buoyancy sink are applied per-vertex in world space, below.
                half isBubble = step(0.85h, saturate(vertexColor.g));
                half heightMask = saturate((half)uv.y) * (1.0h - isBubble);
                half swingScale = lerp((half)SargassumFiniteOr((float)_BeardSwingMultiplier, 0.68), 0.68h, rigidity);
                half sway = SargassumTriangleSigned(timeSeconds * SargassumFiniteOr((float)_SwaySpeed, 0.0) + phase * SargassumFiniteOr((float)_PhaseScale, 0.0) + positionOS.y * SargassumFiniteOr((float)_SwayFrequency, 0.0)) * (half)max(0.0, SargassumFiniteOr((float)_SwayAmplitude, 0.0)) * swingScale;
                positionOS.xz += normalOS.xz * (sway * heightMask);

                float3 driftOffset = SargassumFiniteOr(_SargassumGlobalDriftOffset.xyz, float3(0.0, 0.0, 0.0));
                float4 propWashPosition = SargassumFiniteOr(_HectonPropWashPosition, float4(0.0, 0.0, 0.0, 0.0));
                float3 positionWS_Interact = TransformObjectToWorld(positionOS) + driftOffset;
                float3 washDir = positionWS_Interact - propWashPosition.xyz;
                float washRadius = max(abs(propWashPosition.w), 0.001);
                float washDistSq = dot(washDir, washDir);
                float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                if (washDistSq > 0.0001)
                    positionOS.xyz += SargassumSafeNormalize3(washDir) * (washStrength * max(0.0, SargassumFiniteOr((float)_HectonPropWashForce, 0.0)) * 0.45h * heightMask);

                float3 positionWS_Pulse = TransformObjectToWorld(positionOS) + driftOffset;
                float organicDensity = EvaluateOrganicDensity(positionWS_Pulse.xz);
                float edgePulse = saturate(1.0 - abs(organicDensity * 2.0 - 1.0));
                float pulsePhase = timeSeconds * SargassumFiniteOr((float)_PulsationSpeed, 0.0) + phase * (SargassumFiniteOr((float)_PhaseScale, 0.0) * 0.41h) + organicDensity * (SargassumFiniteOr((float)_PulsationFrequency, 0.0) * 6.28318h);
                float pulse = SargassumTriangleSigned(pulsePhase) * max(0.0, SargassumFiniteOr((float)_PulsationAmplitude, 0.0)) * edgePulse * heightMask;
                float2 radialOS = SargassumSafeNormalize2(positionOS.xz + float2(0.001, 0.001));
                positionOS.xz += radialOS * pulse;
                positionOS.y += pulse * 0.12;

                float3 positionWS_Cut = TransformObjectToWorld(positionOS) + driftOffset;
                half cutWarp = EvaluateCutMask(positionWS_Cut);
                cutWarp = smoothstep(0.05h, 0.9h, cutWarp) * (1.0h - rigidity) * heightMask;
                half woundCurlStrength = saturate((half)SargassumFiniteOr((float)_WoundCurlStrength, 0.0));
                positionOS.xz -= radialOS * (woundCurlStrength * cutWarp);
                positionOS.y -= woundCurlStrength * cutWarp * 0.24h;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS);
                float3 biolumOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0)) + driftOffset;
                float3 positionWS = TransformObjectToWorld(positionOS) + driftOffset;
                positionWS.y -= EvaluateBuoyancySinkOffset(positionWS.xz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = (half3)SargassumSafeNormalize3(normalInputs.normalWS);
                output.color = vertexColor;
                output.uv = uv;
                output.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionWS));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.biolumLocalAupCoord = positionWS - biolumOriginWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif

                half3 normalWS = (half3)SargassumSafeNormalize3(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                Light mainLight = GetMainLight();
                half3 lightDir = mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDir));
                half backLight = saturate(dot(-normalWS, lightDir));
                half ao = saturate(input.color.r);
                half sssMask = saturate(input.color.g);
                half isBubble = step(0.85h, sssMask);
                half leafMask = EvaluateLeafMask(input.uv, input.color.b);
                half alpha = lerp(leafMask, 1.0h, isBubble);
                half cutMask = max(EvaluateCutMask(input.positionWS), EvaluateGlobalCutMask(input.positionWS));
                alpha *= (1.0h - cutMask);
                clip(alpha - saturate((half)SargassumFiniteOr((float)_AlphaClip, 0.36)));

                half immersionDarkening = lerp(0.72h, 1.0h, ao);
                half3 leafColor = lerp(_WetColor.rgb, _DryColor.rgb, saturate(input.uv.y + ao * 0.32h));
                half3 albedo = lerp(leafColor, _BubbleColor.rgb, isBubble);
                albedo *= immersionDarkening;

                half ambientOcclusion = lerp(0.48h, 1.0h, ao);
                half3 ambient = H8CustomLightProbeResolveAmbient(input.positionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * ambientOcclusion;
                half3 diffuse = albedo * (ambient + mainLight.color * (0.25h + NdotL * 0.75h));

                half rim = SargassumFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;
                half sss = SargassumFastPower01(saturate(dot(-lightDir, viewDirWS)), _SSSPower) * _SSSStrength * sssMask;
                half bubbleGlow = isBubble * (_BubbleGlow + backLight * 0.55h);
                float3 biolumLocalAupCoord = input.biolumLocalAupCoord;
                half localPulsePhase = biolumLocalAupCoord.x * 0.085h + biolumLocalAupCoord.z * 0.061h + input.uv.y * 4.2h + input.color.b * 3.7h;
                half biolumPulse = 1.0h + SargassumTriangleSigned(localPulsePhase) * 0.18h;
                half timeBand = 0.75h + 0.25h * SargassumTriangleSigned(_HectonTimeOfDay01 * 6.28318h + input.color.b * 2.4h);
                half bubbleBiolumMask = saturate(isBubble * (0.68h + sssMask * 0.24h + bubbleGlow * 0.18h) * _BiolumMaskStrength);
                half nightFactor = saturate(_HectonNightFactor * _BiolumNightResponse);
                half oceanBiolumInfluence = saturate(_HectonOceanBiolumStrength);
                half3 biolumColor = lerp(_BiolumColor.rgb, _HectonOceanBiolumColor.rgb, oceanBiolumInfluence * 0.65h);
                half4 globalBiolumState = ResolveSargassumGlobalBiolum(biolumLocalAupCoord);
                half globalBiolumMask = step(0.001h, globalBiolumState.w);
                biolumColor = lerp(biolumColor, globalBiolumState.rgb, globalBiolumMask);
                half masterBiolum = globalBiolumState.w;
                half biolumEnergy = clamp(_BiolumStrength * masterBiolum * (1.0h + oceanBiolumInfluence * 0.7h) * bubbleBiolumMask * biolumPulse * timeBand * nightFactor, 0.0h, 10.0h);
                half3 biolum = biolumColor * biolumEnergy;
                half signalPhase = dot((half2)biolumLocalAupCoord.xz, half2(_NoirSignalFlickerScale, _NoirSignalFlickerScale * 1.37h)) + (half)SargassumWrappedVisualTime() * 2.1h + input.color.b * 3.3h;
                half signalWave = 1.0h - abs(frac(signalPhase * 0.15915494h) * 2.0h - 1.0h);
                half signalFlicker = smoothstep(0.18h, 0.92h, signalWave) * saturate(_NoirSignalFlickerStrength);
                half signalMask = saturate((1.0h - ao) * input.uv.y * (1.0h - isBubble));
                half3 noirSignal = biolumColor * (signalFlicker * signalMask);
                half specular = SargassumFastSpecularPower01(saturate(dot((half3)SargassumSafeNormalize3(lightDir + viewDirWS), normalWS)), lerp(8.0h, 36.0h, _Smoothness)) * _Smoothness * 0.22h;
                half cutEdge = smoothstep(0.02h, 0.24h, cutMask) * (1.0h - smoothstep(0.24h, 0.8h, cutMask)) * _InteractionEdgeBoost;

                half3 color = diffuse;
                color += _RimColor.rgb * rim;
                color += _SSSColor.rgb * (sss + bubbleGlow);
                color += biolum;
                color += noirSignal;
                color += specular;
                color += _CutEdgeColor.rgb * cutEdge;
                color = MixFog(color, input.fogFactor);
                color = all(isfinite((float3)color)) ? color : half3(0.0h, 0.0h, 0.0h);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DryColor;
                half4 _WetColor;
                half4 _BubbleColor;
                half4 _RimColor;
                half4 _SSSColor;
                half4 _CutEdgeColor;
                half4 _BiolumColor;
                half _AlphaClip;
                half _Smoothness;
                half _NormalInfluence;
                half _RimStrength;
                half _RimPower;
                half _SSSStrength;
                half _SSSPower;
                half _BubbleGlow;
                half _SwayAmplitude;
                half _SwayFrequency;
                half _SwaySpeed;
                half _PhaseScale;
                half _BeardSwingMultiplier;
                half _PulsationAmplitude;
                half _PulsationFrequency;
                half _PulsationSpeed;
                half _WoundCurlStrength;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumNightResponse;
                half _NoirSignalFlickerStrength;
                half _NoirSignalFlickerScale;
                float3 _InteractionPosition;
                half _InteractionRadius;
                half _InteractionCutStrength;
                half _InteractionEdgeBoost;
            CBUFFER_END

            float3 _LightDirection;
            float4 _HectonPropWashPosition;
            float4 _SargassumGlobalDriftOffset;
            float4 _GlobalBiolumClock;
            half _HectonPropWashForce;
            float4 _SargassumBuoyancySinkWorldRect;
            float _SargassumBuoyancySinkDepth;
            float4 _SargassumCutMaskWorldRect;
            float _SargassumCutMaskActive;

            TEXTURE2D(_SargassumBuoyancySinkRT);
            SAMPLER(sampler_SargassumBuoyancySinkRT);
            TEXTURE2D(_SargassumCutMaskRT);
            SAMPLER(sampler_SargassumCutMaskRT);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float SargassumFiniteOr(float value, float fallbackValue)
            {
                return isfinite(value) ? value : fallbackValue;
            }

            float2 SargassumFiniteOr(float2 value, float2 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float3 SargassumFiniteOr(float3 value, float3 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float4 SargassumFiniteOr(float4 value, float4 fallbackValue)
            {
                return all(isfinite(value)) ? value : fallbackValue;
            }

            float SargassumWrappedVisualTime()
            {
                float wrapped = SargassumFiniteOr(_GlobalBiolumClock.x, 0.0);
                return max(wrapped, 0.0);
            }

            half SargassumTrianglePulse01(float phase)
            {
                phase = SargassumFiniteOr(phase, 0.0);
                return (half)(1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0));
            }

            half SargassumTriangleSigned(float phase)
            {
                return SargassumTrianglePulse01(phase) * 2.0h - 1.0h;
            }

            float SargassumApproxMagnitude2(float2 value)
            {
                float2 axis = abs(value);
                float major = max(axis.x, axis.y);
                float minor = min(axis.x, axis.y);
                return major + minor * 0.375;
            }

            float SargassumApproxMagnitude3(float3 value)
            {
                float3 axis = abs(value);
                float major = max(max(axis.x, axis.y), axis.z);
                float minor = min(min(axis.x, axis.y), axis.z);
                float mid = axis.x + axis.y + axis.z - major - minor;
                return major + mid * 0.375 + minor * 0.125;
            }

            float2 SargassumSafeNormalize2(float2 value)
            {
                value = SargassumFiniteOr(value, float2(1.0, 0.0));
                float approxLen = SargassumApproxMagnitude2(value);
                return isfinite(approxLen) && approxLen > 0.0001 ? value * rcp(approxLen) : float2(1.0, 0.0);
            }

            half EvaluateLeafMask(half2 uv, half phase)
            {
                uv = saturate((half2)SargassumFiniteOr((float2)uv, float2(0.5, 0.5)));
                phase = (half)SargassumFiniteOr((float)phase, 0.0);
                half edge = abs(uv.x * 2.0h - 1.0h);
                half phaseScale = (half)SargassumFiniteOr((float)_PhaseScale, 0.0);
                half serration = SargassumTriangleSigned((uv.y * 18.0h + phase * phaseScale) * 6.28318h) * 0.08h;
                return saturate(1.0h - smoothstep(0.46h + serration, 0.94h, edge));
            }

            half EvaluateCutMask(float3 positionWS)
            {
                positionWS = SargassumFiniteOr(positionWS, float3(0.0, 0.0, 0.0));
                float3 interactionPosition = SargassumFiniteOr(_InteractionPosition, positionWS);
                float3 delta = positionWS - interactionPosition;
                float radius = max(abs(SargassumFiniteOr((float)_InteractionRadius, 0.0)), 0.0001);
                float invRadiusSq = rcp(max(radius * radius, 0.0001));
                half normalized = saturate(1.0h - (half)(dot(delta, delta) * invRadiusSq));
                return normalized * normalized * saturate((half)SargassumFiniteOr((float)_InteractionCutStrength, 0.0));
            }

            half EvaluateGlobalCutMask(float3 positionWS)
            {
                if (!isfinite(_SargassumCutMaskActive) || _SargassumCutMaskActive < 0.5 || !all(isfinite(positionWS)))
                    return 0.0h;

                float4 cutRect = SargassumFiniteOr(_SargassumCutMaskWorldRect, float4(0.0, 0.0, 0.0, 0.0));
                float2 uv = float2(
                    (positionWS.x - cutRect.x) * cutRect.z,
                    (positionWS.z - cutRect.y) * cutRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                return (half)SargassumFiniteOr(SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r, 0.0);
            }

            half EvaluateBuoyancySinkOffset(float2 worldXZ)
            {
                float sinkDepth = SargassumFiniteOr(_SargassumBuoyancySinkDepth, 0.0);
                if (sinkDepth <= 0.0001 || !all(isfinite(worldXZ)))
                    return 0.0h;

                float4 sinkRect = SargassumFiniteOr(_SargassumBuoyancySinkWorldRect, float4(0.0, 0.0, 0.0, 0.0));
                float2 driftOffset = SargassumFiniteOr(_SargassumGlobalDriftOffset.xz, float2(0.0, 0.0));
                float2 sampleXZ = worldXZ - driftOffset;
                float2 uv = float2(
                    (sampleXZ.x - sinkRect.x) * sinkRect.z,
                    (sampleXZ.y - sinkRect.y) * sinkRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                half sink01 = (half)SargassumFiniteOr(SAMPLE_TEXTURE2D_LOD(_SargassumBuoyancySinkRT, sampler_SargassumBuoyancySinkRT, uv, 0).r, 0.0);
                return sink01 * (half)sinkDepth;
            }

            float Hash21(float2 value)
            {
                value = SargassumFiniteOr(value, float2(0.0, 0.0));
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float EvaluateOrganicDensity(float2 worldXZ)
            {
                float2 safeWorldXZ = SargassumFiniteOr(worldXZ, float2(0.0, 0.0));
                float2 driftOffset = SargassumFiniteOr(_SargassumGlobalDriftOffset.xz, float2(0.0, 0.0));
                float2 sample = safeWorldXZ * 0.028 + driftOffset * 0.015;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.93 + 17.0));
                float wave = SargassumTrianglePulse01(sample.x * 1.2 + sample.y * 0.94 + SargassumWrappedVisualTime() * 0.1);
                return saturate(coarse * 0.46 + fine * 0.34 + wave * 0.20);
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 normalOS = SargassumFiniteOr(input.normalOS, float3(0.0, 1.0, 0.0));
                half4 vertexColor = saturate((half4)SargassumFiniteOr((float4)input.color, float4(1.0, 0.0, 0.0, 1.0)));
                float2 uv = saturate(SargassumFiniteOr(input.uv, float2(0.5, 0.5)));
                float timeSeconds = SargassumWrappedVisualTime();
                float3 positionOS = SargassumFiniteOr(input.positionOS.xyz, float3(0.0, 0.0, 0.0));
                half phase = vertexColor.b;
                half rigidity = saturate(vertexColor.a);
                // Same bladder-rigidity gate as the ForwardLit pass. It has to be duplicated here or the shadow
                // silhouette keeps pumping while the lit geometry stands still, which reads as a detached shadow.
                half isBubble = step(0.85h, saturate(vertexColor.g));
                half heightMask = saturate((half)uv.y) * (1.0h - isBubble);
                half swingScale = lerp((half)SargassumFiniteOr((float)_BeardSwingMultiplier, 0.68), 0.68h, rigidity);
                half sway = SargassumTriangleSigned(timeSeconds * SargassumFiniteOr((float)_SwaySpeed, 0.0) + phase * SargassumFiniteOr((float)_PhaseScale, 0.0) + positionOS.y * SargassumFiniteOr((float)_SwayFrequency, 0.0)) * (half)max(0.0, SargassumFiniteOr((float)_SwayAmplitude, 0.0)) * swingScale;
                positionOS.xz += normalOS.xz * (sway * heightMask);

                float3 driftOffset = SargassumFiniteOr(_SargassumGlobalDriftOffset.xyz, float3(0.0, 0.0, 0.0));
                float4 propWashPosition = SargassumFiniteOr(_HectonPropWashPosition, float4(0.0, 0.0, 0.0, 0.0));
                float3 positionWS_Interact = TransformObjectToWorld(positionOS) + driftOffset;
                float3 washDir = positionWS_Interact - propWashPosition.xyz;
                float washRadius = max(abs(propWashPosition.w), 0.001);
                float washDistSq = dot(washDir, washDir);
                float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                if (washDistSq > 0.0001)
                    positionOS.xyz += washDir * rcp(SargassumApproxMagnitude3(washDir) + 0.0001) * (washStrength * max(0.0, SargassumFiniteOr((float)_HectonPropWashForce, 0.0)) * 0.45h * heightMask);

                float3 positionWS_Pulse = TransformObjectToWorld(positionOS) + driftOffset;
                float organicDensity = EvaluateOrganicDensity(positionWS_Pulse.xz);
                float edgePulse = saturate(1.0 - abs(organicDensity * 2.0 - 1.0));
                float pulsePhase = timeSeconds * SargassumFiniteOr((float)_PulsationSpeed, 0.0) + phase * (SargassumFiniteOr((float)_PhaseScale, 0.0) * 0.41h) + organicDensity * (SargassumFiniteOr((float)_PulsationFrequency, 0.0) * 6.28318h);
                float pulse = SargassumTriangleSigned(pulsePhase) * max(0.0, SargassumFiniteOr((float)_PulsationAmplitude, 0.0)) * edgePulse * heightMask;
                float2 radialOS = SargassumSafeNormalize2(positionOS.xz + float2(0.001, 0.001));
                positionOS.xz += radialOS * pulse;
                positionOS.y += pulse * 0.12;

                float3 positionWS_Cut = TransformObjectToWorld(positionOS) + driftOffset;
                half cutWarp = EvaluateCutMask(positionWS_Cut);
                cutWarp = smoothstep(0.05h, 0.9h, cutWarp) * (1.0h - rigidity) * heightMask;
                half woundCurlStrength = saturate((half)SargassumFiniteOr((float)_WoundCurlStrength, 0.0));
                positionOS.xz -= radialOS * (woundCurlStrength * cutWarp);
                positionOS.y -= woundCurlStrength * cutWarp * 0.24h;

                float3 positionWS = TransformObjectToWorld(positionOS) + driftOffset;
                positionWS.y -= EvaluateBuoyancySinkOffset(positionWS.xz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = uv;
                output.positionWS = positionWS;
                output.color = vertexColor;

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif

                half sssMask = saturate(input.color.g);
                half isBubble = step(0.85h, sssMask);
                half alpha = lerp(EvaluateLeafMask(input.uv, input.color.b), 1.0h, isBubble);
                alpha *= (1.0h - max(EvaluateCutMask(input.positionWS), EvaluateGlobalCutMask(input.positionWS)));
                clip(alpha - saturate((half)SargassumFiniteOr((float)_AlphaClip, 0.36)));
                return 0;
            }
            ENDHLSL
        }
    }
}
