Shader "HECTON/World/H8_SurfaceWaterReadability_1428"
{
    Properties
    {
        [HDR] _ShallowColor ("Shallow Color", Color) = (0.30, 0.86, 0.90, 0.72)
        [HDR] _DeepColor ("Deep Color", Color) = (0.02, 0.34, 0.54, 0.78)
        [HDR] _FoamColor ("Foam Color", Color) = (0.92, 1.00, 0.94, 0.46)
        [HDR] _SpecularTint ("Specular Tint", Color) = (0.70, 0.94, 1.00, 0.42)
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _RippleScale ("Ripple Scale", Range(0.005, 0.25)) = 0.055
        _FoamScale ("Foam Scale", Range(0.01, 0.5)) = 0.11
        _HorizonFade ("Horizon Fade", Range(0, 1)) = 0.58
        _EdgeFade ("Edge Fade", Range(0.001, 0.4)) = 0.08
        _GlobalQualityWeight ("Global Quality Weight", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+18"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "SurfaceWaterReadability"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half4 _SpecularTint;
                half _Opacity;
                half _RippleScale;
                half _FoamScale;
                half _HorizonFade;
                half _EdgeFade;
                half _GlobalQualityWeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half q = saturate(_GlobalQualityWeight);
                half rippleScale = max(_RippleScale, 0.001h);
                half lowTierWave = 0.012h;
                half highTierWave = 0.055h;
                half waveAmp = lerp(lowTierWave, highTierWave, q);
                half waveA = sin((positionWS.x * rippleScale) + (positionWS.z * 0.037h) + _Time.y * lerp(0.04h, 0.13h, q));
                half waveB = sin((positionWS.z * rippleScale * 1.47h) - (positionWS.x * 0.029h) + 1.91h);
                positionWS.y += (waveA * 0.58h + waveB * 0.42h) * waveAmp;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(127.13, 311.77));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            half ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                half a = Hash21(i);
                half b = Hash21(i + float2(1.0, 0.0));
                half c = Hash21(i + float2(0.0, 1.0));
                half d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, (half)u.x), lerp(c, d, (half)u.x), (half)u.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half q = saturate(_GlobalQualityWeight);
                half depth = saturate(input.uv.y);
                half edgeX = min((half)input.uv.x, (half)(1.0 - input.uv.x));
                half edge = smoothstep(0.0h, max(_EdgeFade, 0.001h), edgeX);
                half nearFade = smoothstep(0.0h, 0.045h, depth);
                half horizon = 1.0h - smoothstep(saturate(1.0h - _HorizonFade), 1.0h, depth);

                half rippleScale = max(_RippleScale, 0.001h);
                float2 worldUv = input.positionWS.xz * rippleScale;
                half rippleA = 0.5h + 0.5h * sin(worldUv.x * 2.7h + worldUv.y * 0.82h + _Time.y * lerp(0.08h, 0.21h, q));
                half rippleB = 0.5h + 0.5h * sin(worldUv.y * 3.9h - worldUv.x * 1.13h + 2.2h);
                half grain = ValueNoise(worldUv * lerp(1.4h, 3.1h, q));
                half caustic = pow(saturate(rippleA * rippleB * (0.72h + grain * 0.38h)), lerp(5.5h, 3.2h, q));

                half foamBand = smoothstep(0.12h, 0.34h, depth) * (1.0h - smoothstep(0.48h, 0.78h, depth));
                half foamLines = 0.5h + 0.5h * sin((input.positionWS.x + input.positionWS.z * 0.43h) * max(_FoamScale, 0.001h));
                half foam = smoothstep(0.72h - q * 0.14h, 0.97h, foamLines * 0.68h + grain * 0.34h) * foamBand;

                half3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.18h, 1.0h, depth));
                water = lerp(water, _FoamColor.rgb, foam * _FoamColor.a);
                water += _SpecularTint.rgb * caustic * _SpecularTint.a * lerp(0.32h, 0.92h, q);

                half alpha = _Opacity * lerp(_ShallowColor.a, _DeepColor.a, depth);
                alpha *= edge * nearFade * horizon * input.color.a;
                alpha = saturate(alpha + foam * _FoamColor.a * 0.18h);
                return half4(water, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
