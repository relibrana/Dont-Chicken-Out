Shader "Custom/MultiColorSwap_IndependentTint_V6"
{
    Properties
    {
        [PerRendererData]_MainTex ("Texture", 2D) = "white" {}
        _Color ("Global Tint", Color) = (1,1,1,1)
        [Toggle]_ColorReplacementToggle ("Enable Color Replacement", Float) = 1

        [Header(Color Swap 1)]
        _OriginalColor1 ("Original Color 1", Color) = (1,0,0,1)
        _ReplacementColor1 ("Replacement Color 1", Color) = (0,1,0,1)
        _Range1 ("Tolerance Range 1", Range(0.001, 0.5)) = 0.1

        [Header(Color Swap 2)]
        _OriginalColor2 ("Original Color 2", Color) = (0,0,1,1)
        _ReplacementColor2 ("Replacement Color 2", Color) = (1,1,0,1)
        _Range2 ("Tolerance Range 2", Range(0.001, 0.5)) = 0.1

        [Header(Color Swap 3)]
        _OriginalColor3 ("Original Color 3", Color) = (0,1,1,1)
        _ReplacementColor3 ("Replacement Color 3", Color) = (1,0,1,1)
        _Range3 ("Tolerance Range 3", Range(0.001, 0.5)) = 0.1

        [Header(Color Swap 4)]
        _OriginalColor4 ("Original Color 4", Color) = (1,0,1,1)
        _ReplacementColor4 ("Replacement Color 4", Color) = (0,0,0,1)
        _Range4 ("Tolerance Range 4", Range(0.001, 0.5)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // SpriteRenderer Color
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 tint : COLOR; // Color combinado
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ColorReplacementToggle;

            float4 _OriginalColor1, _ReplacementColor1;
            float _Range1;
            float4 _OriginalColor2, _ReplacementColor2;
            float _Range2;
            float4 _OriginalColor3, _ReplacementColor3;
            float _Range3;
            float4 _OriginalColor4, _ReplacementColor4;
            float _Range4;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.tint = v.color * _Color; // se aplica después de la lógica
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // tomamos el color original de la textura SIN tint
                fixed4 texColor = tex2D(_MainTex, i.uv);
                if (texColor.a == 0)
                    return fixed4(0,0,0,0);

                fixed3 resultColor = texColor.rgb;

                // Solo procesamos si el reemplazo está activo
                if (_ColorReplacementToggle == 1)
                {
                    float3 baseColor = texColor.rgb;

                    if (distance(baseColor, _OriginalColor1.rgb) < _Range1)
                        resultColor = _ReplacementColor1.rgb;
                    else if (distance(baseColor, _OriginalColor2.rgb) < _Range2)
                        resultColor = _ReplacementColor2.rgb;
                    else if (distance(baseColor, _OriginalColor3.rgb) < _Range3)
                        resultColor = _ReplacementColor3.rgb;
                    else if (distance(baseColor, _OriginalColor4.rgb) < _Range4)
                        resultColor = _ReplacementColor4.rgb;
                }

                fixed4 finalColor;
                finalColor.rgb = resultColor * i.tint.rgb;
                finalColor.a = texColor.a * i.tint.a;

                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "Sprites/Default"
}
