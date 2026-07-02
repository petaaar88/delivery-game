Shader "DeliveryGame/DeliveryZoneRing"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 1.0, 0.4, 1.0)
        _Intensity ("Intensity", Float) = 1.5
        _EdgeWidth ("Edge Ring Width", Range(0.005, 0.2)) = 0.045
        _PulseSpeed ("Pulse Speed", Float) = 0.45
        _PulseWidth ("Pulse Width", Range(0.01, 0.4)) = 0.12
        _DashCount ("Dash Count", Float) = 14
        _DashSpeed ("Dash Rotate Speed", Float) = 0.06
        _FillStrength ("Center Fill", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeWidth;
                half _PulseSpeed;
                half _PulseWidth;
                half _DashCount;
                half _DashSpeed;
                half _FillStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half PulseRing(float r, float phase)
            {
                half band = 1.0 - smoothstep(0.0, _PulseWidth, abs(r - phase));
                half fade = (1.0 - phase) * (1.0 - phase);
                return band * fade;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 p = IN.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x) / 6.2831853 + 0.5;

                // hard cut just inside the quad edge
                half mask = 1.0 - smoothstep(0.97, 1.0, r);

                // bright outer rim
                half rim = 1.0 - smoothstep(0.0, _EdgeWidth, abs(r - 0.94));

                // rotating dashed inner ring
                half dash = step(0.5, frac(ang * _DashCount + _Time.y * _DashSpeed * _DashCount));
                half dashRing = (1.0 - smoothstep(0.0, _EdgeWidth * 1.4, abs(r - 0.78))) * dash * 0.8;

                // expanding radar pulses, half a cycle apart
                float t = _Time.y * _PulseSpeed;
                half pulse = PulseRing(r, frac(t)) + PulseRing(r, frac(t + 0.5));

                // soft breathing center fill
                half breathe = 0.8 + 0.2 * sin(_Time.y * 2.0);
                half fill = (1.0 - r) * (1.0 - r) * _FillStrength * breathe;

                half glow = rim + dashRing + pulse * 0.9 + fill;
                half alpha = saturate(glow) * mask * _Color.a;
                return half4(_Color.rgb * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
