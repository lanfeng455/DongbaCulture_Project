Shader "Dongba/TreeGroundRange" {
Properties { [PerRendererData] _MainTex("Sprite",2D)="white" {} _InkColor("Soft green ink",Color)=(.25,.42,.12,.64) }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
fixed4 _InkColor; float _Decline; float _Vitality;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
fixed4 frag(v2f i):SV_Target {
 float2 p=(i.uv-.5)*2;float angle=atan2(p.y,p.x);
 float radius=length(p)/(1+.045*sin(angle*5+i.world.x*.13)+.025*cos(angle*9+i.world.z*.17));
 float wash=exp(-3.3*radius*radius)*(1-smoothstep(.72,1,radius));
 float grain=.94+.04*sin(i.world.x*29+i.world.z*17)*sin(i.world.z*37-i.world.x*13);
 // Ground-only decorative wash; preserve the blue river and its bank transition.
 float bank=abs(i.world.x-(2+sin(i.world.z*.18)*2.05));
 fixed3 tint=lerp(_InkColor.rgb,fixed3(.42,.40,.35),_Decline);
 float opacity=lerp(_InkColor.a,.10,_Decline)*_Vitality;
 return fixed4(tint,opacity*wash*grain*smoothstep(1.12,1.50,bank));
}
ENDCG }
} }



