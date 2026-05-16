Shader "Hecton8/WFC/LaserDoorClip"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.22, 0.24, 0.24, 1)
        _MoltenColor ("Molten Edge", Color) = (1.0, 0.38, 0.08, 1)
        _EdgeWidth ("Molten Edge Width", Range(0.005, 0.2)) = 0.045
        _EmissionGain ("Emission Gain", Range(0, 8)) = 2.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _MoltenColor;
                half _EdgeWidth;
                half _EmissionGain;
            CBUFFER_END

            float4 _WfcLaserCutSphereWS;
            float _WfcLaserCutProgress01;
            float _WfcLaserCutHeat01;
            float _WfcLaserCutMolten01;
            float _WfcLaserCutOverkill01;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half3 SafeNormalizeHalf(half3 value)
            {
                float3 safeValue = (float3)value;
                float lengthSq = max(dot(safeValue, safeValue), 0.000001);
                return (half3)(safeValue * rsqrt(lengthSq));
            }

            float TriangleGrain(float3 positionWS)
            {
                float grain = frac(dot(positionWS, float3(17.131, 59.371, 101.113)));
                return abs(grain * 2.0 - 1.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float progress = saturate(_WfcLaserCutProgress01);
                float radius = max(0.0, _WfcLaserCutSphereWS.w) * progress;
                float dist = distance(input.positionWS, _WfcLaserCutSphereWS.xyz);
                clip(dist - radius);

                half3 normalWS = SafeNormalizeHalf(input.normalWS);
                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 lit = _BaseColor.rgb * ((half)0.18 + ndotl * mainLight.color);

                float edgeWidth = max((float)_EdgeWidth, 0.001);
                float shellDistance = abs(dist - radius);
                half edge01 = (half)(1.0 - saturate(shellDistance / edgeWidth));
                half heat01 = (half)saturate(_WfcLaserCutHeat01);
                half molten01 = (half)saturate(_WfcLaserCutMolten01);
                half overkill01 = (half)saturate(_WfcLaserCutOverkill01);
                half grain01 = (half)TriangleGrain(input.positionWS);
                half crystalBand = (half)(1.0 - saturate(shellDistance / max(edgeWidth * 3.0, 0.001))) * overkill01;
                half edgeEnergy = edge01 * ((half)0.5 + heat01 * (half)0.5);
                half overkillEnergy = crystalBand * ((half)0.35 + grain01 * (half)0.65) * ((half)0.75 + heat01);
                half3 molten = _MoltenColor.rgb * (edgeEnergy + overkillEnergy) * (half)_EmissionGain * molten01;

                return half4(lit + molten, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
