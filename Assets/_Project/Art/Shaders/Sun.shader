Shader "Hecton/Celestial/Sun"
{
    Properties
    {
        [HDR] _SunColor ("Sun Color", Color) = (1.0, 0.85, 0.4, 1.0)
        _GlowIntensity ("Glow Intensity", Float) = 50.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SunUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Fog { Mode Off }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog // включаем для возможности отключить

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SunColor;
                half _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.uv = input.uv;

                return output;
            }

            // Limb Darkening модель на основе коэффициентов солнечного лимба
            // Использует полиномиальную аппроксимацию Neckel & Labs (1994)
            // I(mu)/I(1) = a0 + a1*mu + a2*mu^2 + a3*mu^3 + a4*mu^4 + a5*mu^5
            // где mu = cos(theta) — угол между нормалью поверхности и направлением наблюдения
            half3 LimbDarkening(half mu)
            {
                // Коэффициенты для RGB каналов (спектральная зависимость)
                // Красный канал — меньше затемнения к краю
                // Синий канал — больше затемнения к краю (реалистичная хроматическая дисперсия)
                static const half3 a0 = half3(0.3, 0.25, 0.15);
                static const half3 a1 = half3(0.93, 0.87, 0.73);
                static const half3 a2 = half3(-0.23, -0.12, 0.0);

                // Квадратичная аппроксимация: достаточно точная и дешёвая
                half mu2 = mu * mu;
                half3 darkening = a0 + a1 * mu + a2 * mu2;

                return saturate(darkening);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // mu = dot(N, V) — 1.0 в центре диска, 0.0 на краю
                half3 N = normalize(input.normalWS);
                half3 V = normalize(input.viewDirWS);
                half mu = saturate(dot(N, V));

                // Limb Darkening
                half3 limbFactor = LimbDarkening(mu);

                // Цветовой градиент: центр — чистый белый, край — цвет _SunColor
                // Интерполяция: при mu=1 (центр) цвет белый, при mu=0 (край) цвет _SunColor
                half centerBlend = mu * mu; // квадратичный для более резкого белого центра
                half3 baseColor = lerp(_SunColor.rgb, half3(1.0, 1.0, 1.0), centerBlend);

                // Применяем limb darkening
                half3 color = baseColor * limbFactor;

                // Интенсивность: центр должен быть экстремально ярким для Bloom
                // Экспоненциальный профиль интенсивности — пик в центре
                half intensityProfile = pow(mu, 0.5); // мягкий спад
                half intensity = _GlowIntensity * intensityProfile;

                // Дополнительный сверхяркий пик в самом центре для Bloom
                half bloomCore = pow(mu, 8.0) * _GlowIntensity * 2.0;
                intensity += bloomCore;

                // Мягкий край (fade out на силуэте) чтобы не было жёсткого обрезания
                half edgeFade = smoothstep(0.0, 0.15, mu);
                intensity *= edgeFade;

                half3 finalColor = color * intensity;

                // Корона / внешнее свечение на самом краю
                half coronaFactor = pow(1.0 - mu, 3.0) * edgeFade;
                half3 coronaColor = _SunColor.rgb * coronaFactor * _GlowIntensity * 0.3;
                finalColor += coronaColor;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // Depth prepass — отключен, мы не пишем в глубину
        // Shadow pass — не нужен для солнца

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite Off
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}