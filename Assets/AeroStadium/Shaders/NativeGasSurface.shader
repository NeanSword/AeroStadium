// Gastly-only translucent adaptation. Native RGB mixing and UV animation stay intact.
// Gas opacity is an AeroStadium presentation setting, not decoded native shader behavior.
Shader "AeroStadium/NativeGasSurface"
{
    Properties
    {
        _Blend0Tex("Texture principale",2D)="black" {}
        _Blend1Tex("Texture secondaire",2D)="black" {}
        _LerpTex("Mélange",2D)="black" {}
        _BaseColor("Couleur principale",Color)=(.533808589,.439688742,.565728784,1)
        _LayerColor("Couleur secondaire",Color)=(.398298472,.284074634,.436629802,1)
        _ConstantColor0("Couleur constante",Color)=(1,1,1,1)
        _FixMultiplierColor("Multiplicateur",Color)=(1,1,1,1)
        _ConstantColorVal("Couleur constante active",Range(0,1))=0
        _Blend0UVTranslateU("U0",Float)=0
        _Blend0UVTranslateV("V0",Float)=0
        _Blend1UVTranslateU("U1",Float)=0
        _Blend1UVTranslateV("V1",Float)=0
        _Blend0UVScaleU("Échelle U0",Float)=1
        _Blend0UVScaleV("Échelle V0",Float)=1
        _Blend1UVScaleU("Échelle U1",Float)=1
        _Blend1UVScaleV("Échelle V1",Float)=1
        _TransU("Translation U",Float)=0
        _TransV("Translation V",Float)=0
        _GasOpacity("Opacité du gaz",Range(0,1))=.85
        _GasDensityFloor("Densité minimale du gaz",Range(0,1))=.55
        _GasEdgeSoftness("Douceur des contours",Range(.01,1))=.45
        _Stencil("Référence",Float)=1
        _ZTest("Test profondeur",Float)=4
        _CullMode("Faces",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull [_CullMode]
            // The opaque source _ZWrite must never overwrite the gas adaptation.
            ZWrite Off
            ZTest [_ZTest]
            // Keep the animated native mask silhouette; body/eyes write their own depth.
            Stencil { Ref [_Stencil] ReadMask 255 WriteMask 0 Comp Equal Pass Keep }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Blend0Tex); SAMPLER(sampler_Blend0Tex);
            TEXTURE2D(_Blend1Tex); SAMPLER(sampler_Blend1Tex);
            TEXTURE2D(_LerpTex); SAMPLER(sampler_LerpTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Blend0Tex_ST, _Blend1Tex_ST, _LerpTex_ST, _BaseColor, _LayerColor, _ConstantColor0, _FixMultiplierColor;
            float _Blend0UVTranslateU,_Blend0UVTranslateV,_Blend1UVTranslateU,_Blend1UVTranslateV;
            float _Blend0UVScaleU,_Blend0UVScaleV,_Blend1UVScaleU,_Blend1UVScaleV;
            float _ConstantColorVal,_TransU,_TransV;
            float _GasOpacity,_GasDensityFloor,_GasEdgeSoftness;
            CBUFFER_END
            // Smooth source normals are used only for alpha, never lighting or hue.
            // No tangent attribute or tangent-space data is consumed.
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD2; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; float3 positionWS:TEXCOORD2; half3 normalWS:TEXCOORD3; };
            V Vert(A a)
            {
                V v;
                v.positionWS=TransformObjectToWorld(a.positionOS.xyz);
                v.positionCS=TransformWorldToHClip(v.positionWS);
                v.normalWS=TransformObjectToWorldNormal(a.normalOS);
                v.uv=a.uv;
                v.fog=ComputeFogFactor(v.positionCS.z);
                return v;
            }
            half4 Frag(V v):SV_Target
            {
                float2 uv=v.uv+float2(_TransU,_TransV);
                float2 u0=uv*_Blend0Tex_ST.xy*float2(_Blend0UVScaleU,_Blend0UVScaleV)+_Blend0Tex_ST.zw+float2(_Blend0UVTranslateU,_Blend0UVTranslateV);
                float2 u1=uv*_Blend1Tex_ST.xy*float2(_Blend1UVScaleU,_Blend1UVScaleV)+_Blend1Tex_ST.zw+float2(_Blend1UVTranslateU,_Blend1UVTranslateV);
                half a=SAMPLE_TEXTURE2D(_Blend0Tex,sampler_Blend0Tex,u0).r;
                half b=SAMPLE_TEXTURE2D(_Blend1Tex,sampler_Blend1Tex,u1).r;
                half mask=SAMPLE_TEXTURE2D(_LerpTex,sampler_LerpTex,uv*_LerpTex_ST.xy+_LerpTex_ST.zw).r;
                half amount=saturate(lerp(a,b,mask));
                half3 color=lerp(_BaseColor.rgb,_LayerColor.rgb,amount);
                // Identical source color formula to NativeEffectCore.
                color*=lerp(half3(1,1,1),_ConstantColor0.rgb,saturate(_ConstantColorVal))*_FixMultiplierColor.rgb;

                half facing=saturate(abs(dot(normalize(v.normalWS),GetWorldSpaceNormalizeViewDir(v.positionWS))));
                half edge=smoothstep(0,max(_GasEdgeSoftness,.001),facing);
                // Reuse native animated intensity as density; do not multiply RGB by opacity.
                half density=lerp(saturate(_GasDensityFloor),1,amount);
                half alpha=saturate(_GasOpacity)*density*edge;
                return half4(MixFog(color,v.fog),alpha);
            }
            ENDHLSL
        }
    }
}
