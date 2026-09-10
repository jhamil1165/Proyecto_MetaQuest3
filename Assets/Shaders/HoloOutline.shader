Shader "MedicalViewer/HoloOutline"
{
    // Thin inverted-hull outline for the organ models. The hull is extruded in WORLD
    // space (metres), not object space, so it stays the same thickness no matter how
    // the model is scaled - Heart is at 5000x, estomago at 1x.
    // _OutlineAlpha is driven per-renderer at runtime (MaterialPropertyBlock) so the
    // outline only shows while the object is selected.
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.6, 0.95, 1, 1)
        _OutlineWidth("Outline Width (metres)", Range(0, 0.05)) = 0.004
        _OutlineAlpha("Outline Alpha (runtime)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineAlpha;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                positionWS += normalWS * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a * _OutlineAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
