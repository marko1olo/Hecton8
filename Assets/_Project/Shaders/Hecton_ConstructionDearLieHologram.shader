Shader "Hecton8/Construction/DearLieHologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 1.0, 0.72, 0.72)
        _H8SnapDampen ("Snap Dampen", Float) = 0.0
        _H8SnapWiggleSpeed ("Snap Wiggle Speed", Float) = 18.0
        _H8GlobalQualityWeight ("Global Quality Weight", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DearLie"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _H8SnapDampen;
                float _H8SnapWiggleSpeed;
                float _H8GlobalQualityWeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float q = saturate(_H8GlobalQualityWeight);
                float smoothQ = q * q * (3.0 - 2.0 * q);
                float phase = dot(input.positionOS.xyz, float3(19.0, 31.0, 43.0)) + (_Time.y * _H8SnapWiggleSpeed);
                float wave = sin(phase);
                float3 normal = normalize(input.normalOS + float3(0.0001, 0.0001, 0.0001));
                float amplitude = max(0.0, _H8SnapDampen) * lerp(0.25, 1.0, smoothQ);
                float3 positionOS = input.positionOS.xyz - normal * amplitude + normal * wave * amplitude * 0.35;
                output.positionHCS = TransformObjectToHClip(positionOS);
                output.color = _BaseColor;
                output.color.a *= lerp(0.55h, 0.88h, smoothQ);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Unlit/Color"
}
