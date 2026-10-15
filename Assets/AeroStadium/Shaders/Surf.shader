Shader "AeroStadium/Original Surf Breaker"
{
    Properties { _Mode("Body, foam, spray, floor wash", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+16" }
        Pass
        {
            Name "OriginalRollingBreaker"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Mode;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv; output.color=input.color; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                if(input.color.b<.002) return 0;
                float t=_Time.y;
                if(_Mode>1.5 && _Mode<2.5)
                {
                    float2 p=(input.uv-.5)*2;
                    float shape=1-dot(p,p), edge=smoothstep(0,.22,shape);
                    float glint=exp(-dot(p-float2(-.25,.30),p-float2(-.25,.30))*30);
                    float alpha=edge*(.60+.27*glint)*input.color.b;
                    float3 rgb=lerp(float3(.18,.68,.89),float3(.91,.99,1),saturate(glint+.38));
                    return half4(rgb*alpha,alpha);
                }
                float2 flow=input.positionWS.xz*float2(3.6,4.1)+float2(t*.42,-t*.80);
                float broad=Noise(flow), fine=Noise(flow*3.3+float2(11,t*.17));
                float cellular=Noise(flow*7.8+input.color.a);
                float veins=pow(saturate(1-abs(fine-.52)*5.8),5);
                float foam=saturate(input.color.r*(.35+fine*.55)+veins*.11);
                float3 n=SafeNormalize(input.normalWS), v=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                n*=dot(n,v)<0?-1:1;
                n=SafeNormalize(n+float3(sin(flow.x*4+t*2),0,cos(flow.y*3-t*2))*.075);
                float fresnel=pow(1-saturate(dot(n,v)),3.4);
                Light light=GetMainLight();
                float highlight=pow(saturate(dot(n,SafeNormalize(v+light.direction))),100);
                float3 water=lerp(float3(.012,.13,.29),float3(.025,.49,.72),saturate(input.color.g*.4+broad*.55));
                water=lerp(water,float3(.36,.82,.94),fresnel*.66);
                water=lerp(water,float3(.86,.98,1),foam*.66);
                water+=highlight*float3(.72,.93,1)*light.color*.95;
                float alpha=(.30+input.color.g*.12+fresnel*.17+broad*.06)*input.color.b;
                if(_Mode>.5 && _Mode<1.5)
                {
                    float pockets=smoothstep(.15,.68,cellular);
                    float fizz=saturate(pockets*.65+veins*.55+fine*.34);
                    water=lerp(float3(.18,.67,.82),float3(.91,.99,1),fizz);
                    alpha=(.45+fizz*.43)*input.color.b;
                }
                if(_Mode>2.5)
                {
                    float ripple=.5+.5*sin(input.uv.y*64-t*5+broad*4);
                    float lace=pow(saturate(ripple),15)*.18+veins*.13;
                    water=lerp(float3(.016,.27,.42),float3(.61,.91,.98),lace+foam*.36);
                    alpha=(.13+lace+fresnel*.11)*input.color.b;
                }
                alpha=saturate(alpha); return half4(water*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
