Shader "MedicalViewer/HoloGlassPanel"
{
    // Minimalist holographic "glass panel": a very translucent tinted body with a
    // thin fresnel rim light around the edges. _HoverGlow is meant to be driven per-
    // instance at runtime (via MaterialPropertyBlock) so buttons can glow on hover
    // without needing a separate material each.
    Properties
    {
        _BaseColor("Base Color (RGBA, low alpha)", Color) = (0.35, 0.85, 1, 0.18)
        _RimColor("Rim Color", Color) = (0.6, 0.95, 1, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimIntensity("Rim Intensity", Range(0, 5)) = 1.2
        _HoverColor("Hover / Select Color", Color) = (0.1, 0.45, 0.9, 1)
        _HoverGlow("Hover Glow (runtime)", Range(0, 3)) = 0
        _FadeAlpha("Fade Alpha (runtime, intro anim)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float4 _HoverColor;
                float _RimPower;
                float _RimIntensity;
                float _HoverGlow;
                float _FadeAlpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(vpi.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 v = normalize(IN.viewDirWS);
                float rim = pow(saturate(1.0 - saturate(dot(n, v))), _RimPower);
                float glow = _RimIntensity + _HoverGlow;

                // Mezclar (no sumar) permite bordes mas oscuros que el panel, que es
                // lo unico que se ve sobre un fondo claro.
                half3 rimCol = lerp(_RimColor.rgb, _HoverColor.rgb, saturate(_HoverGlow));

                half4 col = _BaseColor;
                col.rgb = lerp(col.rgb, rimCol, saturate(rim * glow));
                col.a = saturate(_BaseColor.a + rim * glow * 0.35 + _HoverGlow * 0.12);
                col.a *= _FadeAlpha; // driven by MedicalMenuIntro during the appear animation
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
