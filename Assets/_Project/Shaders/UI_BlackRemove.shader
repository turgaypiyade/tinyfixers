Shader "UI/BlackRemoveForVictory"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Brightness ("Brightness", Range(0.4, 1.2)) = 0.82
        _Saturation ("Saturation", Range(0, 1.5)) = 0.82
        _GreenReduce ("Green Reduce", Range(0.4, 1.1)) = 0.82
        _HighlightReduce ("Highlight Reduce", Range(0.3, 1.0)) = 0.65
        _HighlightStart ("Highlight Start", Range(0.3, 1.0)) = 0.62
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float _Brightness;
            float _Saturation;
            float _GreenReduce;
            float _HighlightReduce;
            float _HighlightStart;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord) * i.color;

                // Genel parlaklık
                col.rgb *= _Brightness;

                // Saturation azaltma
                float lum = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(float3(lum, lum, lum), col.rgb, _Saturation);

                // Yeşil kanalını kontrollü düşür
                col.g *= _GreenReduce;

                // Çok parlak alanları bastır
                float maxChannel = max(col.r, max(col.g, col.b));
                float highlightMask = smoothstep(_HighlightStart, 1.0, maxChannel);
                col.rgb = lerp(col.rgb, col.rgb * _HighlightReduce, highlightMask);

                return col;
            }
            ENDCG
        }
    }
}