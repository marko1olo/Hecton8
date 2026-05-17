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
            #pragma target 3.5
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
            float4 _BiolumMasterPhase;
            float4 _BiolumIntensity;
            float4 _GlobalBiolumStates[16];
            float4 _GlobalBiolumParams;
            float4 _GlobalBiolumClock;
            float4 _GlobalBiolumAupOffset;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half SargassumTrianglePulse01(float phase)
            {
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
                float approxLen = SargassumApproxMagnitude2(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float2(1.0, 0.0);
            }

            float3 SargassumSafeNormalize3(float3 value)
            {
                float approxLen = SargassumApproxMagnitude3(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float3(0.0, 1.0, 0.0);
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
                half edge = abs(uv.x * 2.0h - 1.0h);
                half serration = SargassumTriangleSigned((uv.y * 18.0h + phase * _PhaseScale) * 6.28318h) * 0.08h;
                return saturate(1.0h - smoothstep(0.46h + serration, 0.94h, edge));
            }

            half EvaluateCutMask(float3 positionWS)
            {
                float3 delta = positionWS - _InteractionPosition;
                float radius = max((float)_InteractionRadius, 0.0001);
                float invRadiusSq = rcp(max(radius * radius, 0.0001));
                half normalized = saturate(1.0h - (half)(dot(delta, delta) * invRadiusSq));
                return normalized * normalized * saturate(_InteractionCutStrength);
            }

            half EvaluateGlobalCutMask(float3 positionWS)
            {
                if (_SargassumCutMaskActive < 0.5)
                    return 0.0h;

                float2 uv = float2(
                    (positionWS.x - _SargassumCutMaskWorldRect.x) * _SargassumCutMaskWorldRect.z,
                    (positionWS.z - _SargassumCutMaskWorldRect.y) * _SargassumCutMaskWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                return SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r;
            }

            half EvaluateBuoyancySinkOffset(float2 worldXZ)
            {
                if (_SargassumBuoyancySinkDepth <= 0.0001h)
                    return 0.0h;

                float2 sampleXZ = worldXZ - _SargassumGlobalDriftOffset.xz;
                float2 uv = float2(
                    (sampleXZ.x - _SargassumBuoyancySinkWorldRect.x) * _SargassumBuoyancySinkWorldRect.z,
                    (sampleXZ.y - _SargassumBuoyancySinkWorldRect.y) * _SargassumBuoyancySinkWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                half sink01 = SAMPLE_TEXTURE2D_LOD(_SargassumBuoyancySinkRT, sampler_SargassumBuoyancySinkRT, uv, 0).r;
                return sink01 * _SargassumBuoyancySinkDepth;
            }

            float Hash21(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float EvaluateOrganicDensity(float2 worldXZ)
            {
                float2 sample = worldXZ * 0.028 + _SargassumGlobalDriftOffset.xz * 0.015;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.93 + 17.0));
                float wave = SargassumTrianglePulse01(sample.x * 1.2 + sample.y * 0.94 + _Time.y * 0.1);
                return saturate(coarse * 0.46 + fine * 0.34 + wave * 0.20);
            }

            half4 ResolveSargassumGlobalBiolum(float3 positionWS)
            {
                if (!all(isfinite(positionWS)))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float4 safeParams = all(isfinite(_GlobalBiolumParams)) ? _GlobalBiolumParams : float4(0.0, 0.0, 0.0, 0.0);
                float4 safeAupOffset = all(isfinite(_GlobalBiolumAupOffset)) ? _GlobalBiolumAupOffset : float4(0.0, 0.0, 0.0, 0.0);
                float safeClock = isfinite(_GlobalBiolumClock.x) ? _GlobalBiolumClock.x : 0.0;
                int activeCount = min(max((int)floor(max(safeParams.x, 0.0)), 0), 16);
                if (activeCount <= 0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float selector = frac(abs(positionWS.x * 0.041 + positionWS.z * 0.033 + safeAupOffset.x * 0.0017 + safeAupOffset.z * 0.0013));
                int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
                float4 stateRaw = _GlobalBiolumStates[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                half strobe = saturate((half)max(safeParams.z, 0.0));
                half highTier = step(4.0h, (half)max(safeParams.y, 0.0));
                int secondaryIndex = stateIndex + 1;
                if (secondaryIndex >= activeCount)
                    secondaryIndex = 0;
                float4 secondaryStateRaw = _GlobalBiolumStates[secondaryIndex];
                float4 secondaryState = all(isfinite(secondaryStateRaw)) ? secondaryStateRaw : float4(0.0, 0.0, 0.0, 0.0);
                half overdrive = 0.0h;
                half godSpark = 0.0h;
                half godHaze = 0.0h;
                if (highTier > 0.5h)
                {
                    half overPulse = (half)(1.0 - abs(frac(safeClock * 0.07 + selector * 3.0) * 2.0 - 1.0));
                    half filament = (half)(1.0 - abs(frac(positionWS.x * 0.127 + positionWS.y * 0.083 + positionWS.z * 0.167 + safeClock * 0.21) * 2.0 - 1.0));
                    godHaze = smoothstep(0.42h, 0.92h, overPulse) * (0.50h + filament * 0.50h);
                    godSpark = smoothstep(0.80h, 0.98h, filament) * overPulse;
                    overdrive = saturate(overPulse * 0.35h + godSpark * 0.22h);
                }
                half3 color = lerp(saturate((half3)state.rgb), half3(1.0h, 1.0h, 1.0h), strobe);
                half intensity = clamp(max((half)max(state.w, 0.0), strobe * 10.0h), 0.0h, 10.0h);
                color = lerp(color, saturate((half3)secondaryState.rgb), overdrive);
                color = saturate(color + godHaze * half3(0.04h, 0.16h, 0.19h));
                intensity = clamp(intensity + (half)max(secondaryState.w, 0.0) * overdrive + godSpark * 0.5h + godHaze * 0.25h, 0.0h, 10.0h);
                return half4(color, intensity);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS.xyz;
                half phase = input.color.b;
                half rigidity = saturate(input.color.a);
                half heightMask = saturate(input.uv.y);
                half swingScale = lerp(_BeardSwingMultiplier, 0.68h, rigidity);
                half sway = SargassumTriangleSigned(_Time.y * _SwaySpeed + phase * _PhaseScale + positionOS.y * _SwayFrequency) * _SwayAmplitude * swingScale;
                positionOS.xz += input.normalOS.xz * (sway * heightMask);

                float3 positionWS_Interact = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                float3 washDir = positionWS_Interact - _HectonPropWashPosition.xyz;
                float washRadius = max(_HectonPropWashPosition.w, 0.001);
                float washDistSq = dot(washDir, washDir);
                float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                if (washDistSq > 0.0001)
                    positionOS.xyz += SargassumSafeNormalize3(washDir) * (washStrength * _HectonPropWashForce * 0.45h * heightMask);

                float3 positionWS_Pulse = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                float organicDensity = EvaluateOrganicDensity(positionWS_Pulse.xz);
                float edgePulse = saturate(1.0 - abs(organicDensity * 2.0 - 1.0));
                float pulsePhase = _Time.y * _PulsationSpeed + phase * (_PhaseScale * 0.41h) + organicDensity * (_PulsationFrequency * 6.28318h);
                float pulse = SargassumTriangleSigned(pulsePhase) * _PulsationAmplitude * edgePulse * heightMask;
                float2 radialOS = SargassumSafeNormalize2(positionOS.xz + float2(0.001, 0.001));
                positionOS.xz += radialOS * pulse;
                positionOS.y += pulse * 0.12;

                float3 positionWS_Cut = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                half cutWarp = EvaluateCutMask(positionWS_Cut);
                cutWarp = smoothstep(0.05h, 0.9h, cutWarp) * (1.0h - rigidity) * heightMask;
                positionOS.xz -= radialOS * (_WoundCurlStrength * cutWarp);
                positionOS.y -= _WoundCurlStrength * cutWarp * 0.24h;

                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                float3 positionWS = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                positionWS.y -= EvaluateBuoyancySinkOffset(positionWS.xz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = (half3)SargassumSafeNormalize3(normalInputs.normalWS);
                output.color = input.color;
                output.uv = input.uv;
                output.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionWS));
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
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
                clip(alpha - _AlphaClip);

                half immersionDarkening = lerp(0.72h, 1.0h, ao);
                half3 leafColor = lerp(_WetColor.rgb, _DryColor.rgb, saturate(input.uv.y + ao * 0.32h));
                half3 albedo = lerp(leafColor, _BubbleColor.rgb, isBubble);
                albedo *= immersionDarkening;

                half ambientOcclusion = lerp(0.48h, 1.0h, ao);
                half3 ambient = SampleSH(normalWS) * ambientOcclusion;
                half3 diffuse = albedo * (ambient + mainLight.color * (0.25h + NdotL * 0.75h));

                half rim = SargassumFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower) * _RimStrength;
                half sss = SargassumFastPower01(saturate(dot(-lightDir, viewDirWS)), _SSSPower) * _SSSStrength * sssMask;
                half bubbleGlow = isBubble * (_BubbleGlow + backLight * 0.55h);
                half biolumPhase = (half)(_BiolumMasterPhase.x * 6.28318 * max(_SargassumBiolumPhaseMultiplier, 0.001)) + input.positionWS.x * 0.085h + input.positionWS.z * 0.061h + input.uv.y * 4.2h + input.color.b * 3.7h;
                half biolumPulse = 1.0h + SargassumTriangleSigned(biolumPhase) * 0.18h;
                half timeBand = 0.75h + 0.25h * SargassumTriangleSigned(_HectonTimeOfDay01 * 6.28318h + input.color.b * 2.4h);
                half bubbleBiolumMask = saturate(isBubble * (0.68h + sssMask * 0.24h + bubbleGlow * 0.18h) * _BiolumMaskStrength);
                half nightFactor = saturate(_HectonNightFactor * _BiolumNightResponse);
                half oceanBiolumInfluence = saturate(_HectonOceanBiolumStrength);
                half3 biolumColor = lerp(_BiolumColor.rgb, _HectonOceanBiolumColor.rgb, oceanBiolumInfluence * 0.65h);
                half4 globalBiolumState = ResolveSargassumGlobalBiolum(input.positionWS);
                half globalBiolumMask = step(0.001h, globalBiolumState.w);
                biolumColor = lerp(biolumColor, globalBiolumState.rgb, globalBiolumMask);
                half masterBiolum = max(max((half)_BiolumIntensity.x, 0.0h), globalBiolumState.w);
                half biolumEnergy = clamp(_BiolumStrength * masterBiolum * (1.0h + oceanBiolumInfluence * 0.7h) * bubbleBiolumMask * biolumPulse * timeBand * nightFactor, 0.0h, 10.0h);
                half3 biolum = biolumColor * biolumEnergy;
                half signalPhase = dot(input.positionWS.xz, half2(_NoirSignalFlickerScale, _NoirSignalFlickerScale * 1.37h)) + _Time.y * 2.1h + input.color.b * 3.3h;
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

            half SargassumTrianglePulse01(float phase)
            {
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
                float approxLen = SargassumApproxMagnitude2(value);
                return approxLen > 0.0001 ? value * rcp(approxLen) : float2(1.0, 0.0);
            }

            half EvaluateLeafMask(half2 uv, half phase)
            {
                half edge = abs(uv.x * 2.0h - 1.0h);
                half serration = SargassumTriangleSigned((uv.y * 18.0h + phase * _PhaseScale) * 6.28318h) * 0.08h;
                return saturate(1.0h - smoothstep(0.46h + serration, 0.94h, edge));
            }

            half EvaluateCutMask(float3 positionWS)
            {
                float3 delta = positionWS - _InteractionPosition;
                float radius = max((float)_InteractionRadius, 0.0001);
                float invRadiusSq = rcp(max(radius * radius, 0.0001));
                half normalized = saturate(1.0h - (half)(dot(delta, delta) * invRadiusSq));
                return normalized * normalized * saturate(_InteractionCutStrength);
            }

            half EvaluateGlobalCutMask(float3 positionWS)
            {
                if (_SargassumCutMaskActive < 0.5)
                    return 0.0h;

                float2 uv = float2(
                    (positionWS.x - _SargassumCutMaskWorldRect.x) * _SargassumCutMaskWorldRect.z,
                    (positionWS.z - _SargassumCutMaskWorldRect.y) * _SargassumCutMaskWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                return SAMPLE_TEXTURE2D_LOD(_SargassumCutMaskRT, sampler_SargassumCutMaskRT, uv, 0).r;
            }

            half EvaluateBuoyancySinkOffset(float2 worldXZ)
            {
                if (_SargassumBuoyancySinkDepth <= 0.0001h)
                    return 0.0h;

                float2 sampleXZ = worldXZ - _SargassumGlobalDriftOffset.xz;
                float2 uv = float2(
                    (sampleXZ.x - _SargassumBuoyancySinkWorldRect.x) * _SargassumBuoyancySinkWorldRect.z,
                    (sampleXZ.y - _SargassumBuoyancySinkWorldRect.y) * _SargassumBuoyancySinkWorldRect.w);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0h;

                half sink01 = SAMPLE_TEXTURE2D_LOD(_SargassumBuoyancySinkRT, sampler_SargassumBuoyancySinkRT, uv, 0).r;
                return sink01 * _SargassumBuoyancySinkDepth;
            }

            float Hash21(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float EvaluateOrganicDensity(float2 worldXZ)
            {
                float2 sample = worldXZ * 0.028 + _SargassumGlobalDriftOffset.xz * 0.015;
                float coarse = Hash21(floor(sample));
                float fine = Hash21(floor(sample * 1.93 + 17.0));
                float wave = SargassumTrianglePulse01(sample.x * 1.2 + sample.y * 0.94 + _Time.y * 0.1);
                return saturate(coarse * 0.46 + fine * 0.34 + wave * 0.20);
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = input.positionOS.xyz;
                half phase = input.color.b;
                half rigidity = saturate(input.color.a);
                half heightMask = saturate(input.uv.y);
                half swingScale = lerp(_BeardSwingMultiplier, 0.68h, rigidity);
                half sway = SargassumTriangleSigned(_Time.y * _SwaySpeed + phase * _PhaseScale + positionOS.y * _SwayFrequency) * _SwayAmplitude * swingScale;
                positionOS.xz += input.normalOS.xz * (sway * heightMask);

                float3 positionWS_Interact = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                float3 washDir = positionWS_Interact - _HectonPropWashPosition.xyz;
                float washRadius = max(_HectonPropWashPosition.w, 0.001);
                float washDistSq = dot(washDir, washDir);
                float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                if (washDistSq > 0.0001)
                    positionOS.xyz += washDir * rcp(SargassumApproxMagnitude3(washDir) + 0.0001) * (washStrength * _HectonPropWashForce * 0.45h * heightMask);

                float3 positionWS_Pulse = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                float organicDensity = EvaluateOrganicDensity(positionWS_Pulse.xz);
                float edgePulse = saturate(1.0 - abs(organicDensity * 2.0 - 1.0));
                float pulsePhase = _Time.y * _PulsationSpeed + phase * (_PhaseScale * 0.41h) + organicDensity * (_PulsationFrequency * 6.28318h);
                float pulse = SargassumTriangleSigned(pulsePhase) * _PulsationAmplitude * edgePulse * heightMask;
                float2 radialOS = SargassumSafeNormalize2(positionOS.xz + float2(0.001, 0.001));
                positionOS.xz += radialOS * pulse;
                positionOS.y += pulse * 0.12;

                float3 positionWS_Cut = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                half cutWarp = EvaluateCutMask(positionWS_Cut);
                cutWarp = smoothstep(0.05h, 0.9h, cutWarp) * (1.0h - rigidity) * heightMask;
                positionOS.xz -= radialOS * (_WoundCurlStrength * cutWarp);
                positionOS.y -= _WoundCurlStrength * cutWarp * 0.24h;

                float3 positionWS = TransformObjectToWorld(positionOS) + _SargassumGlobalDriftOffset.xyz;
                positionWS.y -= EvaluateBuoyancySinkOffset(positionWS.xz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = input.uv;
                output.positionWS = positionWS;
                output.color = input.color;

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
                clip(alpha - _AlphaClip);
                return 0;
            }
            ENDHLSL
        }
    }
}
