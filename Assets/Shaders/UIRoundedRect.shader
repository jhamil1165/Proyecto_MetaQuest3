Shader "MedicalViewer/UIRoundedRect"
{
    // Rectángulo redondeado por SDF para Unity UI, con borde fino opcional.
    //
    // El sprite redondeado que trae Unity tiene un radio fijo y pequeño que se
    // deforma al escalar el panel. Aquí el radio se calcula en píxeles reales del
    // rect, así que se ve igual de nítido sea cual sea el tamaño del elemento.
    //
    // El tamaño del rect no se puede pasar por material (uno solo, compartido por
    // muchos elementos), así que lo inyecta el componente UIRoundedRect en TEXCOORD1.
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _BorderColor("Border Color", Color) = (0,0,0,0)
        _BorderWidth("Border Width (px)", Float) = 0
        _Softness("Edge Softness (px)", Float) = 1.5

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 rectInfo : TEXCOORD1; // xy = tamaño del rect en px, z = radio
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                float4 rectInfo : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _BorderColor;
            float _BorderWidth;
            float _Softness;
            float4 _ClipRect;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPos = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.rectInfo = IN.rectInfo;
                return OUT;
            }

            // Distancia con signo a un rectángulo redondeado.
            float sdRoundedBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - halfSize + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 size = max(IN.rectInfo.xy, float2(1.0, 1.0));
                float radius = min(IN.rectInfo.z, min(size.x, size.y) * 0.5);

                // De UV (0..1) a píxeles relativos al centro del rect.
                float2 p = (IN.texcoord - 0.5) * size;
                float d = sdRoundedBox(p, size * 0.5, radius);

                float aa = max(_Softness, 0.0001);
                float fill = 1.0 - smoothstep(-aa, aa, d);

                fixed4 col = tex2D(_MainTex, IN.texcoord) * IN.color;
                col.a *= fill;

                if (_BorderWidth > 0.0 && _BorderColor.a > 0.0)
                {
                    // Anillo entre el borde exterior y _BorderWidth hacia dentro.
                    float inner = 1.0 - smoothstep(-aa, aa, d + _BorderWidth);
                    float ring = saturate(fill - inner);
                    col.rgb = lerp(col.rgb, _BorderColor.rgb, ring * _BorderColor.a);
                    col.a = max(col.a, ring * _BorderColor.a * IN.color.a);
                }

                col.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);
                clip(col.a - 0.001);
                return col;
            }
            ENDCG
        }
    }
}
