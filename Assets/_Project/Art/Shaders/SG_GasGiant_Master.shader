Shader "HECTON/Celestial/SG_GasGiant_Master"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Cloud Albedo", 2D) = "gray" {}
        _DetailTex ("Detail Clouds", 2D) = "gray" {}
        _EmissionTex ("Storm Emission", 2D) = "black" {}

        [Header(Atmosphere Colors HDR)]
        [HDR] _AtmosColorInner ("Atmos Inner", Color) = (0.4, 0.3, 0.7, 1)
        [HDR] _AtmosColorOuter ("Atmos Outer", Color) = (0.5, 0.4, 0.9, 1)

        [Header(Rotation)]
        _GlobalRotation ("Global Rotation (set from C#)", Float) = 0.0
        _EquatorialSpeed ("Equatorial Speed", Float) = 0.02

        [Header(Detail Layer)]
        _DetailTiling ("Detail Tiling", Vector) = (3, 3, 0, 0)
        _DetailSpeedMult ("Detail Speed Multiplier", Range(1.0, 2.0)) = 1.4
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.3

        [Header(Lighting)]
        _BacklitIntensity ("Backlit Intensity", Range(0, 1)) = 0.08
        _TerminatorWidth ("Terminator Width", Range(0.01, 0.5)) = 0.15
        _TerminatorTintColor ("Terminator Tint", Color) = (1.0, 0.45, 0.12, 1)
        _TerminatorTintStrength ("Terminator Tint Strength", Range(0, 2)) = 0.6

        [Header(Fresnel)]
        _InnerPower ("Fresnel Inner Power", Range(0.5, 8)) = 2.0
        _OuterPower ("Fresnel Outer Power", Range(1, 12)) = 5.0
        _FresnelSunBias ("Fresnel Sun-Side Bias", Range(-0.5, 0.5)) = -0.1

        [Header(Eclipse)]
        _SunBacklitFactor ("Sun Backlit Factor (C#)", Range(0, 1)) = 0.0
        [HDR] _EclipseRimColor ("Eclipse Rim Color", Color) = (0.8, 0.85, 1.0, 1)
        _EclipseRimIntensity ("Eclipse Rim Intensity", Range(0, 10)) = 5.0
        _EclipseRimPower ("Eclipse Rim Power", Range(1, 16)) = 6.0

        [Header(Storms)]
        _StormEmission ("Storm Intensity", Range(0, 5)) = 1.0
        _StormTiling ("Storm Tiling", Vector) = (2, 2, 0, 0)
        _StormSpeed ("Storm Drift Speed", Float) = 0.01

        [Header(Phase Data from C Sharp)]
        _PlanetPhase ("Planet Phase", Range(-1, 1)) = 0
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

        LOD 200

        // ═══════════════════════════════════════════
        // PASS 0: FORWARD (Main visual pass)
        // ═══════════════════════════════════════════
        Pass
        {
            Name "GasGiantForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex GasGiantVert
            #pragma fragment GasGiantFrag
            #pragma target 3.5
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ─────────────────────────────────
            // TEXTURES
            // ─────────────────────────────────
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_DetailTex);      SAMPLER(sampler_DetailTex);
            TEXTURE2D(_EmissionTex);    SAMPLER(sampler_EmissionTex);

            // ─────────────────────────────────────────────────────────
            // CBUFFER (SRP Batcher compatible)
            //
            // PRECISION POLICY:
            //   _GlobalRotation: MUST be float (32-bit).
            //   This is a fractional accumulator set from C# every frame.
            //   Using half (16-bit, 10-bit mantissa) would cause visible
            //   UV jitter after ~10 minutes on low-end GPUs (MX series,
            //   Mali, Adreno) due to mantissa exhaustion at large values.
            //
            //   _EquatorialSpeed: float (participates in _GlobalRotation math).
            //   _DetailSpeedMult: float (participates in _GlobalRotation math).
            //   _StormSpeed: float (participates in _GlobalRotation math).
            //
            //   All other parameters: half (visual params, no precision risk).
            //
            // REMOVED PROPERTIES (v2 cleanup):
            //   _PolarMultiplier — latitude-based speed was producing
            //     visible seams at UV poles and adding complexity for
            //     minimal visual benefit. Banding comes from textures.
            //   _VerticalWiggleFreq / _VerticalWiggleAmp — removed for
            //     the same reason. Vertical turbulence is better achieved
            //     through texture authoring.
            // ─────────────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailTex_ST;
                float4 _EmissionTex_ST;

                half4  _AtmosColorInner;
                half4  _AtmosColorOuter;

                float  _GlobalRotation;          // float — precision-critical
                float  _EquatorialSpeed;         // float — used in rotation math
                float  _DetailSpeedMult;         // float — used in rotation math

                float4 _DetailTiling;

                half   _DetailStrength;

                half   _BacklitIntensity;
                half   _TerminatorWidth;
                half4  _TerminatorTintColor;
                half   _TerminatorTintStrength;

                half   _InnerPower;
                half   _OuterPower;
                half   _FresnelSunBias;

                half   _SunBacklitFactor;
                half4  _EclipseRimColor;
                half   _EclipseRimIntensity;
                half   _EclipseRimPower;

                half   _StormEmission;
                float4 _StormTiling;
                float  _StormSpeed;              // float — used in rotation math

                half   _PlanetPhase;
            CBUFFER_END

            // ─────────────────────────────────
            // GLOBALS (set from C# HectonAtmosphereManager)
            // ─────────────────────────────────
            float4 _SunDirection;

            // ─────────────────────────────────
            // STRUCTS
            // ─────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half3  viewDirWS   : TEXCOORD2;
                float3 positionWS  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─────────────────────────────────────────────────────────
            // CONSTANT-SPEED ROTATION (precision-safe)
            //
            // _GlobalRotation is a fractional accumulator from C#:
            //   _GlobalRotation += speed * Time.deltaTime;
            //   _GlobalRotation = frac(_GlobalRotation);
            //
            // C# keeps _GlobalRotation in [0,1) via frac(), so the GPU
            // never sees large float values — zero precision loss.
            //
            // speedMultiplier: allows detail/storm layers to scroll
            // at different rates relative to the base cloud deck.
            //
            // ALL math uses float (32-bit). half is PROHIBITED in
            // rotation/UV chains to prevent jitter on MX350/Mali GPUs.
            //
            // SIMPLIFIED vs v1:
            //   - No latitude/polar speed logic (removed _PolarMultiplier).
            //   - No vertical wiggle (removed _VerticalWiggleFreq/Amp).
            //   - Pure linear horizontal scroll — clean, artifact-free.
            //   - Visual banding comes from texture authoring, not math.
            // ─────────────────────────────────────────────────────────
            float2 ConstantRotation(float2 uv, float speedMultiplier)
            {
                // Full 32-bit chain: _GlobalRotation (float) * speed (float)
                float offset = _GlobalRotation * _EquatorialSpeed * speedMultiplier;
                float rotatedX = frac(uv.x + offset);
                return float2(rotatedX, uv.y);
            }

            // ─────────────────────────────────────────────────────────
            // RESOLVE SUN DIRECTION — Fallback-safe
            //
            // PROBLEM: _SunDirection is set by HectonAtmosphereManager
            // via Shader.SetGlobalVector. During the first frame(s),
            // or in editor preview / prefab isolation, _SunDirection
            // may be (0,0,0). This causes dot(N, L) = 0 for all
            // fragments → the planet renders pitch black.
            //
            // SOLUTION: Check length(_SunDirection.xyz). If near zero,
            // substitute a hardcoded "fake sun" direction that produces
            // a pleasant 3/4 lit view (warm upper-right illumination).
            //
            // The fallback direction (1, 0.5, 0.2) is chosen to:
            //   - Light the planet from an upper-right angle
            //   - Produce visible terminator and atmosphere
            //   - Look natural in both editor and runtime
            //
            // Returns: direction FROM the sun (towards planet surface).
            //          Negate for standard NdotL lighting (L = -result).
            //
            // PERFORMANCE: length() + branch costs ~2 ALU ops per fragment.
            // Negligible compared to texture sampling. Branch is coherent
            // (all fragments take the same path) so no divergence penalty.
            // ─────────────────────────────────────────────────────────
            static const float3 FALLBACK_SUN_DIR = normalize(float3(1.0, 0.5, 0.2));
            static const float  SUN_DIR_THRESHOLD = 0.001;

            half3 ResolveSunDirection()
            {
                float3 raw = _SunDirection.xyz;
                float  len = length(raw);

                // Coherent branch: either ALL fragments use fallback or none.
                // On the first frame _SunDirection is (0,0,0) → all fallback.
                // After AtmosphereManager starts → all use real direction.
                float3 resolved = (len < SUN_DIR_THRESHOLD)
                    ? FALLBACK_SUN_DIR
                    : raw / len;       // manual normalize — we already computed length

                return (half3)resolved;
            }

            // ─────────────────────────────────
            // TERMINATOR SCATTER
            // ─────────────────────────────────
            struct TerminatorResult
            {
                half3 scatterColor;
                half  daylightFactor;
                half  terminatorMask;
            };

            TerminatorResult TerminatorScatter(half3 N, half3 L, half3 albedo)
            {
                TerminatorResult result;

                half NdotL = dot(N, L);
                half tw = _TerminatorWidth;

                half rampMin = -tw;
                half rampMax =  tw;
                half t = saturate((NdotL - rampMin) / (rampMax - rampMin + 0.0001h));
                result.daylightFactor = smoothstep(0.0h, 1.0h, t);

                half distFromTerminator = NdotL / (tw + 0.0001h);
                half gaussianMask = exp(-distFromTerminator * distFromTerminator * 2.0h);
                result.terminatorMask = gaussianMask;

                half3 tintColor = _TerminatorTintColor.rgb;
                half3 tintedAlbedo = lerp(albedo, albedo * tintColor, gaussianMask);
                result.scatterColor = tintedAlbedo * gaussianMask * _TerminatorTintStrength;

                return result;
            }

            // ─────────────────────────────────
            // CORRECTED FRESNEL
            // ─────────────────────────────────
            struct FresnelResult
            {
                half3 rimColor;
                half  rimAlpha;
            };

            FresnelResult ComputeCorrectedFresnel(
                half3  N,
                half3  V,
                half3  L,
                half3  innerColor,
                half3  outerColor,
                half   sunBacklitFactor)
            {
                FresnelResult result;

                half NdotV = saturate(dot(N, V));
                half fresnel = 1.0h - NdotV;

                half innerFresnel = pow(fresnel, _InnerPower);
                half outerFresnel = pow(fresnel, _OuterPower);

                half NdotL = dot(N, L);
                half sunGate = saturate(
                    (NdotL + _FresnelSunBias) / (0.3h + abs(_FresnelSunBias)));

                half LdotV = dot(L, V);
                half backlitGate = saturate(LdotV * 2.0h + 0.5h);

                half normalVisibility = sunGate;
                half eclipseVisibility = backlitGate * sunBacklitFactor;
                half fresnelGate = max(normalVisibility, eclipseVisibility);

                innerFresnel *= fresnelGate;
                outerFresnel *= fresnelGate;

                half eclipseFresnel = pow(fresnel, _EclipseRimPower);
                half3 eclipseContrib = _EclipseRimColor.rgb * eclipseFresnel
                                     * _EclipseRimIntensity * sunBacklitFactor;

                half3 inner = innerColor * innerFresnel;
                half3 outer = outerColor * outerFresnel;

                result.rimColor = inner + outer + eclipseContrib;
                result.rimAlpha = saturate(innerFresnel + outerFresnel
                                          + eclipseFresnel * sunBacklitFactor);

                return result;
            }

            // ─────────────────────────────────
            // VERTEX
            // ─────────────────────────────────
            Varyings GasGiantVert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS   = (half3)normalInput.normalWS;
                output.viewDirWS  = (half3)GetWorldSpaceNormalizeViewDir(
                                        vertexInput.positionWS);
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            // ─────────────────────────────────────────────────────────
            // FRAGMENT
            //
            // CHANGES v2:
            //   [FIX] ResolveSunDirection() prevents black planet when
            //         _SunDirection is (0,0,0) during init.
            //   [FIX] All rotation uses ConstantRotation() with float
            //         precision — no half jitter at large values.
            //   [DEL] Removed polarRotationMultiplier / latitude logic.
            //   [DEL] Removed vertical wiggle.
            //   [FIX] _EquatorialSpeed, _DetailSpeedMult, _StormSpeed
            //         declared as float in CBUFFER (not half).
            // ─────────────────────────────────────────────────────────
            half4 GasGiantFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 N = normalize(input.normalWS);
                half3 V = normalize(input.viewDirWS);

                // ═════════════════════════════════════════════════════
                // SUN DIRECTION — Fallback-safe resolution.
                // Guarantees the planet is ALWAYS lit, even when:
                //   - C# AtmosphereManager hasn't started
                //   - Prefab isolation / editor scene view
                //   - First frame race condition
                // ═════════════════════════════════════════════════════
                half3 sunDir = ResolveSunDirection();
                half3 L = -sunDir;    // direction TO the sun

                // ═══════════════════════════════════════
                // LAYER 1: BASE CLOUD DECK
                // Constant-speed rotation, full float precision.
                // ═══════════════════════════════════════
                float2 baseUV = ConstantRotation(input.uv, 1.0);
                half4 baseColor = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, baseUV);

                // ═══════════════════════════════════════
                // LAYER 2: UPPER HAZE / DETAIL CLOUDS
                // Rotates faster by _DetailSpeedMult factor.
                // ═══════════════════════════════════════
                float2 hazeUV = ConstantRotation(
                    input.uv * _DetailTiling.xy,
                    _DetailSpeedMult);
                half4 hazeColor = SAMPLE_TEXTURE2D(
                    _DetailTex, sampler_DetailTex, hazeUV);

                // ═══════════════════════════════════════
                // COMBINE: Pseudo-volume blend
                // ═══════════════════════════════════════
                half hazeMask = hazeColor.a * _DetailStrength;
                half3 combinedAlbedo = lerp(
                    baseColor.rgb,
                    hazeColor.rgb,
                    hazeMask);

                // ═══ TERMINATOR SCATTER ═══
                TerminatorResult terminator =
                    TerminatorScatter(N, L, combinedAlbedo);

                // ═══ PRIMARY DAYLIGHT ═══
                half3 daylight = combinedAlbedo * terminator.daylightFactor;

                // ═══ TERMINATOR RAYLEIGH ═══
                half3 terminatorContrib = terminator.scatterColor;

                // ═══ BACKLIT AMBIENT (shadow side) ═══
                half NdotL = dot(N, L);
                half shadowSide = saturate(-NdotL);
                half3 backlitAmbient = half3(0.02h, 0.025h, 0.05h)
                                     * shadowSide * _BacklitIntensity;

                // ═══ CORRECTED FRESNEL RIM ═══
                FresnelResult rim = ComputeCorrectedFresnel(
                    N, V, L,
                    _AtmosColorInner.rgb,
                    _AtmosColorOuter.rgb,
                    _SunBacklitFactor);

                // ═══ STORM EMISSION ═══
                // Uses ConstantRotation with storm-specific speed.
                // _StormSpeed is float — no precision loss.
                float stormSpeedRatio = _StormSpeed / (_EquatorialSpeed + 0.0001);
                float2 stormBaseUV = input.uv * _StormTiling.xy;
                float2 stormUV = ConstantRotation(stormBaseUV, stormSpeedRatio);

                half4 stormRaw = SAMPLE_TEXTURE2D(
                    _EmissionTex, sampler_EmissionTex, stormUV);

                half stormDayFade = saturate(
                    1.0h - terminator.daylightFactor * 1.5h);
                half3 stormEmission = stormRaw.rgb
                                    * _StormEmission * stormDayFade;

                // ═══ FINAL COMPOSITE ═══
                half3 finalColor = daylight
                                 + terminatorContrib
                                 + backlitAmbient
                                 + rim.rimColor
                                 + stormEmission;

                return half4(finalColor, 1.0h);
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════
        // PASS 1: DEPTH ONLY
        // ═══════════════════════════════════════════
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════
        // PASS 2: DEPTH NORMALS
        // ═══════════════════════════════════════════
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                VertexNormalInputs normalInput =
                    GetVertexNormalInputs(input.normalOS);
                output.normalWS = (half3)normalInput.normalWS;
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = normalize(input.normalWS);
                return half4(normalWS, 0.0h);
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════
        // PASS 3: META
        // ═══════════════════════════════════════════
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailTex_ST;
                float4 _EmissionTex_ST;
                half4  _AtmosColorInner;
                half4  _AtmosColorOuter;
                float  _GlobalRotation;
                float  _EquatorialSpeed;
                float  _DetailSpeedMult;
                float4 _DetailTiling;
                half   _DetailStrength;
                half   _BacklitIntensity;
                half   _TerminatorWidth;
                half4  _TerminatorTintColor;
                half   _TerminatorTintStrength;
                half   _InnerPower;
                half   _OuterPower;
                half   _FresnelSunBias;
                half   _SunBacklitFactor;
                half4  _EclipseRimColor;
                half   _EclipseRimIntensity;
                half   _EclipseRimPower;
                half   _StormEmission;
                float4 _StormTiling;
                float  _StormSpeed;
                half   _PlanetPhase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uvLM       : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings MetaVert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityMetaVertexPosition(
                    input.positionOS.xyz,
                    input.uvLM,
                    input.uvLM);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 MetaFrag(Varyings input) : SV_Target
            {
                MetaInput metaInput;
                metaInput.Albedo = half3(0, 0, 0);
                metaInput.Emission = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv).rgb * 0.1h;
                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}