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
        _HUD_Intensity ("HUD Brightness", Range(0, 5)) = 2.5
        _HUD_Color ("HUD Tint", Color) = (0.2, 1.0, 0.3, 1.0)
        _HUD_ScratchBleed ("HUD Scratch Light Bleed", Range(0, 2)) = 0.8

        [Header(Imperfections)]
        _ScratchNormalMap ("Scratch Normal Map", 2D) = "bump" {}
        _ScratchNormalStrength ("Scratch Normal Strength", Range(0, 2)) = 0.6
        _FingerprintTex ("Fingerprint Smudge (R=mask)", 2D) = "black" {}
        _FingerprintStrength ("Fingerprint Strength", Range(0, 1)) = 0.3

        [Header(Refraction Distortion)]
        _DistortionStrength ("Edge Distortion", Range(0, 0.1)) = 0.02
        _DistortionFalloff ("Distortion Edge Falloff", Range(0.5, 5)) = 2.0

        [Header(Fresnel)]
        _FresnelColor ("Fresnel Rim Color", Color) = (0.4, 0.6, 0.8, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 0.6

        [Header(Environment Reflection)]
        _EnvReflStrength ("Environment Reflection", Range(0, 1)) = 0.15
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Metallic ("Metallic", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // Не записываем в depth, чтобы видеть океан за стеклом
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

        Pass
        {
            Name "VisorForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex VisorVert
            #pragma fragment VisorFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ─────────────────────────────────────────────
            // CBUFFER
            // ─────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _GlassAlpha;
                float  _IOR;

                float4 _HUD_RenderTexture_ST;
                float  _HUD_Intensity;
                float4 _HUD_Color;
                float  _HUD_ScratchBleed;

                float4 _ScratchNormalMap_ST;
                float  _ScratchNormalStrength;
                float4 _FingerprintTex_ST;
                float  _FingerprintStrength;

                float  _DistortionStrength;
                float  _DistortionFalloff;

                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelIntensity;

                float  _EnvReflStrength;
                float  _Smoothness;
                float  _Metallic;
            CBUFFER_END

            TEXTURE2D(_HUD_RenderTexture);   SAMPLER(sampler_HUD_RenderTexture);
            TEXTURE2D(_ScratchNormalMap);     SAMPLER(sampler_ScratchNormalMap);
            TEXTURE2D(_FingerprintTex);      SAMPLER(sampler_FingerprintTex);
            TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

            // ─────────────────────────────────────────────
            // STRUCTURES
            // ─────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float2 uv            : TEXCOORD0;
                float3 positionWS    : TEXCOORD1;
                float3 normalWS      : TEXCOORD2;
                float3 tangentWS     : TEXCOORD3;
                float3 bitangentWS   : TEXCOORD4;
                float4 screenPos     : TEXCOORD5;
                float  fogCoord      : TEXCOORD6;
                float3 viewDirWS     : TEXCOORD7;
            };

            // ─────────────────────────────────────────────
            // VERTEX
            // ─────────────────────────────────────────────
            Varyings VisorVert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = nrmInputs.normalWS;
                OUT.tangentWS   = nrmInputs.tangentWS;
                OUT.bitangentWS = nrmInputs.bitangentWS;
                OUT.uv          = IN.uv;
                OUT.screenPos   = ComputeScreenPos(posInputs.positionCS);
                OUT.fogCoord    = ComputeFogFactor(posInputs.positionCS.z);
                OUT.viewDirWS   = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);

                return OUT;
            }

            // ─────────────────────────────────────────────
            // HELPERS
            // ─────────────────────────────────────────────

            // Распаковка нормали с регулируемой силой
            float3 UnpackScaledNormal(float4 packedNormal, float scale)
            {
                float3 n;
                n.xy = (packedNormal.rg * 2.0 - 1.0) * scale;
                n.z  = sqrt(max(0, 1.0 - dot(n.xy, n.xy)));
                return n;
            }

            // Маска «края линзы» — используется для рефракции
            float EdgeMask(float2 uv, float falloff)
            {
                // Предполагаем UV 0-1 по кругу линзы
                float2 centered = uv * 2.0 - 1.0;
                float dist = length(centered);
                return pow(saturate(dist), falloff);
            }

            // ─────────────────────────────────────────────
            // FRAGMENT
            // ─────────────────────────────────────────────
            float4 VisorFrag(Varyings IN) : SV_Target
            {
                // === 1. SCRATCH NORMAL MAP ===
                float2 scratchUV = TRANSFORM_TEX(IN.uv, _ScratchNormalMap);
                float4 scratchPacked = SAMPLE_TEXTURE2D(_ScratchNormalMap, sampler_ScratchNormalMap, scratchUV);
                float3 scratchNormalTS = UnpackScaledNormal(scratchPacked, _ScratchNormalStrength);

                // Scratch intensity mask (как сильно отклоняется нормаль = глубина царапины)
                float scratchMask = length(scratchNormalTS.xy);

                // TBN → World
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                float3 normalWS = normalize(mul(scratchNormalTS, TBN));

                // === 2. FINGERPRINT SMUDGE ===
                float2 fpUV = TRANSFORM_TEX(IN.uv, _FingerprintTex);
                float fingerprint = SAMPLE_TEXTURE2D(_FingerprintTex, sampler_FingerprintTex, fpUV).r;
                fingerprint *= _FingerprintStrength;

                // Отпечатки снижают прозрачность (матовят стекло)
                float smudgeOpacity = fingerprint * 0.4;

                // === 3. FRESNEL ===
                float3 viewDir = normalize(IN.viewDirWS);
                float NdotV = saturate(dot(normalWS, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelIntensity;
                float3 fresnelColor = _FresnelColor.rgb * fresnel;

                // === 4. REFRACTION / DISTORTION ===
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float edgeDist = EdgeMask(IN.uv, _DistortionFalloff);

                // Смещение UV на основе нормали + края
                float2 distortionOffset = scratchNormalTS.xy * _DistortionStrength;
                distortionOffset += edgeDist * normalWS.xy * _DistortionStrength * 0.5;

                float2 refractedUV = screenUV + distortionOffset;

                // Сэмплим то, что за стеклом (Opaque Texture)
                float3 sceneColor = SAMPLE_TEXTURE2D(_CameraOpaqueTexture,
                    sampler_CameraOpaqueTexture, refractedUV).rgb;

                // === 5. HUD RENDER TEXTURE (Emission) ===
                float2 hudUV = TRANSFORM_TEX(IN.uv, _HUD_RenderTexture);

                // Применяем дисторсию к HUD UV тоже — создает «объемность»
                float2 hudDistortedUV = hudUV + distortionOffset * 0.3;

                float4 hudSample = SAMPLE_TEXTURE2D(_HUD_RenderTexture,
                    sampler_HUD_RenderTexture, hudDistortedUV);

                float hudAlpha = hudSample.a;
                float3 hudColor = hudSample.rgb * _HUD_Color.rgb * _HUD_Intensity;

                // HUD подсвечивает царапины — свет «растекается» по царапинам
                float scratchBleed = scratchMask * _HUD_ScratchBleed * hudAlpha;
                float3 hudScratchGlow = hudColor * scratchBleed * 0.5;

                // Fingerprint рассеивает свет HUD
                float3 hudFingerprintGlow = hudColor * fingerprint * hudAlpha * 0.3;

                // === 6. ENVIRONMENT REFLECTION ===
                float3 reflectDir = reflect(-viewDir, normalWS);
                // Простое приближение: sample reflection probe
                float3 envRefl = GlossyEnvironmentReflection(
                    reflectDir, _Smoothness, _EnvReflStrength);

                // === 7. LIGHTING (minimal — стекло не сильно реагирует) ===
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 specular = mainLight.color * pow(saturate(
                    dot(reflect(-mainLight.direction, normalWS), viewDir)),
                    128.0 * _Smoothness) * 0.3;

                // === 8. COMPOSITE ===
                // Базовый цвет стекла
                float3 glassBase = _BaseColor.rgb;

                // Стекло показывает сцену за собой
                float3 behindGlass = sceneColor * (1.0 - _BaseColor.a);

                // Складываем все слои
                float3 finalColor = float3(0, 0, 0);

                // Сцена за стеклом (рефракция)
                finalColor += behindGlass;

                // Тонировка стекла
                finalColor += glassBase * _GlassAlpha;

                // Отпечатки (матовые пятна)
                finalColor = lerp(finalColor, finalColor * 0.85 + 0.02, smudgeOpacity);

                // Environment reflection
                finalColor += envRefl;

                // Specular highlights
                finalColor += specular;

                // Fresnel rim (блики океана по краям)
                finalColor += fresnelColor;

                // HUD Emission (главный слой — зеленые цифры)
                finalColor += hudColor * hudAlpha;

                // HUD bleeding into scratches
                finalColor += hudScratchGlow;
                finalColor += hudFingerprintGlow;

                // === 9. ALPHA ===
                // Стекло прозрачно, но HUD добавляет непрозрачность
                float finalAlpha = _GlassAlpha
                    + hudAlpha * 0.9
                    + fresnel * 0.4
                    + smudgeOpacity
                    + scratchBleed * 0.2;
                finalAlpha = saturate(finalAlpha);

                // Fog
                finalColor = MixFog(finalColor, IN.fogCoord);

                return float4(finalColor, finalAlpha);
            }

            ENDHLSL
        }

        // Shadow caster pass — стекло не отбрасывает тень
        // (намеренно пропущен)
    }

    FallBack "Universal Render Pipeline/Lit"
    // CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShaderGUI"
}