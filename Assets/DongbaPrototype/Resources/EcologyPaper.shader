Shader "Dongba/EcologyPaper" {
Properties { _MainTex("Paper fibers",2D)="white" {} _Decline("Decline",Range(0,1))=0 }
SubShader { Tags {"RenderType"="Opaque"} Cull Off
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; float4 _MainTex_ST; float _Decline;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
v2f vert(app v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}
fixed4 frag(v2f i):SV_Target {
 fixed3 paper=tex2D(_MainTex,i.uv).rgb;
 // Shift the paper's base hue, retaining the original fiber contrast and opacity.
 fixed3 warm=fixed3(.945,.904,.811),cool=fixed3(.865,.867,.843);
 return fixed4(saturate(paper+_Decline*(cool-warm)),1);
}
ENDCG }
} }
