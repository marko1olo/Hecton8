Shader "GPUInstancer/Hecton8/Flora/CoralMaster"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _MaskMap ("Mask Map", 2D) = "white" {}

        [Header(Master Grade PBR)]
        [Normal] _DetailNormalMap ("Detail Normal (Micro-Porosity)", 2D) = "bump" {}
        _DetailNormalStrength ("Detail Normal Strength", Range(0, 2)) = 0.55
        _MicroPorosityScale ("Micro-Porosity Scale", Range(0.1, 8)) = 3.2
        _DetailMap ("Detail Map (Overlay)", 2D) = "gray" {}

        [Header(Colors)]
        [MainColor] _BaseColor ("Base Color", Color) = (0.54, 0.32, 0.28, 1)
        _AccentColor ("Accent Color", Color) = (0.82, 0.58, 0.42, 1)
        _RimColor ("Rim Color", Color) = (0.24, 0.68, 0.72, 1)
        _SubsurfaceColor ("Subsurface Color", Color) = (0.94, 0.62, 0.48, 1)
        _BiolumColor ("Biolum Color", Color) = (0.26, 0.95, 0.84, 1)

        [Header(PBR and Lighting)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.34
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.46
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.8
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.28
        _SubsurfaceStrength ("Subsurface Strength", Range(0, 2)) = 0.36
        _VertexTintStrength ("Vertex Tint Strength", Range(0, 2)) = 0.74
        _AgeDarkening ("Age Darkening", Range(0, 1)) = 0.18
        _MoistureBoost ("Moisture Boost", Range(0, 1)) = 0.14
        _DetailStrength ("Detail Strength", Range(0, 2)) = 0.42
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.78
        _NormalScale ("Normal Scale", Range(0, 2)) = 0.75
        _TriplanarScale ("Triplanar Scale", Range(0.05, 4)) = 0.44
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 8)) = 4.8
        _CurvatureWetnessStrength ("Curvature Wetness Strength", Range(0, 2)) = 0.64
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.22
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4.8
        _HeightScale ("Height Scale", Range(0, 0.05)) = 0.03
        _ThicknessStrength ("Thickness Strength", Range(0, 2)) = 0.52
        _SpecularNoiseStrength ("Specular Noise Strength", Range(0, 2)) = 0.38
        _CavityStrength ("Cavity Strength", Range(0, 2)) = 0.58
        _CausticStrength ("Caustic Strength", Range(0, 2)) = 0.18
        _CausticScale ("Caustic Scale", Range(0.1, 8)) = 1.6
        _CausticSpeed ("Caustic Speed", Range(0, 4)) = 0.42
        _BiolumStrength ("Biolum Strength", Range(0, 4)) = 0
        _BiolumMaskStrength ("Biolum Mask Strength", Range(0, 2)) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
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
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "./../../../GPUInstancer/Shaders/Include/GPUInstancerInclude.cginc"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_ON DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                half4 _RimColor;
                half4 _SubsurfaceColor;
                half4 _BiolumColor;
                half _Smoothness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _SubsurfaceStrength;
                half _VertexTintStrength;
                half _AgeDarkening;
                half _MoistureBoost;
                half _DetailStrength;
                half _NormalStrength;
                half _NormalScale;
                half _DetailNormalStrength;
                half _MicroPorosityScale;
                half _TriplanarScale;
                half _TriplanarSharpness;
                half _CurvatureWetnessStrength;
                half _FresnelStrength;
                half _FresnelPower;
                half _HeightScale;
                half _ThicknessStrength;
                half _SpecularNoiseStrength;
                half _CavityStrength;
                half _CausticStrength;
                half _CausticScale;
                half _CausticSpeed;
                half _BiolumStrength;
                half _BiolumMaskStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DetailNormalMap);
            SAMPLER(sampler_DetailNormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            half4 _HectonOceanBiolumColor;
            half _HectonOceanBiolumStrength;
            half4 _HectonFloorBiolumColor;
            half _HectonFloorBiolumStrength;
            float4x4 _GlobalBiolumDearLieGroups;
            float4 _GlobalBiolumParams;
            StructuredBuffer<float4> _BiolumTouchRipples; // xyz runtime position, w effective radius
            float4 _BiolumTouchRippleParams; // x active count, yzw reserved
            float _HectonCelestialBiolumMultiplier;
            float4 _HectonFloraLifecycleParams; // x: growth, y: decay, z: bloom scale, w: reserved
            float4 _HectonPlayerRuntimePosition; // xyz: player/KCC position, w: interaction radius
            float4 _HectonPlayerFloraInteractionParams; // x: speed, y: force, z: scooter, w: active
            float4 _HectonFloraDamageReaction; // xyz: latest organic hit position, w: decaying impulse
            float _H8GlobalQualityWeight;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
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

            half3 ResolveFloraNormalCheap(half3 value)
            {
                return (half3)HectonCoreLitSafeNormalize((float3)value);
            }

            void ResolveFloraDominantAxisProjection(float3 positionWS, half3 normalWS, out float2 uv, out half dominantAxis)
            {
                half3 absNormal = max(abs(normalWS), half3(0.0001h, 0.0001h, 0.0001h));
                half maxAxis = max(absNormal.x, max(absNormal.y, absNormal.z));
                half tiling = max(_TriplanarScale, 0.001h);

                if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                {
                    uv = positionWS.zy * tiling;
                    dominantAxis = 0.0h;
                }
                else if (absNormal.z >= absNormal.y)
                {
                    uv = positionWS.xy * tiling;
                    dominantAxis = 2.0h;
                }
                else
                {
                    uv = positionWS.xz * tiling;
                    dominantAxis = 1.0h;
                }

                half edgeBand = saturate((1.0h - maxAxis) * max(_TriplanarSharpness, 1.0h));
                float stochastic = HectonCoreLitValueNoise2(floor(positionWS.xz * tiling * 31.0 + dominantAxis * 13.17)) * 2.0 - 1.0;
                uv += float2(stochastic, -stochastic) * edgeBand * 0.037;
            }

            half4 SampleFloraDominantAxis(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS)
            {
                float2 uv;
                half dominantAxis;
                ResolveFloraDominantAxisProjection(positionWS, normalWS, uv, dominantAxis);
                return SAMPLE_TEXTURE2D(tex, samp, uv);
            }

            half3 SampleFloraDominantAxisNormal(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half3 normalWS, half strength)
            {
                float2 uv;
                half dominantAxis;
                ResolveFloraDominantAxisProjection(positionWS, normalWS, uv, dominantAxis);
                half3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, uv), strength);

                if (dominantAxis < 0.5h)
                    return ResolveFloraNormalCheap(half3(0.0h, tangentNormal.y, tangentNormal.x));

                if (dominantAxis > 1.5h)
                    return ResolveFloraNormalCheap(half3(tangentNormal.x, tangentNormal.y, 0.0h));

                return ResolveFloraNormalCheap(half3(tangentNormal.x, 0.0h, tangentNormal.y));
            }

            half ComputeCurvatureWetness(half3 normalWS)
            {
                half3 derivative = abs(ddx(normalWS)) + abs(ddy(normalWS));
                return saturate(dot(derivative, half3(0.5h, 0.5h, 0.5h)) * _CurvatureWetnessStrength);
            }

            half CoralTrianglePulse01(float phase01)
            {
                return (half)(1.0 - abs(frac(phase01) * 2.0 - 1.0));
            }

            half HectonCoralGlobalQualityWeight()
            {
                return (half)(isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0);
            }

            half HectonCoralSmoothRange01(half low, half high, half value)
            {
                half t = saturate((value - low) * rcp(max(high - low, 0.0001h)));
                return t * t * (3.0h - 2.0h * t);
            }

            half3 ResolveCoralBiolumGroupTint(int stateIndex)
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

            half4 ResolveCoralGlobalBiolum(float3 localAupCoord)
            {
                if (!all(isfinite(localAupCoord)))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                int activeCount = min(max((int)_GlobalBiolumParams.x, 0), 4);
                if (activeCount <= 0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float selector = frac(abs(localAupCoord.x * 0.021 + localAupCoord.z * 0.059));
                int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
                const float invTwoPi = 0.159154943091895;
                float4 stateRaw = _GlobalBiolumDearLieGroups[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                float frequency = max(abs(state.y), 0.0025);
                float spatialPhase = dot(localAupCoord, float3(0.021, 0.013, 0.059)) + state.w;
                half primaryPulse = CoralTrianglePulse01(state.x * invTwoPi + spatialPhase * frequency);
                half strobe = saturate((half)_GlobalBiolumParams.z);
                half qualityCurve = saturate((half)_GlobalBiolumParams.y);
                qualityCurve = qualityCurve * qualityCurve * (3.0h - 2.0h * qualityCurve);
                int secondaryIndex = stateIndex + 1;
                if (secondaryIndex >= activeCount)
                    secondaryIndex = 0;
                float4 secondaryStateRaw = _GlobalBiolumDearLieGroups[secondaryIndex];
                float4 secondaryState = all(isfinite(secondaryStateRaw)) ? secondaryStateRaw : float4(0.0, 0.0, 0.0, 0.0);
                float secondaryFrequency = max(abs(secondaryState.y), 0.0025);
                float secondarySpatialPhase = dot(localAupCoord, float3(0.017, -0.011, 0.041)) + secondaryState.w;
                half secondaryPulse = CoralTrianglePulse01(secondaryState.x * invTwoPi + secondarySpatialPhase * secondaryFrequency);
                half overdrive = 0.0h;
                half godSpark = 0.0h;
                half godHaze = 0.0h;
                half overPulse = secondaryPulse;
                half filament = CoralTrianglePulse01(state.x * invTwoPi + dot(localAupCoord, float3(0.137, 0.113, 0.157)) * frequency + state.w);
                godHaze = smoothstep(0.43h, 0.91h, overPulse) * (0.54h + filament * 0.46h) * qualityCurve;
                godSpark = smoothstep(0.82h, 0.98h, filament) * overPulse * qualityCurve;
                overdrive = saturate(overPulse * 0.33h + godSpark * 0.20h) * qualityCurve;
                half3 color = lerp(ResolveCoralBiolumGroupTint(stateIndex), half3(1.0h, 1.0h, 1.0h), strobe);
                half amplitude = (half)max(state.z, 0.0) * (0.64h + primaryPulse * 0.36h);
                half secondaryAmplitude = (half)max(secondaryState.z, 0.0) * (0.64h + secondaryPulse * 0.36h);
                half intensity = clamp(max(amplitude, strobe * 10.0h), 0.0h, 10.0h);
                color = lerp(color, ResolveCoralBiolumGroupTint(secondaryIndex), overdrive);
                color = saturate(color + godHaze * half3(0.06h, 0.19h, 0.18h));
                intensity = clamp(intensity + secondaryAmplitude * overdrive + godSpark * 0.58h + godHaze * 0.30h, 0.0h, 10.0h);
                return half4(color, intensity);
            }

            float ResolveCoralFlashlightReaction(float3 positionWS)
            {
                if (_HectonFlashlightActive <= 0.5)
                    return 0.0;

                float lightEnergy = saturate(_HectonFlashlightColor.w * 0.12);
                if (lightEnergy <= 0.0001)
                    return 0.0;

                float lightRange = max(_HectonFlashlightPositionWS.w, 0.1);
                float3 toSampleWS = positionWS - _HectonFlashlightPositionWS.xyz;
                float sampleDistanceSq = dot(toSampleWS, toSampleWS);
                float rangeInvSq = rcp(max(lightRange * lightRange, 0.0001));
                float rangeMask = saturate(1.0 - sampleDistanceSq * rangeInvSq);
                float3 sampleDirectionWS = HectonCoreLitSafeNormalize(toSampleWS);
                float3 lightDirectionWS = HectonCoreLitSafeNormalize(_HectonFlashlightDirectionWS.xyz);
                float innerCos = _HectonFlashlightDirectionWS.w;
                float outerCos = _HectonFlashlightConeData.x;
                float coneMask = saturate((dot(lightDirectionWS, sampleDirectionWS) - outerCos) * rcp(max(innerCos - outerCos, 0.0001)));
                return saturate(coneMask * rangeMask * rangeMask * lightEnergy);
            }

            float ResolveCoralPlayerReaction(float3 positionWS)
            {
                float playerRadius = _HectonPlayerRuntimePosition.w;
                if (playerRadius <= 0.001 || _HectonPlayerFloraInteractionParams.w <= 0.001)
                    return 0.0;

                float3 delta = positionWS - _HectonPlayerRuntimePosition.xyz;
                float distSq = dot(delta, delta);
                float invRadiusSq = rcp(max(playerRadius * playerRadius, 0.0001));
                return saturate(1.0 - distSq * invRadiusSq) *
                    saturate(_HectonPlayerFloraInteractionParams.x * 0.18 + _HectonPlayerFloraInteractionParams.y);
            }

            float ResolveCoralDamageReaction(float3 positionWS)
            {
                float impulse = saturate(_HectonFloraDamageReaction.w);
                if (impulse <= 0.0001)
                    return 0.0;

                float3 delta = positionWS - _HectonFloraDamageReaction.xyz;
                float distSq = dot(delta, delta);
                float radiusSq = 16.0;
                return saturate(1.0 - distSq * rcp(radiusSq)) * impulse;
            }

            half ResolveBiolumTouchRipple(float3 positionWS)
            {
                if (!all(isfinite(positionWS)))
                    return 0.0h;

                float rippleCountRaw = isfinite(_BiolumTouchRippleParams.x) ? _BiolumTouchRippleParams.x : 0.0;
                int rippleCount = min(max((int)floor(rippleCountRaw + 0.0001), 0), 16);
                half qualityWeight = HectonCoralSmoothRange01(0.24h, 0.82h, HectonCoralGlobalQualityWeight());
                if (rippleCount <= 0 || qualityWeight <= 0.0001h)
                    return 0.0h;

                half rippleEnergy = 0.0h;
                [loop]
                for (int i = 0; i < rippleCount; i++)
                {
                    float4 ripple = _BiolumTouchRipples[i];
                    if (!all(isfinite(ripple)))
                        continue;

                    float radius = max(abs(ripple.w), 0.01);
                    float3 diff = positionWS - ripple.xyz;
                    float distSq = dot(diff, diff);
                    if (!isfinite(distSq))
                        continue;

                    float radiusSq = radius * radius;
                    float insideMask = step(distSq, radiusSq);
                    float invSqFlash = insideMask * saturate(radiusSq * rcp(max(distSq + radius * 0.12, 0.0001)));
                    rippleEnergy = max(rippleEnergy, (half)invSqFlash * qualityWeight);
                }

                return rippleEnergy;
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = HectonCoreLitSanitizePositionOS(input.positionOS.xyz);
                float3 reactionPositionWS = GetVertexPositionInputs(safePositionOS).positionWS;
                float reaction01 = saturate(
                    ResolveCoralFlashlightReaction(reactionPositionWS) +
                    ResolveCoralPlayerReaction(reactionPositionWS) +
                    ResolveCoralDamageReaction(reactionPositionWS));
                // Vertex colour channel contract, 3dmodel.md section 5 and
                // 3DMODEL_FLORA_CORAL.md section 2: R = sway amplitude, G = bioluminescence
                // mask/phase, B = baked ambient occlusion, A = family-specific mask, which is
                // harvest_yield_mask for coral per the generator manifest
                // (Assets/_Project/Art/Generated/Forge/Flora/MANIFEST_Flora_Coral_Branching_1712.json).
                // Retraction is a damage/harvest-eligibility response, so it belongs to A alone.
                // This used to add COLOR.r * 0.35, and R is the current-sway amplitude the contract
                // reserves for motion, so flexible frond tips retracted harder purely for being
                // flexible while rigid mineralised coral could not retract at all.
                float reactiveMask = saturate(input.color.a);
                float retract = reaction01 * reactiveMask;
                float3 safeNormalOS = all(isfinite(input.normalOS)) ? input.normalOS : float3(0.0, 1.0, 0.0);
                safeNormalOS = dot(safeNormalOS, safeNormalOS) > 0.000001 ? safeNormalOS : float3(0.0, 1.0, 0.0);
                safePositionOS -= safeNormalOS * (retract * 0.12);
                safePositionOS.y *= 1.0 - retract * 0.08;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(safePositionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(safeNormalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = (half3)HectonCoreLitSafeNormalize(normalInputs.normalWS);
                output.color = input.color;
                output.uv = input.uv;
                output.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.biolumLocalAupCoord = positionInputs.positionWS - TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif

                half3 baseNormalWS = ResolveFloraNormalCheap(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                // ORGANIC VERTEX COLOUR CONTRACT -- 3dmodel.md section 5 and
                // 3DMODEL_FLORA_CORAL.md section 2: R = water-current sway amplitude,
                // G = bioluminescence mask/phase, B = baked ambient occlusion, A = family-specific
                // mask (harvest_yield_mask for coral per the generator manifest). Tint variation,
                // wetness and age darkening are NOT channels in that contract, so none of them is
                // read out of R/G/B any more:
                //   tint    <- the biome hash below  (was saturate(input.color.r), the sway channel)
                //   wetness <- the authored mask map (was saturate(input.color.g), the biolum channel)
                //   age     <- the flora lifecycle decay owner, handled further down
                // Both replacements mirror the identical repair already landed in
                // Hecton_KelpMaster.shader (wetness :589, tint :618) rather than inventing a new
                // uniform per quantity. R and G are now genuinely off the vertex stream, which is
                // what earns the marker token below; it is a positive fail-closed gate, so it must
                // never be present unless this comment's claims are actually true.
                // H8_ORGANIC_VCOL_CONTRACT_OK
                //
                // TWIN MAINTENANCE: this shader exists twice. Hecton_CoralMaster_GPUI.shader is
                // GENERATED by GPU Instancer from Hecton_CoralMaster.shader -- see the
                // "Hecton8/Flora/CoralMaster" entry in
                // Assets/GPUInstancer/Resources/Settings/GPUInstancerShaderBindings.asset -- and a
                // GPUI regeneration OVERWRITES it, discarding any edit made only there. Every hunk
                // below is therefore kept byte-identical in both files as a stopgap, but the source of
                // truth is the non-GPUI file: change that one and REGENERATE rather than
                // hand-maintaining the pair. The twins have already drifted once (the generated file
                // uses LF where this one uses CRLF), which is what that drift looks like.
                //
                // DELIBERATE SECOND CHANGE, NOT A BUG: biomeAup/biomeHash used to be computed lower
                // down from samplePositionWS, i.e. AFTER the parallax offset had been applied to it.
                // tintMask is consumed by `accent` before that offset exists, so the hash is hoisted
                // here and now reads the un-offset interpolated world position. biomeTint consumes
                // the same hash, so its input moved too. The offset is sub-centimetre against a 32 m
                // hash cell (floor(xz * 0.03125)), so a cell assignment changes only for a vertex
                // sitting within the parallax distance of a cell boundary -- but it is a real change
                // and it is recorded here so it is not later mistaken for a defect.
                //
                // APPEARANCE: appearance-neutral IN THE MEAN ONLY, and only once the paired material
                // value lands (_VertexTintStrength 0.74 -> 0.059). R is contract-capped at 32/255 for
                // mineralized coral, so the old per-vertex tint spanned 4.5 points of the base->accent
                // lerp with a mean of 1.4, while biomeHash is uniform 0..1 and at 0.74 would mean
                // 17.8 -- 12.6x. Per-32 m-cell tint variation is new visual behaviour that has never
                // been rendered, and the old per-vertex root-to-tip tint gradient is gone.
                float3 biomeAup = input.positionWS + _TotalUniverseOffset.xyz;
                half biomeHash = (half)HectonCoreLitHash12(floor(biomeAup.xz * 0.03125));
                half tintMask = saturate(biomeHash * _VertexTintStrength);
                // Age darkening is NOT a channel in the vertex colour contract, so it is sourced
                // from the flora lifecycle decay owner (the same value _HectonFloraLifecycleParams.y
                // feeds to decay01 below) rather than read out of R/G/B. This used to read COLOR.b,
                // which is baked ambient occlusion, and at inverted polarity: B is low in crevices,
                // so exposed tissue darkened and cavities stayed bright. Left in place it cancels
                // roughly half the vertexAO contrast restored below, so the two reads had to move
                // together or the occlusion fix would have been half-defeated on arrival.
                half age = saturate((half)_HectonFloraLifecycleParams.y);
                float3 samplePositionWS = input.positionWS;
                half4 maskSample = SampleFloraDominantAxis(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), samplePositionWS, baseNormalWS);
                half parallaxQualityWeight = HectonCoralSmoothRange01(0.55h, 0.95h, HectonCoralGlobalQualityWeight());
                samplePositionWS -= viewDirWS * ((maskSample.b - 0.5h) * _HeightScale * parallaxQualityWeight);

                half3 baseTex = SampleFloraDominantAxis(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), samplePositionWS, baseNormalWS).rgb;
                // NOTE: baseNormalWS already declared at L229 - no redeclaration.
                half3 triplanarNormalWS = SampleFloraDominantAxisNormal(
                        TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                        samplePositionWS,
                        baseNormalWS,
                        _NormalStrength * _NormalScale);
                
                // Micro-Porosity Detail Normal
                half3 detailNormalWS = SampleFloraDominantAxisNormal(
                        TEXTURE2D_ARGS(_DetailNormalMap, sampler_DetailNormalMap),
                        samplePositionWS * _MicroPorosityScale,
                        baseNormalWS,
                        _DetailNormalStrength);

                half3 normalWS = ResolveFloraNormalCheap(baseNormalWS + triplanarNormalWS + detailNormalWS);
                float2 detailUv = samplePositionWS.xz * (_CausticScale * 0.06h)
                    + float2(_Time.y * _CausticSpeed, _Time.y * (_CausticSpeed * 0.61h));
                half detailSample = (half)HectonCoreLitValueNoise2(detailUv);

                Light mainLight = GetMainLight();
                half3 lightDir = (half3)mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDir));
                half wrapDiffuse = max(0.0h, dot(normalWS, lightDir) + 0.5h) * 0.6666667h;
                half rim = (half)HectonCoreLitFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);

                half floorZoneInfluence = saturate(_HectonFloorBiolumStrength);
                half oceanZoneInfluence = saturate(_HectonOceanBiolumStrength * 0.35h);
                half zoneBiolumStrength = saturate(floorZoneInfluence + oceanZoneInfluence);
                half3 volumeBiolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(samplePositionWS);

                half curvatureWetness = ComputeCurvatureWetness(normalWS);
                half cavity = saturate(1.0h - maskSample.r * _CavityStrength);
                // Wetness comes from the authored mask map plus surface curvature, with _MoistureBoost
                // scaling the mask contribution -- the same shape as Hecton_KelpMaster.shader :589.
                // The removed term was `moisture * _MoistureBoost`, where moisture was
                // saturate(input.color.g), i.e. the bioluminescence channel driving wetness. The
                // cavity and zone-biolum terms below are not vertex-colour derived and are unchanged.
                half wetness = saturate(maskSample.g * (1.0h + _MoistureBoost) + curvatureWetness + cavity * 0.18h + zoneBiolumStrength * 0.45h);
                half thickness = saturate(lerp(maskSample.b, maskSample.a, _ThicknessStrength));
                half glossNoise = lerp(1.0h, maskSample.g, _SpecularNoiseStrength);
                half roughness = saturate(lerp(0.7h, 0.2h, wetness));
                half causticMask = saturate(0.68h + detailSample * _CausticStrength + maskSample.a * 0.18h);

                half3 accent = lerp(_BaseColor.rgb, _AccentColor.rgb, saturate(maskSample.r + tintMask * 0.48h));
                half3 moistureTint = lerp(half3(1.0h, 1.0h, 1.0h), _AccentColor.rgb, wetness * 0.48h);
                half3 ageTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h - _AgeDarkening, 1.0h - _AgeDarkening, 1.0h - _AgeDarkening), age);
                half3 albedo = accent * baseTex * moistureTint * ageTint;
                // biomeAup/biomeHash are hoisted above, next to tintMask, which now consumes them.
                half3 biomeTint = lerp(half3(0.94h, 1.01h, 1.04h), half3(1.05h, 0.97h, 0.91h), biomeHash);
                half decay01 = saturate((half)_HectonFloraLifecycleParams.y);
                half luma = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
                albedo = lerp(albedo * biomeTint, half3(luma, luma, luma) * half3(0.72h, 0.68h, 0.57h), decay01);
                albedo *= lerp(1.0h, detailSample, _DetailStrength);
                albedo = lerp(albedo, albedo * 0.78h, cavity * 0.22h);

                half3 ambient = H8CustomLightProbeResolveAmbient(samplePositionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * (_AmbientStrength + wetness * 0.1h);
                // COLOR.b is baked ambient occlusion in every family contract the bible set defines
                // -- 3dmodel.md sections 4 and 5, 3DMODEL_FLORA_CORAL.md section 2,
                // 3DMODEL_GEOLOGY_ROCKS.md section 4, 3DMODEL_HARD_SURFACE_MODULES.md section 5.
                // This read used to be COLOR.a, the family-specific harvest_yield_mask, so the
                // forge's ray-traced occlusion was arriving as an age tint while the harvest mask
                // was arriving as occlusion. Nothing errored; the coral was simply lit wrong
                // everywhere. Matches the same repair already landed in Hecton_KelpMaster.shader.
                half vertexAO = lerp(0.72h, 1.0h, saturate(input.color.b));
                half3 diffuse = albedo * (ambient + mainLight.color * wrapDiffuse) * vertexAO;
                diffuse *= (1.0h - cavity * _CavityStrength * 0.5h);

                half3 subsurface = _SubsurfaceColor.rgb * (wrapDiffuse * _SubsurfaceStrength * causticMask);
                half3 rimLighting = _RimColor.rgb * (rim * _RimStrength);
                
                half specularSheen = NdotL * NdotL;
                half3 specular = specularSheen * (1.0h - roughness) * 0.22h * glossNoise * mainLight.color;
                half slimeSheen = specularSheen * specularSheen;
                half3 slimeSpecular = slimeSheen * wetness * 0.45h * mainLight.color;

                half3 biolum = volumeBiolum * (0.5h + thickness * 0.5h);
                [branch]
                if (_BiolumStrength > 0.0001h)
                {
                    float3 biolumLocalAupCoord = input.biolumLocalAupCoord;
                    half4 globalBiolumState = ResolveCoralGlobalBiolum(biolumLocalAupCoord);
                    half globalBiolumMask = step(0.001h, globalBiolumState.w);
                    half proceduralBiolumMask = (half)CoralTrianglePulse01(frac(biolumLocalAupCoord.x * 0.019 + biolumLocalAupCoord.z * 0.031 + input.uv.x * 0.47));
                    half biolumMask = saturate((cavity * 0.46h + thickness * 0.32h + proceduralBiolumMask * 0.22h) * _BiolumMaskStrength);
                    half celestialBiolum = max((half)_HectonCelestialBiolumMultiplier, 1.0h);
                    half masterBiolum = globalBiolumState.w;
                    half touchFlash = ResolveBiolumTouchRipple(samplePositionWS);
                    half authoredBiolumEnergy = _BiolumStrength * celestialBiolum * masterBiolum * (1.0h + zoneBiolumStrength * 0.76h) * biolumMask;
                    authoredBiolumEnergy *= (1.0h + touchFlash * 2.0h);
                    authoredBiolumEnergy = clamp(authoredBiolumEnergy, 0.0h, 10.0h);
                    [branch]
                    if (authoredBiolumEnergy > 0.0001h)
                    {
                        half3 zoneBiolumColor = lerp(_BiolumColor.rgb, _HectonFloorBiolumColor.rgb, floorZoneInfluence);
                        zoneBiolumColor = lerp(zoneBiolumColor, _HectonOceanBiolumColor.rgb, oceanZoneInfluence);
                        zoneBiolumColor = lerp(zoneBiolumColor, globalBiolumState.rgb, globalBiolumMask);
                        half3 authoredBiolum = zoneBiolumColor * authoredBiolumEnergy;
                        authoredBiolum *= HectonCoreLitResolveFlashlightPhotophobia(samplePositionWS);
                        biolum += authoredBiolum;
                    }
                }
                half fresnel = (half)HectonCoreLitFastPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower) * _FresnelStrength;

                half3 color = diffuse + subsurface + rimLighting + specular + slimeSpecular + biolum;
                color = lerp(color, unity_FogColor.rgb * 0.88h, saturate(fresnel * (0.5h + wetness * 0.5h)));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ShadowCaster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "./../../../GPUInstancer/Shaders/Include/GPUInstancerInclude.cginc"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 safeNormalOS = all(isfinite(input.normalOS)) ? input.normalOS : float3(0.0, 1.0, 0.0);
                safeNormalOS = dot(safeNormalOS, safeNormalOS) > 0.000001 ? safeNormalOS : float3(0.0, 1.0, 0.0);
                float3 positionWS = TransformObjectToWorld(safePositionOS);
                float3 normalWS = TransformObjectToWorldNormal(safeNormalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#include "./../../../GPUInstancer/Shaders/Include/GPUInstancerInclude.cginc"
#pragma instancing_options procedural:setupGPUI
#pragma multi_compile_instancing

            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 safePositionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                output.positionCS = TransformObjectToHClip(safePositionOS);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
