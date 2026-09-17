// Version URP del efecto "Clipping Plane" original de Ronja Bohringer
// (https://github.com/ronja-tutorials/ShaderTutorials/blob/master/Assets/021_Clipping_Plane/ClippingPlane.shader),
// portado desde Built-in Render Pipeline (surface shader) a Universal Render Pipeline.
// El original sale rosado en este proyecto porque usa "#pragma surface", que URP no sabe compilar.
Shader "APOSE/URP_ClippingPlane"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _CapColor ("Color del corte (cutoff)", Color) = (1, 0, 0, 1)
        _PlaneNormal ("Normal del plano (World Space)", Vector) = (0, 1, 0, 0)
        _PlaneDistance ("Distancia del plano al origen", Float) = 1000
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // Igual que el original: se renderizan las dos caras para poder ver
        // la cara interior (la "tapa" de color) cuando se corta el modelo.
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _CapColor;
                float4 _PlaneNormal;
                float _PlaneDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);

                return OUT;
            }

            half4 frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Distancia con signo del fragmento al plano de corte. Si es positiva,
                // el punto queda "por encima" del plano y se descarta (clip).
                float distanceToPlane = dot(IN.positionWS, _PlaneNormal.xyz) + _PlaneDistance;
                clip(-distanceToPlane);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 albedo = baseTex * _BaseColor;

                // La cara interior de la geometria (isFrontFace == false, gracias a "Cull Off")
                // se pinta plana con _CapColor: es la "tapa" que rellena el hueco del corte.
                if (!isFrontFace)
                {
                    return half4(_CapColor.rgb, 1);
                }

                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(IN.normalWS), mainLight.direction)) * 0.5h + 0.5h;
                half3 lit = albedo.rgb * mainLight.color * NdotL;

                return half4(lit, albedo.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
