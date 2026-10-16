Shader "MedicalViewer/HoloPlatform"
{
    // Disco holográfico que va bajo cada órgano: anillo luminoso en el borde con un
    // relleno interior muy tenue, que se desvanece hacia afuera. Blend aditivo para
    // que sobre passthrough sume luz en vez de oscurecer la habitación real.
    Properties
    {
        _BaseColor("Color", Color) = (0.75, 0.9, 1, 0.55)
        _RingRadius("Ring Radius", Range(0.1, 1)) = 0.78
        _RingWidth("Ring Width", Range(0.01, 0.5)) = 0.09
        _FillAlpha("Inner Fill", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha   // alpha: funciona igual sobre fondo claro u oscuro
        ZWrite Off
        Cull Off             // readable from above and below

        Pass
        {
            Name "Platform"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _RingRadius;
                float _RingWidth;
                float _FillAlpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                float d = length(IN.uv - 0.5) * 2.0; // 0 en el centro, 1 en el borde del quad

                float ring = 1.0 - saturate(abs(d - _RingRadius) / max(_RingWidth, 0.001));
                ring *= ring;

                float fill = saturate(1.0 - d / max(_RingRadius, 0.001)) * _FillAlpha;
                float mask = 1.0 - smoothstep(_RingRadius, 1.0, d);

                float a = saturate(ring + fill) * mask * _BaseColor.a;
                return half4(_BaseColor.rgb, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
