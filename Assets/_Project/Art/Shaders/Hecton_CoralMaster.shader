Shader "Hecton8/Flora/CoralMaster"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        _DetailMap ("Detail Map", 2D) = "gray" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _MaskMap ("Mask Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (0.54, 0.32, 0.28, 1)
        _AccentColor ("Accent Color", Color) = (0.82, 0.58, 0.42, 1)
        _RimColor ("Rim Color", Color) = (0.24, 0.68, 0.72, 1)
        _SubsurfaceColor ("Subsurface Color", Color) = (0.94, 0.62, 0.48, 1)
        _BiolumColor ("Biolum Color", Color) = (0.26, 0.95, 0.84, 1)
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
        _ThicknessStrength ("Thickness Strength", Range(0, 2)) = 0.52
        _SpecularNoiseStrength ("Specular Noise Strength", Range(0, 2)) = 0.38
        _CavityStrength ("Cavity Strength", Range(0, 2)) = 0.58
        _CausticStrength ("Caustic Strength", Range(0, 2)) = 0.18
        _CausticScale ("Caustic Scale", Range(0.1, 8)) = 1.6
        _CausticSpeed ("Caustic Speed", Range(0, 4)) = 0.42
        _BiolumStrength ("Biolum Strength", Range(0, 4)) = 0
        _BiolumMaskStrength ("Biolum Mask Strength", Range(0, 2)) = 1
        _BiolumPulseAmplitude ("Biolum Pulse Amplitude", Range(0, 1)) = 0.28
        _BiolumPulseFrequency ("Biolum Pulse Frequency", Range(0, 8)) = 0.58
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
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half _ThicknessStrength;
                half _SpecularNoiseStrength;
                half _CavityStrength;
                half _CausticStrength;
                half _CausticScale;
                half _CausticSpeed;
                half _BiolumStrength;
                half _BiolumMaskStrength;
                half _BiolumPulseAmplitude;
                half _BiolumPulseFrequency;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

            half4 _HectonOceanBiolumColor;
            half _HectonOceanBiolumStrength;
            half4 _HectonFloorBiolumColor;
            half _HectonFloorBiolumStrength;

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
                half4 tangentWS : TEXCOORD6;
                half3 bitangentWS : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.tangentWS = half4(normalize(normalInputs.tangentWS), input.tangentOS.w);
                output.bitangentWS = normalize(normalInputs.bitangentWS);
                output.color = input.color;
                output.uv = input.uv;
                output.viewDirWS = SafeNormalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 baseNormalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = normalize(input.bitangentWS);
                Light mainLight = GetMainLight();
                half tintMask = saturate(input.color.r) * _VertexTintStrength;
                half moisture = saturate(input.color.g);
                half age = saturate(input.color.b);

                half3 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float2 detailUv = input.uv * (_CausticScale * 0.82h) + input.positionWS.xz * 0.06 + float2(_Time.y * _CausticSpeed, _Time.y * (_CausticSpeed * 0.61h));
                half detailSample = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, detailUv).r;
                half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half3 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).xyz * 2.0h - 1.0h;
                normalSample.xy *= _NormalStrength;
                normalSample.z = sqrt(saturate(1.0h - dot(normalSample.xy, normalSample.xy)));
                half3 normalWS = normalize(tangentWS * normalSample.x + bitangentWS * normalSample.y + baseNormalWS * normalSample.z);
                half3 lightDir = normalize(mainLight.direction);
                half NdotL = saturate(dot(normalWS, lightDir));
                half backLight = saturate(dot(-normalWS, lightDir));
                half rim = pow(1.0h - saturate(dot(normalWS, normalize(input.viewDirWS))), _RimPower);

                half cavity = saturate(1.0h - maskSample.r * _CavityStrength);
                half thickness = saturate(lerp(maskSample.b, maskSample.a, _ThicknessStrength));
                half glossNoise = lerp(1.0h, maskSample.g, _SpecularNoiseStrength);
                half causticMask = saturate(0.68h + detailSample * _CausticStrength + maskSample.a * 0.18h);
                half pulse = 1.0h + sin(_Time.y * _BiolumPulseFrequency + input.positionWS.x * 0.07h + input.positionWS.z * 0.05h + detailSample * 2.4h) * _BiolumPulseAmplitude;
                half biolumMask = saturate((cavity * 0.42h + maskSample.a * 0.28h + maskSample.b * 0.24h + detailSample * 0.18h) * _BiolumMaskStrength);
                half floorZoneInfluence = saturate(_HectonFloorBiolumStrength);
                half oceanZoneInfluence = saturate(_HectonOceanBiolumStrength * 0.35h);
                half zoneBiolumStrength = saturate(floorZoneInfluence + oceanZoneInfluence);
                half3 zoneBiolumColor = lerp(_BiolumColor.rgb, _HectonFloorBiolumColor.rgb, floorZoneInfluence);
                zoneBiolumColor = lerp(zoneBiolumColor, _HectonOceanBiolumColor.rgb, oceanZoneInfluence);

                half3 accent = lerp(_BaseColor.rgb, _AccentColor.rgb, saturate(maskSample.r + tintMask * 0.48h));
                half3 moistureTint = lerp(half3(1.0h, 1.0h, 1.0h), _AccentColor.rgb, moisture * _MoistureBoost);
                half3 ageTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h - _AgeDarkening, 1.0h - _AgeDarkening, 1.0h - _AgeDarkening), age);
                half3 albedo = accent * baseTex * moistureTint * ageTint;
                albedo *= lerp(1.0h, detailSample, _DetailStrength);
                albedo = lerp(albedo, albedo * 0.78h, cavity * 0.22h);

                half3 ambient = SampleSH(normalWS) * (_AmbientStrength + maskSample.b * 0.08h);
                half3 diffuse = albedo * (ambient + mainLight.color * NdotL);
                half3 subsurface = _SubsurfaceColor.rgb * (backLight * _SubsurfaceStrength * thickness * causticMask);
                half3 rimLighting = _RimColor.rgb * (rim * _RimStrength);
                half specular = pow(NdotL, lerp(10.0h, 42.0h, _Smoothness)) * _Smoothness * 0.22h * glossNoise;
                half3 biolum = zoneBiolumColor * (_BiolumStrength * (1.0h + zoneBiolumStrength * 0.76h) * biolumMask * pulse);

                half3 color = diffuse + subsurface + rimLighting + specular + biolum;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
