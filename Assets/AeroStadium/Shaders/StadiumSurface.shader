Shader "AeroStadium/StadiumSurface"
{
    Properties
    {
        _MainTex("Albedo",2D)="white"{}
        _SecondaryTex("Native second tile",2D)="white"{}
        _NormalTex("Relief",2D)="bump"{}
        _BaseColor("Color",Color)=(1,1,1,1)
        _Relief("Relief strength",Range(0,1))=.28
        _NativeSampler("Source texel addressing",Float)=0
        _SamplerMask("Mask bits",Vector)=(0,0,0,0)
        _SamplerClamp("Clamp extent / texture size",Vector)=(1,1,1,1)
        _SamplerOrigin("Native tile origin",Vector)=(0,0,0,0)
        _IntensityAlpha("Native I texture alpha: primary / secondary",Vector)=(0,0,0,0)
        _SecondaryNativeSampler("Source second tile addressing",Float)=0
        _SecondarySamplerMask("Second mask bits",Vector)=(0,0,0,0)
        _SecondarySamplerClamp("Second clamp extent / texture size",Vector)=(1,1,1,1)
        _SecondarySamplerOrigin("Second native tile origin",Vector)=(0,0,0,0)
        _AlphaCompareDisabled("Native alpha compare disabled",Float)=0
        _NativeMaterial("Native combiner",Float)=0
        _NativeCycles("Native cycles",Float)=1
        _PrimitiveColor("Native primitive",Vector)=(1,1,1,1)
        _EnvironmentColor("Native environment",Vector)=(1,1,1,1)
        _PrimitiveLOD("Native primitive LOD",Float)=0
        _ColorMux0("Color cycle zero",Vector)=(1,31,4,31)
        _ColorMux1("Color cycle one",Vector)=(31,31,31,0)
        _AlphaMux0("Alpha cycle zero",Vector)=(7,7,7,1)
        _AlphaMux1("Alpha cycle one",Vector)=(7,7,7,0)
        _SecondaryUV("Second UV scale",Vector)=(1,1,0,0)
        _Wrap("Primary U,V / second U,V",Vector)=(0,0,0,0)
        _CoverageAlpha("Opaque coverage",Float)=0
        _SrcBlend("Source blend",Float)=1
        _DstBlend("Destination blend",Float)=0
        _ZWrite("Depth write",Float)=1
        _Cutoff("Alpha cutoff",Float)=.35
        _OffsetFactor("Decal slope bias",Float)=0
        _OffsetUnits("Decal depth bias",Float)=0
        _Cull("Cull mode",Float)=0
    }
    SubShader
    {
        Tags{"RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode"="UniversalForward"}
            Cull [_Cull] Blend [_SrcBlend] [_DstBlend] ZWrite [_ZWrite] Offset [_OffsetFactor],[_OffsetUnits]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "NativeN64Sampler.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            TEXTURE2D(_SecondaryTex);SAMPLER(sampler_SecondaryTex);
            TEXTURE2D(_NormalTex);SAMPLER(sampler_NormalTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor,_PrimitiveColor,_EnvironmentColor,_SamplerMask,_SamplerClamp,_SamplerOrigin;
                float4 _ColorMux0,_ColorMux1,_AlphaMux0,_AlphaMux1,_SecondaryUV,_Wrap;
                float4 _IntensityAlpha,_SecondarySamplerMask,_SecondarySamplerClamp,_SecondarySamplerOrigin;
                float _NativeSampler,_SecondaryNativeSampler,_AlphaCompareDisabled;
                float _Relief,_Cutoff,_NativeMaterial,_NativeCycles,_PrimitiveLOD,_CoverageAlpha;
            CBUFFER_END
            float _StadiumOriginalRendering;
            struct A{float4 position:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct V{float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float3 tangent:TEXCOORD2;float3 bitangent:TEXCOORD3;float2 uv:TEXCOORD4;float4 color:COLOR;float fog:TEXCOORD5;};
            V Vert(A i)
            {
                V o;VertexPositionInputs p=GetVertexPositionInputs(i.position.xyz);VertexNormalInputs n=GetVertexNormalInputs(i.normal,i.tangent);
                o.position=p.positionCS;o.world=p.positionWS;o.normal=n.normalWS;o.tangent=n.tangentWS;o.bitangent=n.bitangentWS;o.uv=i.uv;o.color=i.color;o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            float AxisUV(float coordinate,float mode)
            {
                if(mode>=2)return saturate(coordinate);
                if(mode==1)return 1-abs(frac(coordinate*.5)*2-1);
                return coordinate;
            }
            float3 RGBInput(float selector,int role,float4 combined,float4 tile0,float4 tile1,float4 shade)
            {
                if(selector==0)return combined.rgb;
                if(selector==1)return tile0.rgb;
                if(selector==2)return tile1.rgb;
                if(selector==3)return _PrimitiveColor.rgb;
                if(selector==4)return shade.rgb;
                if(selector==5)return _EnvironmentColor.rgb;
                if(selector==6&&(role==0||role==3))return 1;
                if(role==2)
                {
                    if(selector==7)return combined.a;
                    if(selector==8)return tile0.a;
                    if(selector==9)return tile1.a;
                    if(selector==10)return _PrimitiveColor.a;
                    if(selector==11)return shade.a;
                    if(selector==12)return _EnvironmentColor.a;
                    if(selector==14)return _PrimitiveLOD;
                }
                // Import validation rejects keying/noise/K4/K5/unknown LOD operands.
                return 0;
            }
            float AlphaInput(float selector,int role,float4 combined,float4 tile0,float4 tile1,float4 shade)
            {
                if(selector==0)return role==2?0:combined.a;
                if(selector==1)return tile0.a;
                if(selector==2)return tile1.a;
                if(selector==3)return _PrimitiveColor.a;
                if(selector==4)return shade.a;
                if(selector==5)return _EnvironmentColor.a;
                if(selector==6)return role==2?_PrimitiveLOD:1;
                return 0;
            }
            float4 Cycle(float4 cm,float4 am,float4 previous,float4 t0,float4 t1,float4 shade)
            {
                float3 rgb=(RGBInput(cm.x,0,previous,t0,t1,shade)-RGBInput(cm.y,1,previous,t0,t1,shade))*RGBInput(cm.z,2,previous,t0,t1,shade)+RGBInput(cm.w,3,previous,t0,t1,shade);
                float alpha=(AlphaInput(am.x,0,previous,t0,t1,shade)-AlphaInput(am.y,1,previous,t0,t1,shade))*AlphaInput(am.z,2,previous,t0,t1,shade)+AlphaInput(am.w,3,previous,t0,t1,shade);
                return saturate(float4(rgb,alpha));
            }
            half4 Frag(V i):SV_Target
            {
                float2 uv0=i.uv,uv1=float2(i.uv.x*_SecondaryUV.x,1-(1-i.uv.y)*_SecondaryUV.y);
                if(_NativeMaterial>0||_NativeSampler>0){uv0=float2(AxisUV(uv0.x,_Wrap.x),1-AxisUV(1-uv0.y,_Wrap.y));uv1=float2(AxisUV(uv1.x,_Wrap.z),1-AxisUV(1-uv1.y,_Wrap.w));}
                if(_NativeSampler>0&&_StadiumOriginalRendering>0)
                    uv0=NativeN64NearestUV(i.uv,int2(_Wrap.xy),int2(_SamplerMask.xy),int2(_SamplerClamp.xy),_SamplerClamp.zw,_SamplerOrigin.xy);
                half4 tile0;
                if(_NativeSampler>0&&_StadiumOriginalRendering>0)
                    tile0=SAMPLE_TEXTURE2D_LOD(_MainTex,sampler_PointClamp,uv0,0);
                else tile0=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv0);
                // Nintendo I4/I8 texels replicate encoded intensity into alpha.
                // Sampling an sRGB texture linearizes RGB, so recover that byte
                // value before assigning alpha; RGBA and IA keep their own alpha.
                if(_IntensityAlpha.x>0)tile0.a=LinearToSRGB(tile0.rgb).r;
                // Native texture bytes and vertex colors combine in their original
                // encoded color space. Unity decodes sRGB textures on sampling.
                if(_StadiumOriginalRendering>0)tile0.rgb=LinearToSRGB(tile0.rgb);
                half4 albedo=tile0*_BaseColor*i.color;
                if(_NativeMaterial>0)
                {
                    half4 tile1;
                    if(_SecondaryNativeSampler>0&&_StadiumOriginalRendering>0)
                    {
                        uv1=NativeN64NearestUV(float2(i.uv.x*_SecondaryUV.x,1-(1-i.uv.y)*_SecondaryUV.y),int2(_Wrap.zw),int2(_SecondarySamplerMask.xy),int2(_SecondarySamplerClamp.xy),_SecondarySamplerClamp.zw,_SecondarySamplerOrigin.xy);
                        tile1=SAMPLE_TEXTURE2D_LOD(_SecondaryTex,sampler_PointClamp,uv1,0);
                    }
                    else tile1=SAMPLE_TEXTURE2D(_SecondaryTex,sampler_SecondaryTex,uv1);
                    if(_IntensityAlpha.y>0)tile1.a=LinearToSRGB(tile1.rgb).r;
                    if(_StadiumOriginalRendering>0)tile1.rgb=LinearToSRGB(tile1.rgb);
                    albedo=Cycle(_ColorMux0,_AlphaMux0,0,tile0,tile1,i.color);
                    if(_NativeCycles>1)albedo=Cycle(_ColorMux1,_AlphaMux1,albedo,tile0,tile1,i.color);
                    if(_CoverageAlpha>0)albedo.a=1;
                }
                if(_AlphaCompareDisabled<.5)clip(albedo.a-_Cutoff);
                if(_StadiumOriginalRendering>0)return half4(SRGBToLinear(albedo.rgb),albedo.a);
                half3 map=UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex,sampler_NormalTex,uv0));map.xy*=_Relief;
                half3 normal=normalize(map.x*i.tangent+map.y*i.bitangent+map.z*i.normal);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));half diffuse=saturate(dot(normal,light.direction));
                half3 lighting=SampleSH(normal)*.85+light.color*(.22+.68*diffuse)*light.shadowAttenuation;
                return half4(MixFog(albedo.rgb*lighting,i.fog),albedo.a);
            }
            ENDHLSL
        }
    }
}
