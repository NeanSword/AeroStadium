Shader "AeroStadium/Original Flamethrower"
{
    Properties { _Mode("Ribbon, rolling flame, or cinder",Float)=0 }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+16"}
        Pass
        {
            Name "OriginalFireBreath"
            Tags {"LightMode"="UniversalForward"}
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Mode;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 phase:TEXCOORD1;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 phase:TEXCOORD1;float4 color:COLOR;};
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            float Turbulence(float2 p)
            {return Noise(p)*.54+Noise(p*2.07+7.3)*.29+Noise(p*4.19+19.1)*.17;}
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                if(_Mode>.5)
                {
                    float2 corner=(input.uv-.5)*2;
                    float angle=input.color.g*.73+_Time.y*(_Mode>1.5?.8:1.15);
                    float2 rotated=float2(cos(angle)*corner.x-sin(angle)*corner.y,sin(angle)*corner.x+cos(angle)*corner.y);
                    rotated.y*=input.color.a;
                    world+=UNITY_MATRIX_I_V._m00_m10_m20*rotated.x*input.color.r
                        +UNITY_MATRIX_I_V._m01_m11_m21*rotated.y*input.color.r;
                }
                output.positionCS=TransformWorldToHClip(world);output.uv=input.uv;
                output.phase=input.phase;output.color=input.color;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                if(input.color.b<.001)return 0;
                float t=_Time.y,alpha,heat;
                if(_Mode>1.5)
                {
                    float2 p=(input.uv-.5)*2;
                    alpha=saturate(1-dot(p,p));alpha*=alpha*input.color.b;
                    return half4(float3(2.1,.95,.13)*alpha,alpha);
                }
                if(_Mode>.5)
                {
                    float2 p=(input.uv-.5)*2;
                    float seed=input.color.g;
                    float2 flow=float2(input.uv.x*3.9+seed,input.uv.y*3.6-t*1.7);
                    float turbulence=Turbulence(flow);
                    float boundary=1-dot(p,p)+(turbulence-.5)*.80;
                    alpha=smoothstep(-.11,.32,boundary)*input.color.b;
                    alpha*=smoothstep(.20,.69,turbulence)*.91;
                    heat=saturate((1-length(p))*.62+turbulence*.53);
                }
                else
                {
                    float2 flow=float2(input.uv.x*3.5+input.phase.y,input.uv.y*1.36-t*4.8);
                    float turbulence=Turbulence(flow);
                    float side=abs((input.uv.x-.5)*2+.14*sin(input.uv.y*5-t*9+input.phase.y));
                    float envelope=1-side+(turbulence-.5)*.70;
                    alpha=smoothstep(.02,.29,envelope)*smoothstep(.20,.68,turbulence)*input.color.b*.75;
                    heat=saturate((1-side)*.54+turbulence*.55);
                }
                float3 color=lerp(float3(1.43,.075,.004),float3(2.0,1.28,.13),smoothstep(.27,.96,heat));
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
