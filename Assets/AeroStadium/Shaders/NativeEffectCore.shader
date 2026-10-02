// Preserve native textures, stencil passes and animated UVs. Color mixing is an URP adaptation.
Shader "AeroStadium/NativeEffectCore"
{
    Properties
    {
        _Blend0Tex("Texture principale",2D)="black" {}
        _Blend1Tex("Texture secondaire",2D)="black" {}
        _LerpTex("Mélange",2D)="black" {}
        _BaseColor("Couleur principale",Color)=(1,.196,.102,1)
        _LayerColor("Couleur secondaire",Color)=(.94,.808,0,1)
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
        _Stencil("Référence",Float)=1
        _ZWrite("Profondeur",Float)=1
        _ZTest("Test profondeur",Float)=4
        _CullMode("Faces",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+2" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_CullMode]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
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
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD2; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            V Vert(A a) { V v; v.positionCS=TransformObjectToHClip(a.positionOS.xyz); v.uv=a.uv; v.fog=ComputeFogFactor(v.positionCS.z); return v; }
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
                color=lerp(color,_ConstantColor0.rgb,saturate(_ConstantColorVal))*_FixMultiplierColor.rgb;
                return half4(MixFog(color,v.fog),1);
            }
            ENDHLSL
        }
    }
}
