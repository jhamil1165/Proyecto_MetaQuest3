// Render de volumen para el Meta Quest: dibuja la tomografía entera dentro del visor,
// con los órganos segmentados en color, sin necesidad de cortes planos.
//
// Es el efecto que pidió el profesor (estilo neuronavegador). El repositorio que pasó,
// UnityVolumeRendering, exige Unity 6 y este proyecto es 2022.3, así que está escrito
// aquí desde cero para URP y con las macros de estéreo, para que se vea en los dos ojos.
//
// Cómo funciona: por cada píxel se lanza un rayo que atraviesa el cubo y va sumando lo
// que encuentra (density + etiqueta de órgano), hasta que se llena de opacidad o sale.
Shader "MedicalViewer/VolumeRender"
{
    Properties
    {
        _Density ("Tomografía (3D)", 3D) = "" {}
        _Organs ("Órganos (3D)", 3D) = "" {}

        _WindowCenter ("Centro de ventana (HU)", Float) = 40
        _WindowWidth ("Ancho de ventana (HU)", Float) = 500
        _TissueOpacity ("Opacidad del tejido", Range(0, 1)) = 0.10
        _OrganOpacity ("Opacidad de los órganos", Range(0, 1)) = 0.85
        _Steps ("Pasos del rayo", Range(16, 192)) = 96

        _ColorLiver ("Hígado", Color) = (0.80, 0.47, 0.37, 1)
        _ColorStomach ("Estómago", Color) = (0.80, 0.55, 0.76, 1)
        _ColorPancreas ("Páncreas", Color) = (0.43, 0.63, 0.82, 1)
        _ColorGallbladder ("Vesícula", Color) = (0.94, 0.59, 0.16, 1)

        // Mismos nombres que usa ClippingPlaneController: el mismo plano corta los
        // modelos y el volumen.
        _PlaneNormal ("Normal del plano (mundo)", Vector) = (0, 1, 0, 0)
        _PlaneDistance ("Distancia del plano", Float) = -100000
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Volume"
            Tags { "LightMode" = "UniversalForward" }

            // Se dibuja la cara trasera del cubo: así el volumen sigue viéndose aunque la
            // cabeza del usuario entre dentro.
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                float _TissueOpacity;
                float _OrganOpacity;
                float _Steps;
                float4 _ColorLiver;
                float4 _ColorStomach;
                float4 _ColorPancreas;
                float4 _ColorGallbladder;
                float4 _PlaneNormal;
                float _PlaneDistance;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            // El cubo va de -0.5 a 0.5. La textura guarda x = columna, y = fila y
            // z = corte empezando por la cabeza, así que el eje Y del cubo (arriba) es el
            // que recorre los cortes al revés.
            float3 ToUVW(float3 p)
            {
                return float3(p.x + 0.5, p.z + 0.5, 0.5 - p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 camOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 dir = normalize(IN.positionOS - camOS);

                // Por dónde entra y sale el rayo de la caja.
                float3 inv = 1.0 / (abs(dir) < 1e-5 ? 1e-5 : dir);
                float3 t0 = (-0.5 - camOS) * inv;
                float3 t1 = (0.5 - camOS) * inv;
                float3 tsmall = min(t0, t1);
                float3 tbig = max(t0, t1);
                float tmin = max(max(tsmall.x, tsmall.y), max(tsmall.z, 0.0));
                float tmax = min(tbig.x, min(tbig.y, tbig.z));
                if (tmax <= tmin) discard;

                int steps = (int)_Steps;
                float dt = (tmax - tmin) / steps;
                float3 position = camOS + dir * tmin;
                float3 advance = dir * dt;

                float low = _WindowCenter - _WindowWidth * 0.5;
                float high = _WindowCenter + _WindowWidth * 0.5;

                float4 accumulated = 0;

                [loop]
                for (int i = 0; i < steps; i++)
                {
                    position += advance;

                    // El plano de corte se define en espacio de mundo, igual que en los modelos.
                    float3 world = TransformObjectToWorld(position);
                    if (dot(world, _PlaneNormal.xyz) + _PlaneDistance > 0) continue;

                    float3 uvw = ToUVW(position);
                    float density = SAMPLE_TEXTURE3D_LOD(_Density, sampler_Density, uvw, 0).r;
                    float organ = SAMPLE_TEXTURE3D_LOD(_Organs, sampler_Organs, uvw, 0).r * 255.0;

                    float4 sample;
                    if (organ > 0.5)
                    {
                        float4 color = organ < 1.5 ? _ColorLiver
                                     : organ < 2.5 ? _ColorStomach
                                     : organ < 3.5 ? _ColorPancreas
                                     : _ColorGallbladder;
                        sample = float4(color.rgb, _OrganOpacity);
                    }
                    else
                    {
                        // La densidad guardada va de -1000 a 1000 HU en 0..1.
                        float hu = density * 2000.0 - 1000.0;
                        float gray = saturate((hu - low) / max(1e-3, high - low));

                        // Al cuadrado: el aire y la grasa casi no aportan y el hueso destaca.
                        sample = float4(gray.xxx, gray * gray * _TissueOpacity);
                    }

                    accumulated.rgb += (1.0 - accumulated.a) * sample.a * sample.rgb;
                    accumulated.a += (1.0 - accumulated.a) * sample.a;

                    if (accumulated.a > 0.98) break;
                }

                if (accumulated.a <= 0.002) discard;
                return half4(accumulated.rgb, accumulated.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
