Shader "AeroStadium/StadiumSurface"
{
 Properties { _MainTex("Albedo",2D)="white"{} _NormalTex("Relief",2D)="bump"{} _BaseColor("Color",Color)=(1,1,1,1) _Relief("Relief strength",Range(0,1))=.28 _SrcBlend("Source blend",Float)=1 _DstBlend("Destination blend",Float)=0 _ZWrite("Depth write",Float)=1 _Cutoff("Alpha cutoff",Float)=.35 }
 SubShader { Tags{"RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline"} Cull Off
 Pass { Name "ForwardLit" Tags{"LightMode"="UniversalForward"} Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite]
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);TEXTURE2D(_NormalTex);SAMPLER(sampler_NormalTex);
 CBUFFER_START(UnityPerMaterial) float4 _BaseColor;float _Relief,_Cutoff; CBUFFER_END
 struct A {float4 position:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct V {float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 tangent:TEXCOORD2;float3 bitangent:TEXCOORD3;float2 uv:TEXCOORD4;float4 color:COLOR;float fog:TEXCOORD5;};
 V Vert(A i){V o;VertexPositionInputs p=GetVertexPositionInputs(i.position.xyz);VertexNormalInputs n=GetVertexNormalInputs(i.normal,i.tangent);o.position=p.positionCS;o.world=p.positionWS;o.normal=n.normalWS;o.tangent=n.tangentWS;o.bitangent=n.bitangentWS;o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 half4 Frag(V i):SV_Target {half4 albedo=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_BaseColor*i.color;clip(albedo.a-_Cutoff);half3 map=UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,i.uv));map.xy*=_Relief;half3 n=normalize(map.x*i.tangent+map.y*i.bitangent+map.z*i.normal);Light light=GetMainLight(TransformWorldToShadowCoord(i.world));half diffuse=saturate(dot(n,light.direction));half3 lighting=SampleSH(n)*.85+light.color*(.22+.68*diffuse)*light.shadowAttenuation;half3 color=albedo.rgb*lighting;return half4(MixFog(color,i.fog),albedo.a);}
 ENDHLSL }
 }
}
