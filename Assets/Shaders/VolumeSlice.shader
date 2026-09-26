// El corte del TAC dibujado sobre el propio plano de corte.
//
// En vez de pegar una imagen PNG en un cuadrado y pelearse con recortarla y alinearla,
// cada píxel del plano mira dónde cae dentro del volumen y lee ahí la tomografía. Así el
// corte sale siempre alineado con lo que se está cortando, porque sale de los mismos datos,
// y cambia solo al mover el plano.
//
// El fondo negro desaparece por el mismo camino: lo que está fuera del cuerpo es aire
// (menos de -300 HU) y se descarta, de modo que se ve el corte del paciente flotando y no
// una lámina negra.
Shader "MedicalViewer/VolumeSlice"
{
    Properties
    {
        _Density ("Tomografía (3D)", 3D) = "" {}
        _Organs ("Órganos (3D)", 3D) = "" {}

        _WindowCenter ("Centro de ventana (HU)", Float) = 40
        _WindowWidth ("Ancho de ventana (HU)", Float) = 400
        _AirThreshold ("Aire (HU): por debajo no se dibuja", Float) = -300
        _OrganTint ("Color de los órganos", Range(0, 1)) = 0.55

        _ColorLiver ("Hígado", Color) = (0.80, 0.47, 0.37, 1)
        _ColorStomach ("Estómago", Color) = (0.80, 0.55, 0.76, 1)
        _ColorPancreas ("Páncreas", Color) = (0.43, 0.63, 0.82, 1)
        _ColorGallbladder ("Vesícula", Color) = (0.94, 0.59, 0.16, 1)
        _ColorBone ("Hueso", Color) = (0.95, 0.93, 0.88, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Slice"
            Tags { "LightMode" = "UniversalForward" }

            // Se ve por los dos lados: el usuario puede mirar el corte desde arriba o desde abajo.
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE3D(_Density);
            SAMPLER(sampler_Density);
            TEXTURE3D(_Organs);
            SAMPLER(sampler_Organs);

            CBUFFER_START(UnityPerMaterial)
                float _WindowCenter;
                float _WindowWidth;
                float _AirThreshold;
                float _OrganTint;
                float4 _ColorLiver;
                float4 _ColorStomach;
                float4 _ColorPancreas;
                float4 _ColorGallbladder;
                float4 _ColorBone;
                float4x4 _VolumeWorldToObject;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            // Misma conversión que el render de volumen, para que los dos lean igual.
            float3 ToUVW(float3 p)
            {
                return float3(p.x + 0.5, p.z + 0.5, 0.5 - p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Dónde cae este píxel dentro de la caja del volumen.
                float3 inVolume = mul(_VolumeWorldToObject, float4(IN.positionWS, 1.0)).xyz;
                float3 uvw = ToUVW(inVolume);

                // Fuera de la tomografía no hay nada que enseñar.
                if (any(uvw < 0.0) || any(uvw > 1.0)) discard;

                float density = SAMPLE_TEXTURE3D(_Density, sampler_Density, uvw).r;
                float hu = density * 2000.0 - 1000.0;

                // El aire de alrededor del paciente se descarta: ese es el "fondo negro".
                if (hu < _AirThreshold) discard;

                float low = _WindowCenter - _WindowWidth * 0.5;
                float high = _WindowCenter + _WindowWidth * 0.5;
                float gray = saturate((hu - low) / max(1e-3, high - low));

                float3 color = gray.xxx;

                float organ = SAMPLE_TEXTURE3D(_Organs, sampler_Organs, uvw).r * 255.0;
                if (organ > 0.5)
                {
                    float4 tint = organ > 4.5 ? _ColorBone
                                : organ < 1.5 ? _ColorLiver
                                : organ < 2.5 ? _ColorStomach
                                : organ < 3.5 ? _ColorPancreas
                                : _ColorGallbladder;

                    // Se tiñe sin tapar: el gris del TAC se sigue viendo debajo del color.
                    color = lerp(color, tint.rgb * (0.4 + 0.6 * gray), _OrganTint);
                }

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
