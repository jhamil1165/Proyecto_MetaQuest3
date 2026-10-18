// Círculo que marca dónde apunta la mano o el mando, en lugar del rayo láser.
// Se dibuja con matemáticas sobre las coordenadas del quad (sin textura) y siempre por
// encima de lo que toca, para que no se esconda medio metido en un panel.
Shader "MedicalViewer/Reticle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.95)
        _Radius ("Radio del aro (0-0.5)", Range(0.05, 0.5)) = 0.42
        _Thickness ("Grosor del aro", Range(0.01, 0.3)) = 0.09
        _Fill ("Relleno del centro", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Radius;
                float _Thickness;
                float _Fill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv - 0.5;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = length(IN.uv);
                float aa = fwidth(r) * 1.5;

                // Aro: distancia al radio menor que medio grosor, con borde suavizado.
                float ring = 1 - smoothstep(_Thickness * 0.5 - aa, _Thickness * 0.5 + aa, abs(r - _Radius));
                // Velo suave dentro del aro, para que se lea sobre imágenes claras.
                float inside = (1 - smoothstep(_Radius - aa, _Radius + aa, r)) * _Fill;

                float a = max(ring, inside) * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
