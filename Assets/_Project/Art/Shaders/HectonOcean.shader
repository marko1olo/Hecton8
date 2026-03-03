Shader "Hecton/HectonOcean"
{
    Properties
    {
        [Header(Wave Parameters)]
        _WaveHeight("Wave Height", Range(0, 5)) = 1.0
        _WaveSpeed("Wave Speed", Range(0, 5)) = 1.0
        _WaveChoppiness("Wave Choppiness", Range(0, 2)) = 0.6

        [Header(Wave Octave 0)]
        _Wave0Dir("Wave 0 Direction (XY)", Vector) = (1, 0, 0, 0)
        _Wave0Params("Wave 0 (Amplitude, Wavelength, Steepness, Pad)", Vector) = (1.0, 8.0, 0.5, 0)

        [Header(Wave Octave 1)]
        _Wave1Dir("Wave 1 Direction (XY)", Vector) = (0.7, 0.7, 0, 0)
        _Wave1Params("Wave 1 (Amplitude, Wavelength, Steepness, Pad)", Vector) = (0.5, 4.0, 0.4, 0)

        [Header(Wave Octave 2)]
        _Wave2Dir("Wave 2 Direction (XY)", Vector) = (-0.3, 0.9, 0, 0)
        _Wave2Params("Wave 2 (Amplitude, Wavelength, Steepness, Pad)", Vector) = (0.25, 2.5, 0.35, 0)

        [Header(Color and Depth)]
        _ShallowColor("Shallow Color", Color) = (0.2, 0.75, 0.7, 0.6)
        _DeepColor("Deep Color", Color) = (0.02, 0.07, 0.15, 0.95)
        _AbsorptionCoeff("Absorption Coefficient", Range(0.01, 2.0)) = 0.45
        _DepthMaxDistance("Depth Max Distance", Range(0.1, 50)) = 15.0
        _DepthFadeDistance("Depth Fade (Shoreline Softness)", Range(0.01, 5.0)) = 1.5

        [Header(Foam)]
        _FoamColor("Foam Color", Color) = (0.85, 0.9, 0.92, 1)
        _FoamTex("Foam Texture", 2D) = "white" {}
        _FoamDepthThreshold("Foam Depth Threshold", Range(0, 3)) = 0.8
        _FoamCrestThreshold("Foam Crest Threshold", Range(0, 2)) = 0.55
        _FoamIntensity("Foam Intensity", Range(0, 3)) = 1.2
        _FoamScale("Foam UV Scale", Range(0.1, 20)) = 5.0

        [Header(Subsurface Scattering)]
        _SSSColor("SSS Color", Color) = (0.1, 0.6, 0.4, 1)
        _SSSIntensity("SSS Intensity", Range(0, 5)) = 1.5
        _SSSPower("SSS Falloff Power", Range(1, 16)) = 4.0
        _SSSDistortion("SSS Normal Distortion", Range(0, 1)) = 0.3

        [Header(Normal Maps Anti Tiling)]
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1.0
        _NormalLayer0("Layer0 (Scale, SpeedX, SpeedY, Rotation°)", Vector) = (0.04, 0.01, 0.008, 0)
        _NormalLayer1("Layer1 (Scale, SpeedX, SpeedY, Rotation°)", Vector) = (0.1, -0.018, 0.012, 37)
        _NormalLayer2("Layer2 (Scale, SpeedX, SpeedY, Rotation°)", Vector) = (0.35, 0.03, -0.025, 72)

        [Header(PBR Surface)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        _Metallic("Metallic", Range(0, 1)) = 0.02
        _FresnelPower("Fresnel Power", Range(1, 10)) = 5.0

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

        // ============================================================
        // CBUFFER — all material properties batched
        // ============================================================
        CBUFFER_START(UnityPerMaterial)
            float  _WaveHeight;
            float  _WaveSpeed;
            float  _WaveChoppiness;

            float4 _Wave0Dir;
            float4 _Wave0Params;
            float4 _Wave1Dir;
            float4 _Wave1Params;
            float4 _Wave2Dir;
            float4 _Wave2Params;

            half4  _ShallowColor;
            half4  _DeepColor;
            float  _AbsorptionCoeff;
            float  _DepthMaxDistance;
            float  _DepthFadeDistance;

            half4  _FoamColor;
            float4 _FoamTex_ST;
            float  _FoamDepthThreshold;
            float  _FoamCrestThreshold;
            float  _FoamIntensity;
            float  _FoamScale;

            half4  _SSSColor;
            float  _SSSIntensity;
            float  _SSSPower;
            float  _SSSDistortion;

            float4 _NormalMap_ST;
            float  _NormalStrength;
            float4 _NormalLayer0; // (scale, speedX, speedY, rotDeg)
            float4 _NormalLayer1;
            float4 _NormalLayer2;

            float  _Smoothness;
            float  _Metallic;
            float  _FresnelPower;
        CBUFFER_END

        // Textures
        TEXTURE2D(_FoamTex);    SAMPLER(sampler_FoamTex);
        TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);

        // ============================================================
        // GERSTNER WAVE — single octave
        // Returns: float3 displacement, float3 binormal, float3 tangent
        // ============================================================
        struct GerstnerResult
        {
            float3 displacement;
            float3 normal;
            float  crestFactor; // 0-1 height ratio for foam
        };

        GerstnerResult ComputeGerstnerWave(
            float2 worldXZ,
            float2 direction,
            float  amplitude,
            float  wavelength,
            float  steepness,
            float  globalHeight,
            float  globalSpeed,
            float  globalChop,
            float  time)
        {
            GerstnerResult o;

            float2 D = normalize(direction);
            float  k  = TWO_PI / max(wavelength, 0.001);
            float  c  = sqrt(9.81 / k); // phase velocity (deep water dispersion)
            float  A  = amplitude * globalHeight;
            float  Q  = steepness * globalChop; // Gerstner Q factor

            float  phase = k * dot(D, worldXZ) - c * k * time * globalSpeed;
            float  S, C;
            sincos(phase, S, C);

            // Displacement: xy = horizontal choppiness, z = vertical
            o.displacement.xz = -D * (Q * A * S);
            o.displacement.y  = A * C;

            // Partial derivatives for normal reconstruction
            // dP/dx, dP/dz accumulated externally; here we return per-wave contribution
            // We'll reconstruct the normal from accumulated displacement instead (more stable).
            // CrestFactor: normalized height
            o.crestFactor = saturate((C * 0.5 + 0.5)); // 0 = trough, 1 = crest

            // Approximate vertex normal contribution (analytic)
            float  WA  = k * A;
            o.normal.x  = -D.x * WA * S;
            o.normal.z  = -D.y * WA * S;
            o.normal.y  = 1.0 - Q * WA * C;

            return o;
        }

        // Aggregate 3 octaves
        void ComputeAllGerstnerWaves(
            float2 worldXZ,
            float  time,
            out float3 totalDisplacement,
            out float3 totalNormal,
            out float  crestMask)
        {
            totalDisplacement = float3(0, 0, 0);
            totalNormal       = float3(0, 0, 0);
            crestMask         = 0.0;

            // Octave 0
            GerstnerResult w0 = ComputeGerstnerWave(
                worldXZ, _Wave0Dir.xy,
                _Wave0Params.x, _Wave0Params.y, _Wave0Params.z,
                _WaveHeight, _WaveSpeed, _WaveChoppiness, time);
            totalDisplacement += w0.displacement;
            totalNormal       += w0.normal;
            crestMask          = max(crestMask, w0.crestFactor * _Wave0Params.x);

            // Octave 1
            GerstnerResult w1 = ComputeGerstnerWave(
                worldXZ, _Wave1Dir.xy,
                _Wave1Params.x, _Wave1Params.y, _Wave1Params.z,
                _WaveHeight, _WaveSpeed, _WaveChoppiness, time);
            totalDisplacement += w1.displacement;
            totalNormal       += w1.normal;
            crestMask          = max(crestMask, w1.crestFactor * _Wave1Params.x);

            // Octave 2
            GerstnerResult w2 = ComputeGerstnerWave(
                worldXZ, _Wave2Dir.xy,
                _Wave2Params.x, _Wave2Params.y, _Wave2Params.z,
                _WaveHeight, _WaveSpeed, _WaveChoppiness, time);
            totalDisplacement += w2.displacement;
            totalNormal       += w2.normal;
            crestMask          = max(crestMask, w2.crestFactor * _Wave2Params.x);

            totalNormal = normalize(totalNormal);
        }

        // ============================================================
        // UV ROTATION HELPER (for anti-tiling normals)
        // ============================================================
        float2 RotateUV(float2 uv, float angleDeg)
        {
            float rad = angleDeg * PI / 180.0;
            float s, c;
            sincos(rad, s, c);
            return float2(
                uv.x * c - uv.y * s,
                uv.x * s + uv.y * c);
        }

        // ============================================================
        // MULTI-LAYER NORMAL SAMPLING — anti-tiling
        // ============================================================
        half3 SampleAntiTileNormals(float2 worldXZ, float time)
        {
            // Layer 0: large scale, slow
            float2 uv0 = worldXZ * _NormalLayer0.x + _NormalLayer0.yz * time;
            uv0 = RotateUV(uv0, _NormalLayer0.w);
            half3 n0 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv0), _NormalStrength);

            // Layer 1: medium scale, medium speed, rotated
            float2 uv1 = worldXZ * _NormalLayer1.x + _NormalLayer1.yz * time;
            uv1 = RotateUV(uv1, _NormalLayer1.w);
            half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength * 0.7);

            // Layer 2: micro detail, fast
            float2 uv2 = worldXZ * _NormalLayer2.x + _NormalLayer2.yz * time;
            uv2 = RotateUV(uv2, _NormalLayer2.w);
            half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength * 0.45);

            // UDN blending (Unreal-style partial derivative blending)
            half3 combined;
            combined.xy = n0.xy + n1.xy + n2.xy;
            combined.z  = n0.z * n1.z * n2.z; // product preserves direction better
            return normalize(combined);
        }

        // ============================================================
        // STRUCTURES
        // ============================================================
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float4 tangentOS  : TANGENT;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS     : SV_POSITION;
            float3 positionWS     : TEXCOORD0;
            float3 normalWS       : TEXCOORD1;
            float3 tangentWS      : TEXCOORD2;
            float3 bitangentWS    : TEXCOORD3;
            float4 screenPos      : TEXCOORD4;
            float  crestMask      : TEXCOORD5;
            float3 viewDirWS      : TEXCOORD6;
            float  waveHeightRaw  : TEXCOORD7;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        ENDHLSL

        // ================================================================
        // FORWARD LIT PASS
        // ================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex   OceanVert
            #pragma fragment OceanFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // ============================================================
            // VERTEX SHADER
            // ============================================================
            Varyings OceanVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // World-space position (undisplaced)
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float  time     = _Time.y;

                // Compute Gerstner displacement
                float3 disp;
                float3 waveNormal;
                float  crest;
                ComputeAllGerstnerWaves(worldPos.xz, time, disp, waveNormal, crest);

                // Apply displacement
                worldPos += disp;

                OUT.positionWS    = worldPos;
                OUT.positionCS    = TransformWorldToHClip(worldPos);
                OUT.screenPos     = ComputeScreenPos(OUT.positionCS);
                OUT.crestMask     = crest;
                OUT.waveHeightRaw = disp.y;

                // Build TBN from wave analytic normal
                float3 N = normalize(TransformObjectToWorldNormal(waveNormal));
                float3 T = normalize(TransformObjectToWorldDir(IN.tangentOS.xyz));
                float3 B = cross(N, T) * IN.tangentOS.w;

                OUT.normalWS    = N;
                OUT.tangentWS   = T;
                OUT.bitangentWS = B;

                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);

                return OUT;
            }

            // ============================================================
            // FRAGMENT SHADER
            // ============================================================
            half4 OceanFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float  time    = _Time.y;
                float2 worldXZ = IN.positionWS.xz;

                // ---- Screen UVs ----
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                // ---- Scene Depth ----
                float  rawDepth    = SampleSceneDepth(screenUV);
                float  sceneEyeZ   = LinearEyeDepth(rawDepth, _ZBufferParams);
                float  surfaceEyeZ = IN.screenPos.w; // linear eye depth of water surface
                float  depthDiff   = sceneEyeZ - surfaceEyeZ;
                float  depthDiffClamped = max(depthDiff, 0.0);

                // ---- Beer's Law Absorption ----
                float  absorb      = 1.0 - exp(-_AbsorptionCoeff * depthDiffClamped);
                absorb = saturate(absorb);
                half4  waterColor  = lerp(_ShallowColor, _DeepColor, absorb);

                // ---- Depth Fade (soft intersection) ----
                float  depthFade   = saturate(depthDiffClamped / _DepthFadeDistance);

                // ---- Anti-Tiling Normal Map ----
                half3 normalTS = SampleAntiTileNormals(worldXZ, time);

                // Transform tangent-space normal to world space
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS));
                float3 normalWS = normalize(mul(normalTS, TBN));

                // ---- Fresnel ----
                float3 viewDir  = normalize(IN.viewDirWS);
                float  NdotV    = saturate(dot(normalWS, viewDir));
                float  fresnel  = pow(1.0 - NdotV, _FresnelPower);
                fresnel = saturate(fresnel);

                // ---- Main Light ----
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);
                float3 lightDir    = normalize(mainLight.direction);
                half3  lightColor  = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                // ---- PBR Diffuse + Specular (simplified) ----
                float  NdotL      = saturate(dot(normalWS, lightDir));
                half3  diffuse    = waterColor.rgb * lightColor * NdotL;

                // GGX-like specular (Blinn-Phong approximation for perf)
                float3 halfDir    = normalize(lightDir + viewDir);
                float  NdotH      = saturate(dot(normalWS, halfDir));
                float  specPow    = exp2(10.0 * _Smoothness + 1.0);
                float  spec       = pow(NdotH, specPow) * _Smoothness;
                half3  specular   = lightColor * spec;

                // ---- Additional Lights ----
                half3  addLighting = half3(0, 0, 0);
                #ifdef _ADDITIONAL_LIGHTS
                uint addLightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < addLightCount; li++)
                {
                    Light addLight = GetAdditionalLight(li, IN.positionWS, half4(1,1,1,1));
                    float aNdotL   = saturate(dot(normalWS, addLight.direction));
                    addLighting   += addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation * aNdotL;
                }
                #endif
                diffuse += waterColor.rgb * addLighting;

                // ---- Subsurface Scattering (Fake SSS) ----
                // Light bleeding through wave tips when looking toward the sun
                float3 sssNormal = normalize(normalWS * _SSSDistortion + lightDir);
                float  sssDot    = saturate(dot(viewDir, -sssNormal));
                float  sss       = pow(sssDot, _SSSPower) * _SSSIntensity;
                // Modulate by wave height (stronger at crests / thin parts)
                sss *= saturate(IN.waveHeightRaw / max(_WaveHeight, 0.01));
                half3  sssContrib = sss * _SSSColor.rgb * lightColor;

                // ---- Foam ----
                float2 foamUV     = worldXZ * _FoamScale + time * float2(0.02, 0.01);
                half   foamTex    = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).r;

                // Intersection foam
                float  intersectionFoam = 1.0 - saturate(depthDiffClamped / _FoamDepthThreshold);
                intersectionFoam = intersectionFoam * intersectionFoam; // sharpen

                // Crest foam
                float  normalizedCrest = saturate(IN.waveHeightRaw / max(_WaveHeight * 1.5, 0.01));
                float  crestFoam       = saturate(normalizedCrest - (1.0 - _FoamCrestThreshold)) / max(_FoamCrestThreshold, 0.01);
                crestFoam = saturate(crestFoam);

                float  foamMask   = saturate(intersectionFoam + crestFoam) * foamTex * _FoamIntensity;
                half3  foamFinal  = _FoamColor.rgb * foamMask;

                // ---- Ambient / Environment ----
                half3  ambient = SampleSH(normalWS) * waterColor.rgb * 0.4;

                // ---- Reflection (environment cubemap via URP) ----
                float3 reflDir     = reflect(-viewDir, normalWS);
                half3  envReflect  = GlossyEnvironmentReflection(reflDir, 1.0 - _Smoothness, 1.0);

                // ---- Composite ----
                half3  color = half3(0, 0, 0);
                color += diffuse;
                color += specular;
                color += sssContrib;
                color += ambient;
                color  = lerp(color, envReflect, fresnel * _Metallic + fresnel * 0.5); // reflections at grazing
                color += foamFinal;

                // ---- Alpha ----
                half alpha = waterColor.a;
                alpha = lerp(alpha, 1.0, fresnel * 0.5);   // more opaque at grazing angles
                alpha *= depthFade;                          // fade at shoreline intersection
                alpha = saturate(alpha + foamMask * 0.7);    // foam is opaque-ish

                // ---- Fog ----
                float fogFactor = ComputeFogFactor(IN.positionCS.z);
                color = MixFog(color, fogFactor);

                return half4(color, alpha);
            }

            ENDHLSL
        }

        // ================================================================
        // DEPTH ONLY PASS (for shadow / depth prepass)
        // ================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(Attributes IN)
            {
                DepthVaryings OUT = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float  time     = _Time.y;

                float3 disp;
                float3 wn;
                float  c;
                ComputeAllGerstnerWaves(worldPos.xz, time, disp, wn, c);
                worldPos += disp;

                OUT.positionCS = TransformWorldToHClip(worldPos);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }

        // ================================================================
        // SHADOW CASTER PASS
        // ================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(Attributes IN)
            {
                ShadowVaryings OUT = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float  time     = _Time.y;

                float3 disp, wn;
                float  c;
                ComputeAllGerstnerWaves(worldPos.xz, time, disp, wn, c);
                worldPos += disp;

                float3 worldNormal = normalize(wn);
                Light  mainLight   = GetMainLight();

                OUT.positionCS = TransformWorldToHClip(
                    ApplyShadowBias(worldPos, worldNormal, mainLight.direction));
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.ShaderGraphLitGUI"
}