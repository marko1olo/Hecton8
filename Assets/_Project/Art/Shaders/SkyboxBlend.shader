// SG_SkyboxBlend.shader
// Шейдер скайбокса с блендом между дневным и ночным кубмапом + звёзды

Shader "Hecton/SkyboxBlend"
{
    Properties
    {
        _DayCubemap ("Day Cubemap", Cube) = "" {}
        _NightCubemap ("Night Cubemap", Cube) = "" {}
        _Blend ("Day/Night Blend", Range(0, 1)) = 0
        _StarIntensity ("Star Intensity", Range(0, 2)) = 0
        _DayTint ("Day Tint", Color) = (1, 1, 1, 1)
        _NightTint ("Night Tint", Color) = (0.1, 0.1, 0.2, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "SkyboxBlend"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_DayCubemap);
            SAMPLER(sampler_DayCubemap);
            TEXTURECUBE(_NightCubemap);
            SAMPLER(sampler_NightCubemap);

            CBUFFER_START(UnityPerMaterial)
                float  _Blend;
                float  _StarIntensity;
                float4 _DayTint;
                float4 _NightTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 texcoord   : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Skybox: позиция без трансляции
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.texcoord = input.positionOS.xyz;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.texcoord);

                float4 dayColor = SAMPLE_TEXTURECUBE(_DayCubemap, sampler_DayCubemap, dir);
                float4 nightColor = SAMPLE_TEXTURECUBE(_NightCubemap, sampler_NightCubemap, dir);

                // Тинтируем
                dayColor.rgb *= _DayTint.rgb;
                nightColor.rgb *= _NightTint.rgb;

                // Звёзды в ночном кубмапе масштабируются _StarIntensity
                // Предполагаем что ночной кубмап содержит звёзды в rgb
                nightColor.rgb *= _StarIntensity;

                // Бленд
                float blend = saturate(_Blend);
                blend = blend * blend * (3.0 - 2.0 * blend); // smoothstep

                float3 finalColor = lerp(dayColor.rgb, nightColor.rgb, blend);

                // Добавляем минимальный ambient к ночи чтобы не было pitch black
                float3 nightAmbient = float3(0.005, 0.005, 0.012) * blend;
                finalColor += nightAmbient;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}