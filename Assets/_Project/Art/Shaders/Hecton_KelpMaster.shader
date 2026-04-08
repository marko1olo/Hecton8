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
        _SwayAmplitude ("Sway Amplitude", Range(0, 0.5)) = 0.08
        _SwayFrequency ("Sway Frequency", Range(0, 8)) = 1.8
        _SwaySpeed ("Sway Speed", Range(0, 4)) = 0.9
        _SwayPhaseScale ("Sway Phase Scale", Range(0, 4)) = 0.75
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
                half4 _TipColor;
                half4 _RimColor;
                half4 _TransmissionColor;
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
                half _SwayAmplitude;
                half _SwayFrequency;
                half _SwaySpeed;
                half _SwayPhaseScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);

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

                float3 positionOS = input.positionOS.xyz;
                half heightMask = saturate(input.uv.y);
                half swayPhase = (positionOS.x + positionOS.z) * _SwayPhaseScale + input.color.r * 2.1h;
                half swayWave = sin(_Time.y * _SwaySpeed + swayPhase + positionOS.y * _SwayFrequency);
                positionOS.xz += input.normalOS.xz * (swayWave * _SwayAmplitude * heightMask);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(positionOS);
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
                half heightMask = saturate(input.uv.y);
                half widthMask = saturate(input.uv.x);
                half centerDistance = abs(widthMask - 0.5h) * 2.0h;
                half midribMask = saturate(1.0h - centerDistance * centerDistance * 6.0h);
                half edgeMask = saturate((centerDistance - 0.24h) / 0.76h);

                half3 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float2 causticUv = input.uv * _CausticScale + input.positionWS.xz * 0.08 + float2(_Time.y * _CausticSpeed, _Time.y * (_CausticSpeed * 0.73));
                half detailSample = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, causticUv).r;
                half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half3 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).xyz * 2.0h - 1.0h;
                normalSample.xy *= _NormalStrength;
                half curveSigned = (widthMask - 0.5h) * 2.0h;
                normalSample.x += curveSigned * edgeMask * _BladeCurveNormalStrength;
                normalSample.y += midribMask * (_BladeCurveNormalStrength * 0.18h);
                normalSample.z = sqrt(saturate(1.0h - dot(normalSample.xy, normalSample.xy)));
                half3 normalWS = normalize(tangentWS * normalSample.x + bitangentWS * normalSample.y + baseNormalWS * normalSample.z);
                half3 lightDir = normalize(mainLight.direction);
                half NdotL = saturate(dot(normalWS, lightDir));
                half backLight = saturate(dot(-normalWS, lightDir));
                half rim = pow(1.0h - saturate(dot(normalWS, normalize(input.viewDirWS))), _RimPower);
                half detailMask = lerp(1.0h, detailSample, saturate(_DetailStrength + edgeMask * _EdgeDetailBoost));
                half causticMask = saturate(0.65h + detailSample * _CausticStrength + maskSample.a * 0.2h + edgeMask * 0.08h);
                half thicknessMask = saturate(lerp(heightMask, maskSample.r, _ThicknessStrength) + edgeMask * _EdgeTransmissionBoost * 0.18h);
                half glossNoise = lerp(1.0h, maskSample.g, _SpecularNoiseStrength);
                half glossMask = saturate(glossNoise + midribMask * _MidribGlossBoost - edgeMask * (_EdgeWearDarkening * 0.22h));

                half3 gradient = lerp(_BaseColor.rgb, _TipColor.rgb, heightMask);
                half3 moistureTint = lerp(half3(1.0h, 1.0h, 1.0h), _TipColor.rgb, moisture * _MoistureBoost);
                half3 ageTint = lerp(half3(1.0h, 1.0h, 1.0h), half3(1.0h - _AgeDarkening, 1.0h - _AgeDarkening, 1.0h - _AgeDarkening), age);
                half3 albedo = gradient * baseTex * moistureTint * ageTint * detailMask;
                albedo = lerp(albedo, albedo * half3(1.12h, 1.08h, 0.92h), tintMask + maskSample.b * 0.08h);
                albedo *= (1.0h - midribMask * _MidribDarkening);
                albedo *= lerp(1.0h, 1.0h - _EdgeWearDarkening, edgeMask);

                half3 ambient = SampleSH(normalWS) * (_AmbientStrength + maskSample.b * 0.06h);
                half3 diffuse = albedo * (ambient + mainLight.color * NdotL);
                half3 transmission = _TransmissionColor.rgb * (backLight * _TransmissionStrength * thicknessMask * causticMask);
                half3 rimLighting = _RimColor.rgb * (rim * _RimStrength);
                half specular = pow(NdotL, lerp(12.0h, 48.0h, _Smoothness)) * _Smoothness * 0.18h * glossMask;

                half3 color = diffuse + transmission + rimLighting + specular;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
