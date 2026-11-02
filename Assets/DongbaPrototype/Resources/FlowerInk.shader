Shader "Dongba/FlowerInk" {
Properties { [PerRendererData] _MainTex("Original flower alpha",2D)="white" {} [HideInInspector] _RendererColor("Renderer color",Color)=(1,1,1,1) _InkColor("Pink ink",Color)=(.82,.34,.49,1) }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; fixed4 _RendererColor; fixed4 _InkColor;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
fixed4 frag(v2f i):SV_Target {return fixed4(_InkColor.rgb,tex2D(_MainTex,i.uv).a*i.color.a*_RendererColor.a);}
ENDCG }
} }
