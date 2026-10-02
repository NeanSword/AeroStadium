Shader "AeroStadium/NativeEffectMask"
{
    Properties
    {
        _Mask0Tex("Masque principal", 2D) = "black" {}
        _Mask1Tex("Masque secondaire", 2D) = "black" {}
        _Mask0UVTranslateU("U0", Float) = 0
        _Mask0UVTranslateV("V0", Float) = 0
        _Mask1UVTranslateU("U1", Float) = 0
        _Mask1UVTranslateV("V1", Float) = 0
        _Mask0UVScaleU("Échelle U0", Float) = 1
        _Mask0UVScaleV("Échelle V0", Float) = 1
        _Mask1UVScaleU("Échelle U1", Float) = 1
        _Mask1UVScaleV("Échelle V1", Float) = 1
        MASK_CALC_MODE("Addition / multiplication", Float) = 0
        MASK_FIRST_UV("Canal UV0", Float) = 0
        MASK_SECOND_UV("Canal UV1", Float) = 0
        _SecondMask("Second masque actif", Float) = 1
        _DiscardValue("Seuil", Range(0,1)) = .5
        _Stencil("Référence", Float) = 1
        _ZWrite("Profondeur", Float) = 1
        _ZTest("Test profondeur", Float) = 4
        _CullMode("Faces", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ColorMask 0
            Cull [_CullMode]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Stencil { Ref [_Stencil] ReadMask 127 WriteMask 255 Comp NotEqual Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Mask0Tex); SAMPLER(sampler_Mask0Tex);
            TEXTURE2D(_Mask1Tex); SAMPLER(sampler_Mask1Tex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Mask0Tex_ST, _Mask1Tex_ST;
            float _Mask0UVTranslateU, _Mask0UVTranslateV, _Mask1UVTranslateU, _Mask1UVTranslateV;
            float _Mask0UVScaleU, _Mask0UVScaleV, _Mask1UVScaleU, _Mask1UVScaleV;
            float MASK_CALC_MODE, MASK_FIRST_UV, MASK_SECOND_UV, _SecondMask, _DiscardValue;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv0:TEXCOORD2; float2 uv1:TEXCOORD3; };
            struct V { float4 positionCS:SV_POSITION; float2 uv0:TEXCOORD0; float2 uv1:TEXCOORD1; };
            V Vert(A a) { V v; v.positionCS=TransformObjectToHClip(a.positionOS.xyz); v.uv0=a.uv0; v.uv1=a.uv1; return v; }
            half4 Frag(V v):SV_Target
            {
                float2 a=lerp(v.uv0,v.uv1,step(.5,MASK_FIRST_UV));
                float2 b=lerp(v.uv0,v.uv1,step(.5,MASK_SECOND_UV));
                a=a*_Mask0Tex_ST.xy*float2(_Mask0UVScaleU,_Mask0UVScaleV)+_Mask0Tex_ST.zw+float2(_Mask0UVTranslateU,_Mask0UVTranslateV);
                b=b*_Mask1Tex_ST.xy*float2(_Mask1UVScaleU,_Mask1UVScaleV)+_Mask1Tex_ST.zw+float2(_Mask1UVTranslateU,_Mask1UVTranslateV);
                half m0=SAMPLE_TEXTURE2D(_Mask0Tex,sampler_Mask0Tex,a).r;
                half m1=SAMPLE_TEXTURE2D(_Mask1Tex,sampler_Mask1Tex,b).r;
                half combined=lerp(saturate(m0+m1),m0*m1,step(.5,MASK_CALC_MODE));
                clip(lerp(m0,combined,_SecondMask)-_DiscardValue);
                return 0;
            }
            ENDHLSL
        }
    }
}
