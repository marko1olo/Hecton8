// SG_SkyboxBlend.shader
// Sheyder skayboksa s blendom mezhdu dnevnym i nochnym kubmapom + zvezdy

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
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants POINT POINT_COOKIE SHADOWS_CUBE
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

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

            float _HectonFreezeFrameDither;

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
                // Skybox: pozitsiya bez translyatsii
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(posWS);
                output.texcoord = input.positionOS.xyz;
                return output;
            }

            float ResolveFreezeFrameNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float3 ApplyFreezeFrameDither(float3 color, float2 positionCS)
            {
                float freeze = saturate(_HectonFreezeFrameDither);
                float noise = ResolveFreezeFrameNoise(positionCS);
                float scanline = step(0.5, frac(positionCS.y * 0.5));
                float ditherMask = step(noise, freeze);
                float3 frozenTint = color * 0.72 + float3(0.015, 0.055, 0.075) * 0.28;
                frozenTint += (noise - 0.5) * 0.055 + scanline * 0.018;
                frozenTint *= lerp(1.0, 0.82 + ditherMask * 0.18, freeze);
                return lerp(color, frozenTint, freeze);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.texcoord);

                float4 dayColor = SAMPLE_TEXTURECUBE(_DayCubemap, sampler_DayCubemap, dir);
                float4 nightColor = SAMPLE_TEXTURECUBE(_NightCubemap, sampler_NightCubemap, dir);

                // Tintiruem
                dayColor.rgb *= _DayTint.rgb;
                nightColor.rgb *= _NightTint.rgb;

                // Zvezdy v nochnom kubmape masshtabiruyutsya _StarIntensity
                // Predpolagaem chto nochnoy kubmap soderzhit zvezdy v rgb
                nightColor.rgb *= _StarIntensity;

                // Blend
                float blend = saturate(_Blend);
                blend = blend * blend * (3.0 - 2.0 * blend); // smoothstep

                float3 finalColor = lerp(dayColor.rgb, nightColor.rgb, blend);

                // Dobavlyaem minimalnyy ambient k nochi chtoby ne bylo pitch black
                float3 nightAmbient = float3(0.005, 0.005, 0.012) * blend;
                finalColor += nightAmbient;
                finalColor = ApplyFreezeFrameDither(finalColor, input.positionCS.xy);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
