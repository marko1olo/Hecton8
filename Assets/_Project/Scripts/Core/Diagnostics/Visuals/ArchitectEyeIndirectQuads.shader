Shader "Hidden/Hecton8/Diagnostics/ArchitectEyeIndirectQuads"
{
    Properties
    {
        _H8EyeGlyphAtlas ("Glyph Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct ArchitectEyeQuadInstance
            {
                float4 CenterHalfX;
                float4 AxisYHalfY;
                float4 Color;
                float4 UvMode;
                float4 Aux;
            };

            StructuredBuffer<ArchitectEyeQuadInstance> _H8EyeQuads;
            sampler2D _H8EyeGlyphAtlas;
            float _H8EyeVisualTier;

            float3 SafeNormalizeAxis(float3 value, float3 fallback)
            {
                float lenSq = dot(value, value);
                if (!(lenSq > 1.0e-6f) || lenSq > 1.0e12f)
                    return fallback;

                return value * rsqrt(lenSq);
            }

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                ArchitectEyeQuadInstance q = _H8EyeQuads[instanceID];
                float mode = q.UvMode.w;
                float2 uvMin = q.UvMode.xy;
                float2 uvMax = float2(q.UvMode.z, q.Aux.w);

                float4 positionCS;
                if (mode > 0.5f && mode < 1.5f)
                {
                    float2 halfSize = float2(q.CenterHalfX.w, q.AxisYHalfY.w);
                    float2 positionXY = q.CenterHalfX.xy + v.vertex.xy * halfSize;
                    positionCS = float4(positionXY, q.CenterHalfX.z, 1.0f);
                }
                else
                {
                    float3 axisX;
                    float3 axisY;
                    if (mode > 1.5f)
                    {
                        axisX = q.Aux.xyz;
                        axisY = q.AxisYHalfY.xyz;
                    }
                    else
                    {
                        axisX = SafeNormalizeAxis(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m02), float3(1.0f, 0.0f, 0.0f));
                        axisY = SafeNormalizeAxis(float3(UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m12), float3(0.0f, 1.0f, 0.0f));
                    }

                    float3 positionWS =
                        q.CenterHalfX.xyz +
                        axisX * (v.vertex.x * q.CenterHalfX.w) +
                        axisY * (v.vertex.y * q.AxisYHalfY.w);
                    positionCS = UnityWorldToClipPos(positionWS);
                }

                v2f o;
                o.positionCS = positionCS;
                o.uv = lerp(uvMin, uvMax, v.uv);
                o.color = q.Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half glyph = tex2D(_H8EyeGlyphAtlas, i.uv).a;
                half2 centered = half2(i.uv) * 2.0 - 1.0;
                half ridge = saturate(1.0 - abs(centered.x) * 0.72 - abs(centered.y) * 0.38);
                half pomFake = saturate(glyph + ridge * min((half)_H8EyeVisualTier, 3.0) * 0.10);
                half sssFake = saturate(dot(i.color.rgb, half3(0.2126, 0.7152, 0.0722)) + ridge * 0.22);
                half dither = frac((i.positionCS.x * 0.06711056 + i.positionCS.y * 0.00583715) * 52.9829189);
                half alpha = i.color.a * max(pomFake, 0.30 + dither * 0.08);
                half3 rgb = lerp(i.color.rgb, i.color.rgb + sssFake * 0.18, saturate((half)_H8EyeVisualTier * 0.25));
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
