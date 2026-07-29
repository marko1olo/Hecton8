Shader "Hecton8/Flora/KelpMaster"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _DetailMap ("Detail Map", 2D) = "gray" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _MaskMap ("Mask Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.16, 0.46, 0.24, 1)
        _TipColor ("Tip Color", Color) = (0.34, 0.74, 0.42, 1)
        _RimColor ("Rim Color", Color) = (0.18, 0.52, 0.34, 1)
        _TransmissionColor ("Transmission Color", Color) = (0.26, 0.68, 0.34, 1)
        _BiolumColor ("Biolum Color", Color) = (0.20, 0.86, 0.92, 1)

        [Header(Master Grade SSS)]
        _SSSColor ("SSS Color", Color) = (0.45, 0.82, 0.38, 1)
        _SSSStrength ("SSS Strength", Range(0, 4)) = 1.2
        _SSSPower ("SSS Power", Range(1, 16)) = 4.0
        _SSSAmbient ("SSS Ambient", Range(0, 1)) = 0.15

        [Header(Interior Params)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.42
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.42
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.2
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.42
        _TransmissionStrength ("Transmission Strength", Range(0, 2)) = 0.55
        _EdgeTransmissionBoost ("Edge Transmission Boost", Range(0, 2)) = 0.42
        _VertexTintStrength ("Vertex Tint Strength", Range(0, 2)) = 0.8
        _AgeDarkening ("Age Darkening", Range(0, 1)) = 0.28
        _MoistureBoost ("Moisture Boost", Range(0, 1)) = 0.22
        _DetailStrength ("Detail Strength", Range(0, 2)) = 0.34
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.85
        _NormalScale ("Normal Scale", Range(0, 2)) = 0.75
        _TriplanarScale ("Triplanar Scale", Range(0.05, 4)) = 0.36
        _TriplanarSharpness ("Triplanar Sharpness", Range(1, 8)) = 4.2
        _CurvatureWetnessStrength ("Curvature Wetness Strength", Range(0, 2)) = 0.42
        _FresnelStrength ("Fresnel Strength", Range(0, 1)) = 0.26
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4.1
        _HeightScale ("Height Scale", Range(0, 0.05)) = 0.018
        _BladeCurveNormalStrength ("Blade Curve Normal Strength", Range(0, 1)) = 0.22
        _ThicknessStrength ("Thickness Strength", Range(0, 2)) = 0.65
        _SpecularNoiseStrength ("Specular Noise Strength", Range(0, 2)) = 0.45
        _MidribDarkening ("Midrib Darkening", Range(0, 1)) = 0.22
        _MidribGlossBoost ("Midrib Gloss Boost", Range(0, 1)) = 0.24
        _EdgeWearDarkening ("Edge Wear Darkening", Range(0, 1)) = 0.14
        _EdgeDetailBoost ("Edge Detail Boost", Range(0, 1)) = 0.18
        _CausticStrength ("Caustic Strength", Range(0, 2)) = 0.22
        _CausticScale ("Caustic Scale", Range(0.1, 8)) = 1.8
        _CausticSpeed ("Caustic Speed", Range(0, 4)) = 0.6
        _BiolumStrength ("Biolum Strength", Range(0, 4)) = 0
        _BiolumMaskStrength ("Biolum Mask Strength", Range(0, 2)) = 1
        _BiolumCurrentResponse ("Biolum Current Response", Range(0, 2)) = 0.35
        _SwayAmplitude ("Sway Amplitude", Range(0, 0.5)) = 0.08
        _SwayFrequency ("Sway Frequency", Range(0, 8)) = 1.8
        _SwaySpeed ("Sway Speed", Range(0, 4)) = 0.9
        _SwayPhaseScale ("Sway Phase Scale", Range(0, 4)) = 0.75

        [Header(Interaction)]
        _PropWashDisplacement ("Prop Wash Displacement", Range(0, 2)) = 0.85

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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CustomLightProbeGrid.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _RimColor;
                half4 _TransmissionColor;
                half4 _BiolumColor;
                half4 _SSSColor;
                half _SSSStrength;
                half _SSSPower;
                half _SSSAmbient;
                half _Smoothness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _TransmissionStrength;
                half _EdgeTransmissionBoost;
                half _VertexTintStrength;
                half _AgeDarkening;
                half _MoistureBoost;
                half _DetailStrength;
                half _NormalStrength;
                half _NormalScale;
                half _TriplanarScale;
                half _TriplanarSharpness;
                half _CurvatureWetnessStrength;
                half _FresnelStrength;
                half _FresnelPower;
                half _HeightScale;
                half _BladeCurveNormalStrength;
                half _ThicknessStrength;
                half _SpecularNoiseStrength;
                half _MidribDarkening;
                half _MidribGlossBoost;
                half _EdgeWearDarkening;
                half _EdgeDetailBoost;
                half _CausticStrength;
                half _CausticScale;
                half _CausticSpeed;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumCurrentResponse;
                half _SwayAmplitude;
                half _SwayFrequency;
                half _SwaySpeed;
                half _SwayPhaseScale;
                half _PropWashDisplacement;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            half4 _HectonOceanBiolumColor;
            half _HectonOceanBiolumStrength;
            half4 _HectonFloorBiolumColor;
            half _HectonFloorBiolumStrength;
            float4x4 _GlobalBiolumDearLieGroups;
            float4 _GlobalBiolumParams;

            float4 _HectonPropWashPosition; // xyz: position, w: radius
            half _HectonPropWashForce;
            float4 _HectonSubmarineWashSphere; // xyz: submarine center, w: runtime wash radius
            float4 SubmarinePropwash; // xyz: thruster stream direction, w: normalized force
            float4 _HectonPlayerRuntimePosition; // xyz: player/KCC position, w: interaction radius
            float4 _HectonPlayerFloraInteractionParams; // x: speed, y: force, z: scooter, w: active
            float4 _HectonFloraLifecycleParams; // x: growth, y: decay, z: bloom scale, w: reserved
            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float _HectonCelestialBiolumMultiplier;
            float _H8GlobalQualityWeight;

            float3 ResolvePropWashDominantDirection(float3 washDir)
            {
                float2 absXZ = abs(washDir.xz);
                return absXZ.x >= absXZ.y
                    ? float3(washDir.x < 0.0 ? -1.0 : 1.0, 0.0, 0.0)
                    : float3(0.0, 0.0, washDir.z < 0.0 ? -1.0 : 1.0);
            }

            float HectonKelpDearLieWave(float phase)
            {
                float t = frac(phase * 0.159154943 + 0.5);
                float wave = 1.0 - abs(t * 2.0 - 1.0) * 2.0;
                return wave * (1.5 - 0.5 * wave * wave);
            }

            half HectonKelpGlobalQualityWeight()
            {
                return (half)(isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0);
            }

            half HectonKelpSmoothRange01(half low, half high, half value)
            {
                half t = saturate((value - low) * rcp(max(high - low, 0.0001h)));
                return t * t * (3.0h - 2.0h * t);
            }

            float3 HectonKelpSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return isfinite(lengthSq) && lengthSq > 0.000001 ? value * rsqrt(lengthSq) : fallback;
            }

            float HectonKelpHash12(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            half ResolveKelpSineParabolaWave(float3 positionOS, float3 positionWS, half heightMask)
            {
                float tipParabola = (float)(heightMask * heightMask);
                float swaySpeed = isfinite((float)_SwaySpeed) ? max((float)_SwaySpeed, 0.001) : 0.001;
                float swayFrequency = isfinite((float)_SwayFrequency) ? max((float)_SwayFrequency, 0.001) : 0.001;
                float swayPhaseScale = isfinite((float)_SwayPhaseScale) ? (float)_SwayPhaseScale : 0.0;
                float speedNorm = swaySpeed * rcp(swayFrequency);
                float3 safeOffset = all(isfinite(_TotalUniverseOffset.xyz)) ? _TotalUniverseOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 aupPos = all(isfinite(positionWS)) ? positionWS + safeOffset : safeOffset;
                float aupHash = HectonKelpHash12(floor(aupPos.xz * 0.0625));
                // COLOR.r is the bible's SWAY AMPLITUDE (3dmodel.md section 5, 3DMODEL_FLORA_CORAL.md
                // section 2), not a phase seed; it is consumed as an amplitude in ApplyKelp*Biota.
                // Phase decorrelation is per-INSTANCE and comes from aupHash, which is the route
                // REND_Instanced_Flora_Physics.txt section III.F specifies. Seeding phase from a
                // monotonic root-to-tip gradient gave every plant an unauthored travelling wave
                // along its own length: neighbouring vertices on one blade must stay nearly in
                // phase and differ only in amplitude, because a blade bends as a beam.
                float phaseSeed = dot(aupPos.xz, float2(0.173, -0.131)) * swayPhaseScale + aupHash * 6.283185307;
                float safeTime = isfinite(_Time.y) ? _Time.y : 0.0;
                float basePhase = safeTime * speedNorm + phaseSeed + aupPos.y * swayFrequency * 0.071;
                float octave0 = HectonKelpDearLieWave(basePhase);
                float octave1 = HectonKelpDearLieWave(basePhase * 1.73 + phaseSeed * 0.37 + 1.9);
                float octave2 = HectonKelpDearLieWave(basePhase * 2.41 - aupPos.y * 0.29 + 3.7);
                return (half)((octave0 * 0.58 + octave1 * 0.29 + octave2 * 0.13) * tipParabola);
            }

            void ApplyKelpLivingBiota(inout float3 positionOS, float3 normalOS, half4 vertexColor, half2 uvMask)
            {
                half heightMask = isfinite((float)uvMask.y) ? saturate(uvMask.y) : 0.0h;
                float tipParabola = (float)(heightMask * heightMask);
                float3 positionWS = GetVertexPositionInputs(positionOS).positionWS;
                half swayWave = ResolveKelpSineParabolaWave(positionOS, positionWS, heightMask);
                half qualityWeight = HectonKelpGlobalQualityWeight();
                half motionWeight = lerp(0.42h, 1.0h, HectonKelpSmoothRange01(0.05h, 0.85h, qualityWeight));
                half interactionWeight = lerp(0.35h, 1.0h, HectonKelpSmoothRange01(0.18h, 0.72h, qualityWeight));
                half swayAmplitude = _SwayAmplitude * motionWeight;
                swayAmplitude *= lerp(0.58h, 1.0h, HectonKelpSmoothRange01(0.0h, 0.85h, qualityWeight));
                // Sway amplitude is COLOR.r: "Anchor/root = 0 ... Flexible frond tips = 192 to 255"
                // (3DMODEL_FLORA_CORAL.md section 2), with that section's per-family stiffness
                // exponent already baked in by the generator, so a stiff organism and a flexible
                // one no longer share one hardcoded curve. uvMask.y is the fallback for a
                // non-finite red channel -- TEXCOORD1, the generator's "UVMask" set, whose V is
                // the geodesic root-to-tip distance and is the same field as COLOR.r by
                // construction (Tools/Blender/generators/kelp.py UV_MASK_LAYER). heightMask below
                // still pins the holdfast, so a mesh whose red channel is a flat constant degrades
                // to that gradient instead of violating the "Root vertices sway as much as tips"
                // rejection gate in section 8.
                half swayMask = isfinite((float)vertexColor.r) ? saturate(vertexColor.r) : heightMask;
                swayAmplitude *= swayMask;

                float3 flowVector = _HectonVegetationCurrentVector.xyz + _GlobalOceanFlow.xyz * 0.35;
                float3 flowDirection = HectonKelpSafeNormalize(flowVector, float3(0.0, 0.0, 1.0));
                positionOS.xz += normalOS.xz * (swayWave * swayAmplitude * heightMask);
                positionOS.xz += flowDirection.xz * (swayWave * swayAmplitude * 0.35h * tipParabola);

                half propWashAmount = _HectonPropWashForce * _PropWashDisplacement * heightMask * interactionWeight;
                [branch]
                if (abs(propWashAmount) > 0.0001h && _HectonPropWashPosition.w > 0.0001h)
                {
                    float3 washDir = positionWS - _HectonPropWashPosition.xyz;
                    float washRadius = _HectonPropWashPosition.w;
                    float washDistSq = dot(washDir, washDir);
                    float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                    float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                    float3 washDirection = ResolvePropWashDominantDirection(washDir);
                    positionOS.xyz += washDirection * (washStrength * propWashAmount);
                }

                float submarineRadius = min(max(_HectonSubmarineWashSphere.w, 0.0), 10.0);
                [branch]
                if (submarineRadius > 0.001 && SubmarinePropwash.w > 0.001)
                {
                    float3 toPlant = positionWS - _HectonSubmarineWashSphere.xyz;
                    float submarineDistSq = dot(toPlant, toPlant);
                    float submarineInvRadiusSq = rcp(max(submarineRadius * submarineRadius, 0.0001));
                    float submarineInfluence = saturate(1.0 - submarineDistSq * submarineInvRadiusSq);
                    float3 radialFallback = HectonKelpSafeNormalize(toPlant, float3(0.0, 0.0, 1.0));
                    float3 streamBasis = HectonKelpSafeNormalize(SubmarinePropwash.xyz, radialFallback);
                    float streamCone = saturate(dot(radialFallback, streamBasis));
                    float3 streamDirection = HectonKelpSafeNormalize(streamBasis + radialFallback * 0.085, radialFallback);
                    positionOS.xyz += streamDirection * (submarineInfluence * streamCone * SubmarinePropwash.w * _PropWashDisplacement * 1.65 * tipParabola * interactionWeight);
                }

                float playerRadius = _HectonPlayerRuntimePosition.w;
                [branch]
                if (playerRadius > 0.001 && _HectonPlayerFloraInteractionParams.w > 0.001)
                {
                    float3 playerDelta = positionWS - _HectonPlayerRuntimePosition.xyz;
                    playerDelta.y = 0.0;
                    float playerDistSq = dot(playerDelta, playerDelta);
                    float playerInvRadiusSq = rcp(max(playerRadius * playerRadius, 0.0001));
                    float playerInfluence = saturate(1.0 - playerDistSq * playerInvRadiusSq) * saturate(_HectonPlayerFloraInteractionParams.w);
                    // COLOR.g is the bible's BIOLUMINESCENCE mask/phase and is consumed as such in
                    // the fragment stage; it is not a motion seed. Flutter phase decorrelates on
                    // object-space position alone, which is already per-vertex high frequency.
                    float safeTime = isfinite(_Time.y) ? _Time.y : 0.0;
                    float flutterPhase = safeTime * 6.1 + dot(positionOS.xz, float2(2.7, -3.1));
                    float flutter = HectonKelpDearLieWave(flutterPhase) * 0.045 * saturate(_HectonPlayerFloraInteractionParams.x * 0.25 + _HectonPlayerFloraInteractionParams.y);
                    positionOS.xz += normalOS.xz * (flutter * playerInfluence * tipParabola * interactionWeight);
                }

                float decay01 = saturate(_HectonFloraLifecycleParams.y);
                positionOS.y -= decay01 * 0.05 * tipParabola;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                // TEXCOORD3 carries the MASK set (TEXCOORD1 in), not UV0. UV0 stays a vertex
                // input only: no fragment consumer remains once the height and width masks read
                // the mask set, and every map here is sampled triplanar from world position, so
                // interpolating the atlas unwrap as well would be dead bandwidth.
                half2 uvMask : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half4 tangentWS : TEXCOORD6;
                half3 bitangentWS : TEXCOORD7;
                float3 biolumLocalAupCoord : TEXCOORD8;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 ResolveFloraNormalCheap(half3 value)
            {
                return (half3)HectonCoreLitSafeNormalize((float3)value);
            }

            half ResolveFloraDominantAxis(half3 normalWS, float3 positionWS)
            {
                half3 absNormal = abs(normalWS);
                half jitter = (half)HectonCoreLitHash12(floor(positionWS.xz * 19.17)) - 0.5h;
                half edgeNoise = jitter * 0.08h;
                absNormal.x += edgeNoise;
                absNormal.z -= edgeNoise;

                if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
                    return 1.0h;

                return absNormal.x >= absNormal.z ? 0.0h : 2.0h;
            }

            float2 ResolveFloraAxisUv(float3 positionWS, half axis)
            {
                if (axis < 0.5h)
                    return positionWS.zy * _TriplanarScale;

                if (axis < 1.5h)
                    return positionWS.xz * _TriplanarScale;

                return positionWS.xy * _TriplanarScale;
            }

            half4 SampleFloraTriplanar(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half axis)
            {
                return SAMPLE_TEXTURE2D(tex, samp, ResolveFloraAxisUv(positionWS, axis));
            }

            half3 SampleFloraTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 positionWS, half axis, half strength)
            {
                half3 sampledNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(tex, samp, ResolveFloraAxisUv(positionWS, axis)), strength);
                if (axis < 0.5h)
                    return half3(0.0h, sampledNormal.y, sampledNormal.x);

                if (axis < 1.5h)
                    return half3(sampledNormal.x, 0.0h, sampledNormal.y);

                return half3(sampledNormal.x, sampledNormal.y, 0.0h);
            }

            half ComputeCurvatureWetness(half3 normalWS)
            {
                half3 dx = ddx(normalWS);
                half3 dy = ddy(normalWS);
                half3 dxAbs = abs(dx);
                half3 dyAbs = abs(dy);
                half dxApprox = max(max(dxAbs.x, dxAbs.y), dxAbs.z);
                half dyApprox = max(max(dyAbs.x, dyAbs.y), dyAbs.z);
                return saturate((dxApprox + dyApprox) * (_CurvatureWetnessStrength * 1.35h));
            }

            half FastKelpPower01(half value, half exponent)
            {
                half v = saturate(value);
                half v2 = v * v;
                half v4 = v2 * v2;
                half v8 = v4 * v4;
                half low = lerp(v, v2, saturate(exponent - 1.0h));
                half high = lerp(v2, v8, saturate((exponent - 2.0h) * 0.16666667h));
                return lerp(low, high, step(2.0h, exponent));
            }

            half3 ResolveKelpBiolumGroupTint(int stateIndex)
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

            half4 ResolveKelpGlobalBiolum(float3 localAupCoord)
            {
                if (!all(isfinite(localAupCoord)))
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float4 safeParams = all(isfinite(_GlobalBiolumParams)) ? _GlobalBiolumParams : float4(0.0, 0.0, 0.0, 0.0);
                int activeCount = min(max((int)floor(max(safeParams.x, 0.0)), 0), 4);
                if (activeCount <= 0)
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);

                float selector = frac(abs(localAupCoord.x * 0.029 + localAupCoord.z * 0.047));
                int stateIndex = min((int)floor(selector * activeCount), activeCount - 1);
                float4 stateRaw = _GlobalBiolumDearLieGroups[stateIndex];
                float4 state = all(isfinite(stateRaw)) ? stateRaw : float4(0.0, 0.0, 0.0, 0.0);
                const float invTwoPi = 0.159154943091895;
                float frequency = max(abs(state.y), 0.0025);
                float spatialPhase = dot(localAupCoord, float3(0.029, 0.017, 0.047)) + state.w;
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
                float secondarySpatialPhase = dot(localAupCoord, float3(0.023, -0.013, 0.039)) + secondaryState.w;
                half secondaryPulse = (half)(1.0 - abs(frac(secondaryState.x * invTwoPi + secondarySpatialPhase * secondaryFrequency) * 2.0 - 1.0));
                half overdrive = 0.0h;
                half godSpark = 0.0h;
                half godHaze = 0.0h;
                half overPulse = secondaryPulse;
                half filament = (half)(1.0 - abs(frac(state.x * invTwoPi + dot(localAupCoord, float3(0.149, 0.071, 0.181)) * frequency + state.w) * 2.0 - 1.0));
                godHaze = smoothstep(0.42h, 0.92h, overPulse) * (0.52h + filament * 0.48h) * qualityCurve;
                godSpark = smoothstep(0.80h, 0.97h, filament) * overPulse * qualityCurve;
                overdrive = saturate(overPulse * 0.35h + godSpark * 0.22h) * qualityCurve;
                half3 color = lerp(ResolveKelpBiolumGroupTint(stateIndex), half3(1.0h, 1.0h, 1.0h), strobe);
                half amplitude = (half)max(state.z, 0.0) * (0.62h + primaryPulse * 0.38h);
                half secondaryAmplitude = (half)max(secondaryState.z, 0.0) * (0.62h + secondaryPulse * 0.38h);
                half intensity = clamp(max(amplitude, strobe * 10.0h), 0.0h, 10.0h);
                color = lerp(color, ResolveKelpBiolumGroupTint(secondaryIndex), overdrive);
                color = saturate(color + godHaze * half3(0.05h, 0.18h, 0.20h));
                intensity = clamp(intensity + secondaryAmplitude * overdrive + godSpark * 0.55h + godHaze * 0.28h, 0.0h, 10.0h);
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

                float3 positionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 normalOS = HectonKelpSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
                float4 tangentOS = all(isfinite(input.tangentOS)) ? input.tangentOS : float4(1.0, 0.0, 0.0, 1.0);
                ApplyKelpLivingBiota(positionOS, normalOS, input.color, input.uvMask);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normalOS, tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = (half3)HectonCoreLitSafeNormalize(normalInputs.normalWS);
                output.tangentWS = half4((half3)HectonCoreLitSafeNormalize(normalInputs.tangentWS), tangentOS.w);
                output.bitangentWS = (half3)HectonCoreLitSafeNormalize(normalInputs.bitangentWS);
                output.color = input.color;
                output.uvMask = input.uvMask;
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
                half3 tangentWS = ResolveFloraNormalCheap(input.tangentWS.xyz);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                // Vertex colour channel contract, 3dmodel.md section 5 and
                // 3DMODEL_FLORA_CORAL.md section 2: R = sway amplitude (consumed in the vertex
                // stage), G = bioluminescence mask/phase, B = baked ambient occlusion,
                // A = family-specific mask -- harvest_mask for kelp per the generator manifest.
                // Tint variation, wetness and age darkening are NOT channels in that contract, so
                // they are sourced from the biome hash, the mask map and the lifecycle decay
                // parameter below instead of being read out of R/G/B.
                half bakedBiolumMask = saturate(input.color.g);
                half bakedVertexAo = saturate(input.color.b);
                // UV SET CONTRACT. The height and width masks come from TEXCOORD1, the generator's
                // "UVMask" set (Tools/Blender/generators/kelp.py UV_MASK_LAYER): V = geodesic
                // distance from the holdfast, root 0 to farthest tip 1; U = 0 and 1 at the blade
                // margins, 0.5 on the midrib. They do NOT come from UV0. Every map in this pass is
                // sampled triplanar from world position, so UV0 is never a texture coordinate
                // here; UV0 is the atlas-packed conformal unwrap that the 3dmodel.md section 6
                // padding/density/island gates measure, which makes each island's V an atlas band
                // rather than a root-to-tip parameter. 3dmodel.md section 3 assigns TexCoord1 to
                // "atlas remap, or packed baked masks"; section 6 states that triplanar assignment
                // "still requires UV0 or object-space coordinates for decals and masks".
                // SOURCE STATUS 2026-07-29. This supersedes an earlier note claiming "the forge FBX
                // that does carry UVMask is not imported under Assets/". THAT IS NO LONGER TRUE, and
                // it was the sentence telling people not to bind this shader at all. Re-measured:
                // four kelp packages ARE imported under Assets/_Project/Art/Generated/Forge/Flora/ --
                // MESH_Flora_Kelp_s4021_q050, s4021_q100, s4023_q100 and s4025_q100 -- each with its
                // .meta, so each has a stable asset GUID and can be referenced.
                // MANIFEST_Flora_Kelp_s4021_q100.json declares maskUv.layer "UVMask" at
                // maskUv.texcoordIndex 1, with acrossAttributeSurvived and geodesicAttributeSurvived
                // both true. That is exactly the U-across-the-blade / V-geodesic parameterisation
                // this pass wants, on the TEXCOORD1 that the Attributes struct above binds. The same
                // package's vertex colour is real as well: swayMin 0.0, swayMax 1.0, swayUniform
                // false, biolumWritten true, aoWritten true, alphaMeaning "harvest_mask".
                //
                // STILL VALID, DO NOT DELETE: the BAKED mesh assets are a different source and are
                // still stale. 472 kelp-named .asset meshes exist across
                // Assets/_Project/Prefabs/Nature/Flora/Baked (165 of them),
                // Assets/_Project/Prefabs/GeneratedEcosystem (7) and
                // Art/Generated/Flora/BioForge/Shallows/Kelp (300). Those were measured serializing
                // TexCoord1 with dimension 0, so every mask below reads 0 on them. The in-Unity
                // writer that produces them has since been fixed and its output proven in the sway
                // gate, but an already-baked asset does not change until a re-bake runs. So the
                // current condition is: binding this shader to a FORGE FBX is supported; binding it
                // to a pre-re-bake .asset is not, and the two cannot be told apart by looking at the
                // shader.
                //
                // RE-CHECK rather than trusting this comment, because it will go stale again: for a
                // forge mesh read maskUv.texcoordIndex in its package manifest; for a baked .asset
                // confirm TexCoord1 dimension is 2 and not 0. A mask below reading uniformly 0 across
                // an entire mesh means the channel is ABSENT, not flat. Note that absent UV1 does not
                // degrade gracefully: heightMask 0 also zeroes the sway wave itself, because
                // ResolveKelpSineParabolaWave multiplies by tipParabola = heightMask^2, so it presents
                // as a completely motionless plant rather than an under-animated one.
                half heightMask = saturate(input.uvMask.y);
                half widthMask = saturate(input.uvMask.x);
                half centerDistance = abs(widthMask - 0.5h) * 2.0h;
                half midribMask = saturate(1.0h - centerDistance * centerDistance * 6.0h);
                half edgeMask = saturate((centerDistance - 0.24h) / 0.76h);
                half triplanarAxis = ResolveFloraDominantAxis(baseNormalWS, input.positionWS);
                half flatNoirLod = HectonCoreLitResolveFlatNoirLod(input.positionWS);

                float3 samplePositionWS = input.positionWS;
                half4 maskSample = SampleFloraTriplanar(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), samplePositionWS, triplanarAxis);
                half parallaxQualityWeight = HectonKelpSmoothRange01(0.55h, 0.95h, HectonKelpGlobalQualityWeight());
                samplePositionWS -= viewDirWS * ((maskSample.b - 0.5h) * _HeightScale * parallaxQualityWeight);

                half3 baseTex = SampleFloraTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), samplePositionWS, triplanarAxis).rgb;
                half3 triplanarNormalWS = half3(0.0h, 0.0h, 0.0h);
                [branch]
                if (flatNoirLod < 0.5h)
                {
                    triplanarNormalWS = SampleFloraTriplanarNormal(
                        TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                        samplePositionWS,
                        triplanarAxis,
                        _NormalStrength * _NormalScale);
                }
                float2 detailUv = samplePositionWS.xz * (_CausticScale * 0.08h)
                    + float2(_Time.y * _CausticSpeed, _Time.y * (_CausticSpeed * 0.73h));
                half detailSample = (half)HectonCoreLitValueNoise2(detailUv);

                half curveSigned = (widthMask - 0.5h) * 2.0h;
                half3 normalWS = ResolveFloraNormalCheap(
                    baseNormalWS
                    + triplanarNormalWS
                    + tangentWS * (curveSigned * edgeMask * _BladeCurveNormalStrength)
                    + baseNormalWS * (midribMask * (_BladeCurveNormalStrength * 0.18h)));

                Light mainLight = GetMainLight();
                half3 lightDir = (half3)mainLight.direction;
                half NdotL = saturate(dot(normalWS, lightDir));
                half wrapDiffuse = saturate(dot(normalWS, lightDir) * 0.5h + 0.5h);
                half backLight = saturate(dot(-normalWS, lightDir));
                half rim = FastKelpPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _RimPower);
                half curvatureWetness = ComputeCurvatureWetness(normalWS);
                // Wetness comes from the authored mask map plus surface curvature. _MoistureBoost
                // now scales the map's own wetness channel; it used to scale COLOR.g, which the
                // channel contract reserves for bioluminescence.
                half wetness = saturate(maskSample.g * (1.0h + _MoistureBoost) + curvatureWetness);
                half detailMask = lerp(1.0h, detailSample, saturate(_DetailStrength + edgeMask * _EdgeDetailBoost));
                half causticMask = saturate(0.65h + detailSample * _CausticStrength + maskSample.a * 0.2h + edgeMask * 0.08h);
                half thicknessMask = saturate(lerp(heightMask, maskSample.r, _ThicknessStrength) + edgeMask * _EdgeTransmissionBoost * 0.18h);
                half glossNoise = lerp(1.0h, maskSample.g, _SpecularNoiseStrength);
                half glossMask = saturate(glossNoise + wetness * 0.28h + midribMask * _MidribGlossBoost - edgeMask * (_EdgeWearDarkening * 0.22h));
                half roughness = saturate(lerp(0.7h, 0.2h, wetness));
                detailMask = lerp(detailMask, 1.0h, flatNoirLod);
                causticMask = lerp(causticMask, 0.72h, flatNoirLod);
                glossMask *= 1.0h - flatNoirLod;
                roughness = lerp(roughness, 1.0h, flatNoirLod);
                half oceanZoneInfluence = saturate(_HectonOceanBiolumStrength);
                half floorZoneInfluence = saturate(_HectonFloorBiolumStrength * 0.45h);
                half zoneBiolumStrength = saturate(oceanZoneInfluence + floorZoneInfluence);
                half3 volumeBiolum = (half3)HectonCoreLitSampleBiolumVolumeRadiance(samplePositionWS);

                // Hoisted above the albedo build because the per-instance tint variation now reads
                // this hash. It used to read COLOR.r, which the channel contract reserves for sway.
                float3 biomeAup = samplePositionWS + _TotalUniverseOffset.xyz;
                half biomeHash = (half)HectonCoreLitHash12(floor(biomeAup.xz * 0.03125));
                half decay01 = saturate((half)_HectonFloraLifecycleParams.y);

                half3 gradient = lerp(_BaseColor.rgb, _TipColor.rgb, heightMask);
                half3 moistureTint = lerp(half3(1.0h, 1.0h, 1.0h), _TipColor.rgb, wetness * 0.5h);
                // Age darkening follows the lifecycle decay parameter that already drives the
                // desaturation below. It used to read COLOR.b, so the ray-traced ambient occlusion
                // the offline forge bakes into B was being spent as an age tint.
                half3 ageTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h - _AgeDarkening, 1.0h - _AgeDarkening, 1.0h - _AgeDarkening), decay01);
                half3 albedo = gradient * baseTex * moistureTint * ageTint * detailMask;
                half tintMask = saturate(biomeHash * _VertexTintStrength);
                albedo = lerp(albedo, albedo * half3(1.12h, 1.08h, 0.92h), tintMask + maskSample.b * 0.08h);
                half3 biomeTint = lerp(half3(0.94h, 1.01h, 1.04h), half3(1.05h, 0.97h, 0.91h), biomeHash);
                half luma = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
                albedo = lerp(albedo * biomeTint, half3(luma, luma, luma) * half3(0.72h, 0.68h, 0.57h), decay01);
                albedo *= (1.0h - midribMask * _MidribDarkening);
                albedo *= lerp(1.0h, 1.0h - _EdgeWearDarkening, edgeMask);

                half3 ambient = H8CustomLightProbeResolveAmbient(samplePositionWS, normalWS, half3(0.015h, 0.025h, 0.035h)) * (_AmbientStrength + wetness * 0.12h);
                // COLOR.b is baked ambient occlusion in every family contract the bible set defines
                // -- 3dmodel.md sections 4 and 5, 3DMODEL_FLORA_CORAL.md section 2,
                // 3DMODEL_GEOLOGY_ROCKS.md section 4, 3DMODEL_HARD_SURFACE_MODULES.md section 5.
                // This read used to be COLOR.a, which is the family-specific harvest mask, so the
                // occlusion was arriving as an age tint while the harvest mask was arriving as
                // occlusion. Nothing errored; the kelp was simply lit wrong everywhere.
                half vertexAO = lerp(0.72h, 1.0h, bakedVertexAo);
                half3 diffuse = albedo * (ambient + mainLight.color * wrapDiffuse) * vertexAO;

                half sssWrap = max(0.0h, dot(normalWS, lightDir) + 0.5h) * 0.6666667h;
                half3 sssLighting = _SSSColor.rgb * ((sssWrap * _SSSStrength * causticMask) + (_SSSAmbient * ambient));

                half3 transmission = _TransmissionColor.rgb * (backLight * _TransmissionStrength * thicknessMask * causticMask);
                half3 rimLighting = _RimColor.rgb * (rim * _RimStrength);
                half specularSheen = NdotL * NdotL;
                half specular = specularSheen * specularSheen * (1.0h - roughness) * 0.18h * glossMask;
                transmission *= 1.0h - flatNoirLod;
                rimLighting *= 1.0h - flatNoirLod;
                specular *= 1.0h - flatNoirLod;
                half3 biolum = volumeBiolum * (0.45h + thicknessMask * 0.55h);
                [branch]
                if (_BiolumStrength > 0.0001h)
                {
                    float3 biolumLocalAupCoord = input.biolumLocalAupCoord;
                    half4 globalBiolumState = ResolveKelpGlobalBiolum(biolumLocalAupCoord);
                    half globalBiolumMask = step(0.001h, globalBiolumState.w);
                    half proceduralBiolumMask = (half)HectonCoreLitTrianglePulse01(biolumLocalAupCoord.x * 0.043h + biolumLocalAupCoord.z * 0.061h + input.uvMask.y * 1.7h);
                    // COLOR.g is the authored bioluminescence mask/phase, and section 2 fixes
                    // "Non-emissive tissue = 0", so it GATES emission rather than merely biasing
                    // it. The edge/thickness/pulse terms now shape that baked mask instead of
                    // standing in for it -- previously the baked channel was ignored entirely and
                    // the glow landed on whatever geometry happened to be thin or near an edge.
                    half biolumMask = saturate(bakedBiolumMask * (0.55h + edgeMask * 0.42h + thicknessMask * 0.38h + proceduralBiolumMask * 0.20h) * _BiolumMaskStrength);
                    half celestialBiolum = max((half)_HectonCelestialBiolumMultiplier, 1.0h);
                    half masterBiolum = globalBiolumState.w;
                    half authoredBiolumEnergy = _BiolumStrength * celestialBiolum * masterBiolum * (1.0h + zoneBiolumStrength * 0.72h) * biolumMask;
                    authoredBiolumEnergy = clamp(authoredBiolumEnergy, 0.0h, 10.0h);
                    [branch]
                    if (authoredBiolumEnergy > 0.0001h)
                    {
                        half3 zoneBiolumColor = lerp(_BiolumColor.rgb, _HectonOceanBiolumColor.rgb, oceanZoneInfluence);
                        zoneBiolumColor = lerp(zoneBiolumColor, _HectonFloorBiolumColor.rgb, floorZoneInfluence);
                        zoneBiolumColor = lerp(zoneBiolumColor, globalBiolumState.rgb, globalBiolumMask);
                        half3 authoredBiolum = zoneBiolumColor * authoredBiolumEnergy;
                        authoredBiolum *= HectonCoreLitResolveFlashlightPhotophobia(samplePositionWS);
                        biolum += authoredBiolum;
                    }
                }
                half fresnel = FastKelpPower01(1.0h - saturate(dot(normalWS, viewDirWS)), _FresnelPower) * _FresnelStrength;

                sssLighting *= 1.0h - flatNoirLod * 0.75h;
                half3 color = diffuse + transmission + rimLighting + specular + biolum + sssLighting;
                color = lerp(color, unity_FogColor.rgb * 0.85h, saturate(fresnel * (0.55h + wetness * 0.45h)));
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ShadowCaster Pass (with vertex sway)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _RimColor;
                half4 _TransmissionColor;
                half4 _BiolumColor;
                half4 _SSSColor;
                half _SSSStrength;
                half _SSSPower;
                half _SSSAmbient;
                half _Smoothness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _TransmissionStrength;
                half _EdgeTransmissionBoost;
                half _VertexTintStrength;
                half _AgeDarkening;
                half _MoistureBoost;
                half _DetailStrength;
                half _NormalStrength;
                half _NormalScale;
                half _TriplanarScale;
                half _TriplanarSharpness;
                half _CurvatureWetnessStrength;
                half _FresnelStrength;
                half _FresnelPower;
                half _HeightScale;
                half _BladeCurveNormalStrength;
                half _ThicknessStrength;
                half _SpecularNoiseStrength;
                half _MidribDarkening;
                half _MidribGlossBoost;
                half _EdgeWearDarkening;
                half _EdgeDetailBoost;
                half _CausticStrength;
                half _CausticScale;
                half _CausticSpeed;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumCurrentResponse;
                half _SwayAmplitude;
                half _SwayFrequency;
                half _SwaySpeed;
                half _SwayPhaseScale;
                half _PropWashDisplacement;
            CBUFFER_END

            float3 _LightDirection;
            float4 _HectonPropWashPosition;
            half _HectonPropWashForce;
            float4 _HectonSubmarineWashSphere;
            float4 SubmarinePropwash;
            float4 _HectonFloraLifecycleParams;
            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float4 _TotalUniverseOffset;
            float _H8GlobalQualityWeight;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half FastKelpTriangleSigned(half phase)
            {
                return (1.0h - abs(frac(phase * 0.15915494h + 0.25h) * 2.0h - 1.0h)) * 2.0h - 1.0h;
            }

            float3 ResolvePropWashDominantDirection(float3 washDir)
            {
                float2 absXZ = abs(washDir.xz);
                return absXZ.x >= absXZ.y
                    ? float3(washDir.x < 0.0 ? -1.0 : 1.0, 0.0, 0.0)
                    : float3(0.0, 0.0, washDir.z < 0.0 ? -1.0 : 1.0);
            }

            float HectonKelpDearLieWave(float phase)
            {
                float t = frac(phase * 0.159154943 + 0.5);
                float wave = 1.0 - abs(t * 2.0 - 1.0) * 2.0;
                return wave * (1.5 - 0.5 * wave * wave);
            }

            half HectonKelpGlobalQualityWeight()
            {
                return (half)(isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0);
            }

            half HectonKelpSmoothRange01(half low, half high, half value)
            {
                half t = saturate((value - low) * rcp(max(high - low, 0.0001h)));
                return t * t * (3.0h - 2.0h * t);
            }

            float3 HectonKelpSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return isfinite(lengthSq) && lengthSq > 0.000001 ? value * rsqrt(lengthSq) : fallback;
            }

            float HectonKelpHash12(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            half ResolveKelpSineParabolaWave(float3 positionOS, float3 positionWS, half heightMask)
            {
                float tipParabola = (float)(heightMask * heightMask);
                float swaySpeed = isfinite((float)_SwaySpeed) ? max((float)_SwaySpeed, 0.001) : 0.001;
                float swayFrequency = isfinite((float)_SwayFrequency) ? max((float)_SwayFrequency, 0.001) : 0.001;
                float swayPhaseScale = isfinite((float)_SwayPhaseScale) ? (float)_SwayPhaseScale : 0.0;
                float speedNorm = swaySpeed * rcp(swayFrequency);
                float3 safeOffset = all(isfinite(_TotalUniverseOffset.xyz)) ? _TotalUniverseOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 aupPos = all(isfinite(positionWS)) ? positionWS + safeOffset : safeOffset;
                float aupHash = HectonKelpHash12(floor(aupPos.xz * 0.0625));
                // COLOR.r is the bible's SWAY AMPLITUDE (3dmodel.md section 5, 3DMODEL_FLORA_CORAL.md
                // section 2), not a phase seed; it is consumed as an amplitude in ApplyKelp*Biota.
                // Phase decorrelation is per-INSTANCE and comes from aupHash, which is the route
                // REND_Instanced_Flora_Physics.txt section III.F specifies. Seeding phase from a
                // monotonic root-to-tip gradient gave every plant an unauthored travelling wave
                // along its own length: neighbouring vertices on one blade must stay nearly in
                // phase and differ only in amplitude, because a blade bends as a beam.
                float phaseSeed = dot(aupPos.xz, float2(0.173, -0.131)) * swayPhaseScale + aupHash * 6.283185307;
                float safeTime = isfinite(_Time.y) ? _Time.y : 0.0;
                float basePhase = safeTime * speedNorm + phaseSeed + aupPos.y * swayFrequency * 0.071;
                float octave0 = HectonKelpDearLieWave(basePhase);
                float octave1 = HectonKelpDearLieWave(basePhase * 1.73 + phaseSeed * 0.37 + 1.9);
                float octave2 = HectonKelpDearLieWave(basePhase * 2.41 - aupPos.y * 0.29 + 3.7);
                return (half)((octave0 * 0.58 + octave1 * 0.29 + octave2 * 0.13) * tipParabola);
            }

            void ApplyKelpShadowBiota(inout float3 positionOS, float3 normalOS, half4 vertexColor, half2 uvMask)
            {
                half heightMask = isfinite((float)uvMask.y) ? saturate(uvMask.y) : 0.0h;
                float tipParabola = (float)(heightMask * heightMask);
                float3 positionWS = TransformObjectToWorld(positionOS);
                half swayWave = ResolveKelpSineParabolaWave(positionOS, positionWS, heightMask);
                half qualityWeight = HectonKelpGlobalQualityWeight();
                half motionWeight = lerp(0.42h, 1.0h, HectonKelpSmoothRange01(0.05h, 0.85h, qualityWeight));
                half interactionWeight = lerp(0.35h, 1.0h, HectonKelpSmoothRange01(0.18h, 0.72h, qualityWeight));
                half swayAmplitude = _SwayAmplitude * motionWeight;
                swayAmplitude *= lerp(0.58h, 1.0h, HectonKelpSmoothRange01(0.0h, 0.85h, qualityWeight));
                // Sway amplitude is COLOR.r: "Anchor/root = 0 ... Flexible frond tips = 192 to 255"
                // (3DMODEL_FLORA_CORAL.md section 2), with that section's per-family stiffness
                // exponent already baked in by the generator, so a stiff organism and a flexible
                // one no longer share one hardcoded curve. uvMask.y is the fallback for a
                // non-finite red channel -- TEXCOORD1, the generator's "UVMask" set, whose V is
                // the geodesic root-to-tip distance and is the same field as COLOR.r by
                // construction (Tools/Blender/generators/kelp.py UV_MASK_LAYER). heightMask below
                // still pins the holdfast, so a mesh whose red channel is a flat constant degrades
                // to that gradient instead of violating the "Root vertices sway as much as tips"
                // rejection gate in section 8.
                half swayMask = isfinite((float)vertexColor.r) ? saturate(vertexColor.r) : heightMask;
                swayAmplitude *= swayMask;

                float3 flowDirection = HectonKelpSafeNormalize(_HectonVegetationCurrentVector.xyz + _GlobalOceanFlow.xyz * 0.35, float3(0.0, 0.0, 1.0));
                positionOS.xz += normalOS.xz * (swayWave * swayAmplitude * heightMask);
                positionOS.xz += flowDirection.xz * (swayWave * swayAmplitude * 0.35h * tipParabola);

                half propWashAmount = _HectonPropWashForce * _PropWashDisplacement * heightMask * interactionWeight;
                [branch]
                if (abs(propWashAmount) > 0.0001h && _HectonPropWashPosition.w > 0.0001h)
                {
                    float3 washDir = positionWS - _HectonPropWashPosition.xyz;
                    float washRadius = _HectonPropWashPosition.w;
                    float washDistSq = dot(washDir, washDir);
                    float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                    float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                    float3 washDirection = ResolvePropWashDominantDirection(washDir);
                    positionOS.xyz += washDirection * (washStrength * propWashAmount);
                }

                float submarineRadius = min(max(_HectonSubmarineWashSphere.w, 0.0), 10.0);
                [branch]
                if (submarineRadius > 0.001 && SubmarinePropwash.w > 0.001)
                {
                    float3 toPlant = positionWS - _HectonSubmarineWashSphere.xyz;
                    float submarineDistSq = dot(toPlant, toPlant);
                    float submarineInvRadiusSq = rcp(max(submarineRadius * submarineRadius, 0.0001));
                    float submarineInfluence = saturate(1.0 - submarineDistSq * submarineInvRadiusSq);
                    float3 radialFallback = HectonKelpSafeNormalize(toPlant, float3(0.0, 0.0, 1.0));
                    float3 streamBasis = HectonKelpSafeNormalize(SubmarinePropwash.xyz, radialFallback);
                    float streamCone = saturate(dot(radialFallback, streamBasis));
                    float3 streamDirection = HectonKelpSafeNormalize(streamBasis + radialFallback * 0.085, radialFallback);
                    positionOS.xyz += streamDirection * (submarineInfluence * streamCone * SubmarinePropwash.w * _PropWashDisplacement * 1.65 * tipParabola * interactionWeight);
                }

                positionOS.y -= saturate(_HectonFloraLifecycleParams.y) * 0.05 * tipParabola;
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 normalOS = HectonKelpSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
                ApplyKelpShadowBiota(positionOS, normalOS, input.color, input.uvMask);

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
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

        // DepthOnly Pass (with vertex sway)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _RimColor;
                half4 _TransmissionColor;
                half4 _BiolumColor;
                half4 _SSSColor;
                half _SSSStrength;
                half _SSSPower;
                half _SSSAmbient;
                half _Smoothness;
                half _AmbientStrength;
                half _RimPower;
                half _RimStrength;
                half _TransmissionStrength;
                half _EdgeTransmissionBoost;
                half _VertexTintStrength;
                half _AgeDarkening;
                half _MoistureBoost;
                half _DetailStrength;
                half _NormalStrength;
                half _NormalScale;
                half _TriplanarScale;
                half _TriplanarSharpness;
                half _CurvatureWetnessStrength;
                half _FresnelStrength;
                half _FresnelPower;
                half _HeightScale;
                half _BladeCurveNormalStrength;
                half _ThicknessStrength;
                half _SpecularNoiseStrength;
                half _MidribDarkening;
                half _MidribGlossBoost;
                half _EdgeWearDarkening;
                half _EdgeDetailBoost;
                half _CausticStrength;
                half _CausticScale;
                half _CausticSpeed;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumCurrentResponse;
                half _SwayAmplitude;
                half _SwayFrequency;
                half _SwaySpeed;
                half _SwayPhaseScale;
                half _PropWashDisplacement;
            CBUFFER_END

            float4 _HectonPropWashPosition;
            half _HectonPropWashForce;
            float4 _HectonSubmarineWashSphere;
            float4 SubmarinePropwash;
            float4 _HectonFloraLifecycleParams;
            float4 _HectonVegetationCurrentVector;
            float4 _GlobalOceanFlow;
            float4 _TotalUniverseOffset;
            float _H8GlobalQualityWeight;

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half FastKelpTriangleSigned(half phase)
            {
                return (1.0h - abs(frac(phase * 0.15915494h + 0.25h) * 2.0h - 1.0h)) * 2.0h - 1.0h;
            }

            float3 ResolvePropWashDominantDirection(float3 washDir)
            {
                float2 absXZ = abs(washDir.xz);
                return absXZ.x >= absXZ.y
                    ? float3(washDir.x < 0.0 ? -1.0 : 1.0, 0.0, 0.0)
                    : float3(0.0, 0.0, washDir.z < 0.0 ? -1.0 : 1.0);
            }

            float HectonKelpDearLieWave(float phase)
            {
                float t = frac(phase * 0.159154943 + 0.5);
                float wave = 1.0 - abs(t * 2.0 - 1.0) * 2.0;
                return wave * (1.5 - 0.5 * wave * wave);
            }

            half HectonKelpGlobalQualityWeight()
            {
                return (half)(isfinite(_H8GlobalQualityWeight) ? saturate(_H8GlobalQualityWeight) : 0.0);
            }

            half HectonKelpSmoothRange01(half low, half high, half value)
            {
                half t = saturate((value - low) * rcp(max(high - low, 0.0001h)));
                return t * t * (3.0h - 2.0h * t);
            }

            float3 HectonKelpSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return isfinite(lengthSq) && lengthSq > 0.000001 ? value * rsqrt(lengthSq) : fallback;
            }

            float HectonKelpHash12(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            half ResolveKelpSineParabolaWave(float3 positionOS, float3 positionWS, half heightMask)
            {
                float tipParabola = (float)(heightMask * heightMask);
                float swaySpeed = isfinite((float)_SwaySpeed) ? max((float)_SwaySpeed, 0.001) : 0.001;
                float swayFrequency = isfinite((float)_SwayFrequency) ? max((float)_SwayFrequency, 0.001) : 0.001;
                float swayPhaseScale = isfinite((float)_SwayPhaseScale) ? (float)_SwayPhaseScale : 0.0;
                float speedNorm = swaySpeed * rcp(swayFrequency);
                float3 safeOffset = all(isfinite(_TotalUniverseOffset.xyz)) ? _TotalUniverseOffset.xyz : float3(0.0, 0.0, 0.0);
                float3 aupPos = all(isfinite(positionWS)) ? positionWS + safeOffset : safeOffset;
                float aupHash = HectonKelpHash12(floor(aupPos.xz * 0.0625));
                // COLOR.r is the bible's SWAY AMPLITUDE (3dmodel.md section 5, 3DMODEL_FLORA_CORAL.md
                // section 2), not a phase seed; it is consumed as an amplitude in ApplyKelp*Biota.
                // Phase decorrelation is per-INSTANCE and comes from aupHash, which is the route
                // REND_Instanced_Flora_Physics.txt section III.F specifies. Seeding phase from a
                // monotonic root-to-tip gradient gave every plant an unauthored travelling wave
                // along its own length: neighbouring vertices on one blade must stay nearly in
                // phase and differ only in amplitude, because a blade bends as a beam.
                float phaseSeed = dot(aupPos.xz, float2(0.173, -0.131)) * swayPhaseScale + aupHash * 6.283185307;
                float safeTime = isfinite(_Time.y) ? _Time.y : 0.0;
                float basePhase = safeTime * speedNorm + phaseSeed + aupPos.y * swayFrequency * 0.071;
                float octave0 = HectonKelpDearLieWave(basePhase);
                float octave1 = HectonKelpDearLieWave(basePhase * 1.73 + phaseSeed * 0.37 + 1.9);
                float octave2 = HectonKelpDearLieWave(basePhase * 2.41 - aupPos.y * 0.29 + 3.7);
                return (half)((octave0 * 0.58 + octave1 * 0.29 + octave2 * 0.13) * tipParabola);
            }

            void ApplyKelpDepthBiota(inout float3 positionOS, float3 normalOS, half4 vertexColor, half2 uvMask)
            {
                half heightMask = isfinite((float)uvMask.y) ? saturate(uvMask.y) : 0.0h;
                float tipParabola = (float)(heightMask * heightMask);
                float3 positionWS = TransformObjectToWorld(positionOS);
                half swayWave = ResolveKelpSineParabolaWave(positionOS, positionWS, heightMask);
                half qualityWeight = HectonKelpGlobalQualityWeight();
                half motionWeight = lerp(0.42h, 1.0h, HectonKelpSmoothRange01(0.05h, 0.85h, qualityWeight));
                half interactionWeight = lerp(0.35h, 1.0h, HectonKelpSmoothRange01(0.18h, 0.72h, qualityWeight));
                half swayAmplitude = _SwayAmplitude * motionWeight;
                swayAmplitude *= lerp(0.58h, 1.0h, HectonKelpSmoothRange01(0.0h, 0.85h, qualityWeight));
                // Sway amplitude is COLOR.r: "Anchor/root = 0 ... Flexible frond tips = 192 to 255"
                // (3DMODEL_FLORA_CORAL.md section 2), with that section's per-family stiffness
                // exponent already baked in by the generator, so a stiff organism and a flexible
                // one no longer share one hardcoded curve. uvMask.y is the fallback for a
                // non-finite red channel -- TEXCOORD1, the generator's "UVMask" set, whose V is
                // the geodesic root-to-tip distance and is the same field as COLOR.r by
                // construction (Tools/Blender/generators/kelp.py UV_MASK_LAYER). heightMask below
                // still pins the holdfast, so a mesh whose red channel is a flat constant degrades
                // to that gradient instead of violating the "Root vertices sway as much as tips"
                // rejection gate in section 8.
                half swayMask = isfinite((float)vertexColor.r) ? saturate(vertexColor.r) : heightMask;
                swayAmplitude *= swayMask;

                float3 flowDirection = HectonKelpSafeNormalize(_HectonVegetationCurrentVector.xyz + _GlobalOceanFlow.xyz * 0.35, float3(0.0, 0.0, 1.0));
                positionOS.xz += normalOS.xz * (swayWave * swayAmplitude * heightMask);
                positionOS.xz += flowDirection.xz * (swayWave * swayAmplitude * 0.35h * tipParabola);

                half propWashAmount = _HectonPropWashForce * _PropWashDisplacement * heightMask * interactionWeight;
                [branch]
                if (abs(propWashAmount) > 0.0001h && _HectonPropWashPosition.w > 0.0001h)
                {
                    float3 washDir = positionWS - _HectonPropWashPosition.xyz;
                    float washRadius = _HectonPropWashPosition.w;
                    float washDistSq = dot(washDir, washDir);
                    float washInvRadiusSq = rcp(max(washRadius * washRadius, 0.0001));
                    float washStrength = saturate(1.0 - washDistSq * washInvRadiusSq);
                    float3 washDirection = ResolvePropWashDominantDirection(washDir);
                    positionOS.xyz += washDirection * (washStrength * propWashAmount);
                }

                float submarineRadius = min(max(_HectonSubmarineWashSphere.w, 0.0), 10.0);
                [branch]
                if (submarineRadius > 0.001 && SubmarinePropwash.w > 0.001)
                {
                    float3 toPlant = positionWS - _HectonSubmarineWashSphere.xyz;
                    float submarineDistSq = dot(toPlant, toPlant);
                    float submarineInvRadiusSq = rcp(max(submarineRadius * submarineRadius, 0.0001));
                    float submarineInfluence = saturate(1.0 - submarineDistSq * submarineInvRadiusSq);
                    float3 radialFallback = HectonKelpSafeNormalize(toPlant, float3(0.0, 0.0, 1.0));
                    float3 streamBasis = HectonKelpSafeNormalize(SubmarinePropwash.xyz, radialFallback);
                    float streamCone = saturate(dot(radialFallback, streamBasis));
                    float3 streamDirection = HectonKelpSafeNormalize(streamBasis + radialFallback * 0.085, radialFallback);
                    positionOS.xyz += streamDirection * (submarineInfluence * streamCone * SubmarinePropwash.w * _PropWashDisplacement * 1.65 * tipParabola * interactionWeight);
                }

                positionOS.y -= saturate(_HectonFloraLifecycleParams.y) * 0.05 * tipParabola;
            }

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = all(isfinite(input.positionOS.xyz)) ? input.positionOS.xyz : float3(0.0, 0.0, 0.0);
                float3 normalOS = HectonKelpSafeNormalize(input.normalOS, float3(0.0, 1.0, 0.0));
                ApplyKelpDepthBiota(positionOS, normalOS, input.color, input.uvMask);

                output.positionCS = TransformObjectToHClip(positionOS);
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
