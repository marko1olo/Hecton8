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

        [Header(Differential Rotation)]
        _EquatorialSpeed ("Equatorial Speed", Float) = 0.02
        _PolarMultiplier ("Polar Multiplier", Range(0, 1)) = 0.4
        _DetailTiling ("Detail Tiling", Vector) = (3, 3, 0, 0)
        _DetailSpeed ("Detail Drift Speed", Float) = 0.005
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

            // ─────────────────────────────────
            // CBUFFER (SRP Batcher compatible)
            // ─────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _DetailTex_ST;
                float4 _EmissionTex_ST;

                half4  _AtmosColorInner;
                half4  _AtmosColorOuter;

                half   _EquatorialSpeed;
                half   _PolarMultiplier;
                float4 _DetailTiling;
                half   _DetailSpeed;
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
                half   _StormSpeed;

                half   _PlanetPhase;
            CBUFFER_END

            // ─────────────────────────────────
            // GLOBALS (set from C#)
            // ─────────────────────────────────
            float4 _SunDirection; // Shader.SetGlobalVector — direction FROM sun (normalized)

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
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float3 positionWS  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─────────────────────────────────
            // DIFFERENTIAL ROTATION
            // ─────────────────────────────────
            float2 DifferentialRotation(float2 uv, float time)
            {
                // Latitude: UV.y 0..1, equator at 0.5
                float latitude = abs(uv.y - 0.5) * 2.0;   // 0 at equator, 1 at poles
                float latitudeMask = 1.0 - latitude;        // 1 at equator, 0 at poles

                // Jupiter-like cos²-modulated speed
                float polarSpeed = _EquatorialSpeed * _PolarMultiplier;
                float speed = lerp(polarSpeed, _EquatorialSpeed, latitudeMask);
                speed *= latitudeMask;

                return float2(uv.x + time * speed, uv.y);
            }

            // ─────────────────────────────────
            // TERMINATOR SCATTER FUNCTION
            // ─────────────────────────────────
            // Physically-motivated terminator with Rayleigh-like
            // color shift. Returns:
            //   .rgb = terminator scatter color contribution
            //   .a   = soft daylight factor (0 = full night, 1 = full day)
            // ─────────────────────────────────
            struct TerminatorResult
            {
                half3 scatterColor;    // Rayleigh scatter tint at terminator
                half  daylightFactor;  // smooth day/night ramp
                half  terminatorMask;  // peaks at the terminator line itself
            };

            TerminatorResult TerminatorScatter(float3 N, float3 L, half3 albedo)
            {
                TerminatorResult result;

                float NdotL = dot(N, L);
                float tw = _TerminatorWidth;

                // ── Smooth daylight ramp ──
                // Remaps NdotL from [-tw, +tw] → [0, 1] with smoothstep
                // This gives a physically softer gradient than a linear remap
                float rampMin = -tw;
                float rampMax =  tw;
                float t = saturate((NdotL - rampMin) / (rampMax - rampMin + 0.0001));
                result.daylightFactor = smoothstep(0.0, 1.0, t);

                // ── Terminator zone mask ──
                // Gaussian-like falloff centered at NdotL ≈ 0 (the geometric terminator)
                // Width controlled by _TerminatorWidth
                float distFromTerminator = NdotL / (tw + 0.0001);
                float gaussianMask = exp(-distFromTerminator * distFromTerminator * 2.0);
                result.terminatorMask = gaussianMask;

                // ── Rayleigh color shift ──
                // At the terminator, sunlight passes through maximum atmospheric depth.
                // Short wavelengths (blue) scatter away → remaining light is orange/red.
                // We blend the tint color based on how deep into the terminator we are.
                half3 tintColor = _TerminatorTintColor.rgb;

                // The scatter contribution is strongest right at the terminator
                // and tints the existing albedo
                half3 tintedAlbedo = lerp(albedo, albedo * tintColor, gaussianMask);
                result.scatterColor = tintedAlbedo * gaussianMask * _TerminatorTintStrength;

                return result;
            }

            // ─────────────────────────────────
            // CORRECTED FRESNEL FUNCTION
            // ─────────────────────────────────
            // Key fix: Fresnel rim is gated by sun-facing geometry.
            // The rim should ONLY appear:
            //   1. On the sun-lit hemisphere (normal lighting)
            //   2. When the sun is directly behind (eclipse backlight)
            // It must NOT bleed onto the dark side during normal phases.
            // ─────────────────────────────────
            struct FresnelResult
            {
                half3 rimColor;
                half  rimAlpha;
            };

            FresnelResult ComputeCorrectedFresnel(
                float3 N,
                float3 V,
                float3 L,           // direction TO sun
                half3  innerColor,
                half3  outerColor,
                half   sunBacklitFactor
            )
            {
                FresnelResult result;

                float NdotV = saturate(dot(N, V));
                float fresnel = 1.0 - NdotV;

                float innerFresnel = pow(fresnel, _InnerPower);
                float outerFresnel = pow(fresnel, _OuterPower);

                // ── Sun-side visibility gate ──
                // dot(L, V): +1 when viewer looks toward sun (sun behind planet = eclipse)
                //            -1 when viewer looks away from sun (sun in front of planet = normal)
                //
                // For the Fresnel rim on the lit side, we need NdotL > 0 on rim pixels.
                // But rim pixels have N nearly perpendicular to V, so NdotL is the real gate.
                //
                // We use a combination:
                //   sunVisibility = how much the surface normal faces the sun
                //   backlitVisibility = how much the sun is behind the planet from camera's view
                //
                // Normal phase: rim only where NdotL > bias (sun-lit limb)
                // Eclipse phase: rim everywhere (sunBacklitFactor drives this)

                float NdotL = dot(N, L);

                // Gate 1: Surface-level sun visibility for this pixel's rim
                // _FresnelSunBias shifts the cutoff slightly into shadow for a thin scatter rim
                float sunGate = saturate((NdotL + _FresnelSunBias) / (0.3 + abs(_FresnelSunBias)));

                // Gate 2: Backlit/eclipse visibility
                // dot(L, V) > 0 means sun is somewhat behind the planet from viewer's perspective
                float LdotV = dot(L, V);
                float backlitGate = saturate(LdotV * 2.0 + 0.5); // ramps from 0 to 1

                // Combine: during normal viewing, sunGate dominates.
                // During eclipse (sunBacklitFactor → 1), we bypass sunGate and use backlitGate.
                float normalVisibility = sunGate;
                float eclipseVisibility = backlitGate * sunBacklitFactor;

                // Final gate: whichever is stronger
                float fresnelGate = max(normalVisibility, eclipseVisibility);

                // Apply gate to fresnel
                innerFresnel *= fresnelGate;
                outerFresnel *= fresnelGate;

                // ── Eclipse rim boost ──
                // When _SunBacklitFactor = 1, the outer atmosphere glows intensely & uniformly
                float eclipseFresnel = pow(fresnel, _EclipseRimPower);
                half3 eclipseContrib = _EclipseRimColor.rgb * eclipseFresnel
                                     * _EclipseRimIntensity * sunBacklitFactor;

                // ── Composite ──
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

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS  = vertexInput.positionCS;
                output.positionWS  = vertexInput.positionWS;
                output.normalWS    = normalInput.normalWS;
                output.viewDirWS   = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.uv          = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            // ─────────────────────────────────
            // FRAGMENT
            // ─────────────────────────────────
            half4 GasGiantFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float3 L = -sunDir;  // direction TO sun
                float  time = _Time.y;

                // ═══ DIFFERENTIAL ROTATION UV ═══
                float2 rotatedUV = DifferentialRotation(input.uv, time);

                // ═══ SAMPLE MAIN CLOUDS ═══
                half4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rotatedUV);

                // ═══ SAMPLE DETAIL CLOUDS ═══
                float2 detailUV = rotatedUV * _DetailTiling.xy + float2(time * _DetailSpeed, 0);
                half4 detailColor = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, detailUV);

                // ═══ COMBINE ALBEDO ═══
                half3 combinedAlbedo = mainColor.rgb + detailColor.rgb * _DetailStrength;

                // ═══ TERMINATOR SCATTER ═══
                TerminatorResult terminator = TerminatorScatter(N, L, combinedAlbedo);

                // ═══ PRIMARY DAYLIGHT ═══
                half3 daylight = combinedAlbedo * terminator.daylightFactor;

                // ═══ TERMINATOR RAYLEIGH CONTRIBUTION ═══
                half3 terminatorContrib = terminator.scatterColor;

                // ═══ BACKLIT AMBIENT (shadow side) ═══
                float NdotL = dot(N, L);
                float shadowSide = saturate(-NdotL);
                half3 backlitAmbient = half3(0.02, 0.025, 0.05) * shadowSide * _BacklitIntensity;

                // ═══ CORRECTED FRESNEL RIM ═══
                FresnelResult rim = ComputeCorrectedFresnel(
                    N, V, L,
                    _AtmosColorInner.rgb,
                    _AtmosColorOuter.rgb,
                    _SunBacklitFactor
                );

                // ═══ STORM EMISSION ═══
                float2 stormUV = rotatedUV * _StormTiling.xy + float2(time * _StormSpeed, 0);
                half4 stormRaw = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, stormUV);

                // Storms glow on the night side (lightning in deep atmosphere)
                // Fade them out on the fully lit side so they don't wash out
                float stormDayFade = saturate(1.0 - terminator.daylightFactor * 1.5);
                half3 stormEmission = stormRaw.rgb * _StormEmission * stormDayFade;

                // ═══ FINAL COMPOSITE ═══
                half3 finalColor = half3(0, 0, 0);

                // Day/night lit surface
                finalColor += daylight;

                // Rayleigh scatter at terminator
                finalColor += terminatorContrib;

                // Minimal backlit ambient on shadow side
                finalColor += backlitAmbient;

                // Atmospheric rim (correctly gated)
                finalColor += rim.rimColor;

                // Storm emission
                finalColor += stormEmission;

                // No fog applied — space objects must not receive scene fog
                return half4(finalColor, 1.0);
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
                float3 normalWS   : TEXCOORD0;
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
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                return half4(normalWS, 0.0);
            }

            ENDHLSL
        }

        // ═══════════════════════════════════════════
        // PASS 3: META (for lightmapping / GI)
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
                half   _EquatorialSpeed;
                half   _PolarMultiplier;
                float4 _DetailTiling;
                half   _DetailSpeed;
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
                half   _StormSpeed;
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
                    input.uvLM
                );
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 MetaFrag(Varyings input) : SV_Target
            {
                MetaInput metaInput;
                metaInput.Albedo = half3(0, 0, 0);
                metaInput.Emission = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, input.uv
                ).rgb * 0.1;

                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }

        // Shadow Caster intentionally omitted — gas giants cast no mesh shadows
    }

    FallBack "Universal Render Pipeline/Unlit"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.UnlitShaderGUI"
}