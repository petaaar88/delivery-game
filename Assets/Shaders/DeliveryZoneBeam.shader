Shader "DeliveryGame/DeliveryZoneBeam"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 1.0, 0.4, 1.0)
        _Intensity ("Intensity", Float) = 1.0
        _FadePower ("Height Fade Power", Range(0.5, 6)) = 2.2
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _BaseAlpha ("Base Alpha", Range(0, 1)) = 0.08
        _ScrollSpeed ("Scroll Speed", Float) = 0.5
        _BandCount ("Band Count", Float) = 3
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
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 heightAndCap : TEXCOORD2; // x: 0..1 height, y: object-space normal y
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _FadePower;
                half _RimPower;
                half _BaseAlpha;
                half _ScrollSpeed;
                half _BandCount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(positionWS);
                // Unity's cylinder spans y in [-1, 1]
                OUT.heightAndCap = float2(IN.positionOS.y * 0.5 + 0.5, IN.normalOS.y);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // drop the cylinder caps, keep only the side wall
                clip(0.5 - abs(IN.heightAndCap.y));

                half h = saturate(IN.heightAndCap.x);
                half fade = pow(1.0 - h, _FadePower);

                half ndv = abs(dot(normalize(IN.viewDirWS), normalize(IN.normalWS)));
                half rim = pow(1.0 - ndv, _RimPower);

                half band = 0.85 + 0.15 * sin((h * _BandCount - _Time.y * _ScrollSpeed) * 6.2831853);

                half alpha = fade * (_BaseAlpha + rim) * band * _Color.a;
                return half4(_Color.rgb * _Intensity, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
