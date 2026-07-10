Shader "DeliveryGame/NavigationArrowGlow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (0.06, 3.2, 0.22, 0.5)
        _GlowWidth ("Glow Width", Range(0.005, 0.2)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ArrowGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Front

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
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half _GlowWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _GlowWidth;
                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _GlowColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
