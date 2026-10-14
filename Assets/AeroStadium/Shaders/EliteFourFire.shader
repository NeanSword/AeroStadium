Shader "AeroStadium/Elite Four Living Fire"
{
    Properties
    {
        _Seed ("Independent flame phase",Float)=1.9
        _Ember ("Spark rendering",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+12" }
        Pass
        {
            Name "LivingFire"
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
                float _Seed;
                float _Ember;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 phase:TEXCOORD1;float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 phase:TEXCOORD1;float4 color:COLOR; };
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }
            float Flow(float2 p)
            {return Noise(p)*.57+Noise(p*2.03+13.7)*.28+Noise(p*4.13+7.2)*.15;}
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position=input.positionOS.xyz;
                if(_Ember>.5)
                {
                    float3 world=TransformObjectToWorld(position);
                    float2 corner=(input.uv-.5)*2*input.color.g;
                    world+=UNITY_MATRIX_I_V._m00_m10_m20*corner.x+UNITY_MATRIX_I_V._m01_m11_m21*corner.y*1.7;
                    output.positionCS=TransformWorldToHClip(world);
                }
                else
                {
                    float y=input.uv.y,phase=_Seed+input.phase.x,t=_Time.y;
                    if(input.phase.y<0)
                        position.xz*=1+.055*sin(t*2.6+phase+y*11)+.025*sin(t*5.3+phase*1.7);
                    float swing=y*y*(.17*sin(t*1.9+phase+y*5.1)+.065*sin(t*4.3+phase+y*11));
                    position.x+=swing;position.z+=y*y*.095*sin(t*2.1+phase*.7+y*4.4);
                    position.y*=1+.035*sin(t*3.1+phase)+.02*sin(t*5.7+phase+y*3);
                    output.positionCS=TransformObjectToHClip(position);
                }
                output.uv=input.uv;output.phase=input.phase;output.color=input.color;return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                if(_Ember>.5)
                {
                    float2 p=(uv-.5)*2;float halo=saturate(1-dot(p,p));
                    float alpha=halo*halo*input.color.r*.86;
                    return half4(float3(2.1,.76,.085)*alpha,alpha);
                }
                float time=_Time.y,seed=_Seed+input.phase.x;
                // Advected turbulence rises, splits and reunites; it never becomes a static textured cone.
                float2 p=float2(uv.x*3.7+seed,uv.y*3.15-time*1.38);
                float coarse=Flow(p),fine=Noise(p*4.1+float2(time*.12,0));
                float side=(uv.x-.5)*2;
                float bend=.19*sin(uv.y*8.5-time*2.05+seed)*uv.y+.13*(coarse-.5)*uv.y;
                // A wide, irregular lower skirt engulfs the collar and stone point;
                // the higher portion separates into narrower upward licking tongues.
                float width=lerp(.97,.07,pow(saturate(uv.y),1.06));
                float skirt=(uv.y-.14)*5.5;
                width+=.105*exp(-skirt*skirt);
                width+=(coarse-.5)*(.20+uv.y*.59);
                width+=.045*sin(side*11+time*3.2+seed)*(1-smoothstep(.12,.42,uv.y));
                float envelope=width-abs(side-bend);
                float density=envelope+(coarse-.48)*(.10+uv.y*.23);
                float alpha=smoothstep(-.055,.065,density);
                alpha*=smoothstep(0,.018,uv.y)*(1-smoothstep(.89,1,uv.y));
                alpha*=lerp(.95,.66,uv.y)*( .84+.16*fine);
                if(input.phase.y<0)alpha*=lerp(.14,.89,smoothstep(.31,.64,coarse));
                float core=saturate(1-abs(side-bend)/max(width,.06));
                float heat=saturate(core*.74+(1-uv.y)*.33+(coarse-.5)*.31);
                float3 color=lerp(float3(1.45,.085,.005),float3(2.02,1.33,.12),smoothstep(.17,.96,heat));
                color*=.91+.09*fine;
                return half4(color*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
