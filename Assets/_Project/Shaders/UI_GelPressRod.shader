Shader "TinyFixers/UI/GelPressRod"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 spin:TEXCOORD1; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 spin:TEXCOORD1; float4 world:TEXCOORD2; };
            float4 _ClipRect;
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color*_Color; o.uv=v.uv; o.spin=v.spin; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float x=clamp(i.uv.x*2-1,-0.999,0.999);
                // Cylindrical wrap; the fixed lighting never rotates with the stripe phase.
                float angle=asin(x)/6.2831853;
                float band=frac(i.uv.y*i.spin.y*0.65 + angle - i.spin.x);
                float aa=max(fwidth(band),0.006);
                float stripe=smoothstep(0.02-aa,0.02+aa,band)*(1-smoothstep(0.52-aa,0.52+aa,band));
                float light=0.40+0.60*sqrt(saturate(1-x*x));
                float3 rgb=lerp(float3(0.018,0.025,0.045),float3(1,0.69,0.025),stripe)*light;
                float highlight=exp(-pow((i.uv.x-0.27)*19,2));
                rgb=lerp(rgb,float3(1,0.98,0.87),highlight*0.50);
                float rim=smoothstep(0.025,0.075,min(i.uv.x,1-i.uv.x));
                rgb=lerp(float3(0.10,0.025,0.14),rgb,rim);
                fixed4 c=fixed4(rgb,1)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
