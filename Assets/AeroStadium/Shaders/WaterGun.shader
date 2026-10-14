Shader "AeroStadium/Original Water Gun"
{
    Properties { _Mode("Liquid sheet, droplet, or splash", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+14" }
        Pass
        {
            Name "OriginalPressureWater"
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
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float2 phase:TEXCOORD1; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float2 phase:TEXCOORD3; float4 color:COLOR; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 c=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(c), Hash(c+float2(1,0)), f.x), lerp(Hash(c+float2(0,1)), Hash(c+1), f.x), f.y);
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                if (_Mode>.5 && _Mode<1.5)
                {
                    float2 p=(input.uv-.5)*2;
                    float angle=input.color.g*.73;
                    float2 q=float2(cos(angle)*p.x-sin(angle)*p.y, sin(angle)*p.x+cos(angle)*p.y);
                    q.y*=input.color.a;
                    world += UNITY_MATRIX_I_V._m00_m10_m20*q.x*input.color.r
                        + UNITY_MATRIX_I_V._m01_m11_m21*q.y*input.color.r;
                    normal=UNITY_MATRIX_I_V._m02_m12_m22;
                }
                output.positionCS=TransformWorldToHClip(world);
                output.positionWS=world; output.normalWS=normal;
                output.uv=input.uv; output.phase=input.phase; output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                if (input.color.b<.001) return 0;
                float t=_Time.y, alpha, foam, fresnel, spec;
                float3 water;
                if (_Mode>.5 && _Mode<1.5)
                {
                    float2 p=(input.uv-.5)*2;
                    float shape=1-dot(p,p);
                    float disc=smoothstep(0,.15,shape);
                    float highlight=exp(-dot(p-float2(-.27,.31),p-float2(-.27,.31))*43);
                    float rim=pow(saturate(1-sqrt(max(0,shape))),2);
                    alpha=disc*(.47+.38*rim)*input.color.b;
                    water=lerp(float3(.025,.36,.66),float3(.46,.87,1),rim*.78);
                    water+=float3(.76,.95,1)*highlight*1.55;
                    return half4(water*alpha,alpha);
                }
                float side=abs((input.uv.x-.5)*2);
                float2 flow=float2(input.uv.x*4+input.color.r*3, input.uv.y*1.8-t*7.2);
                float broad=Noise(flow), fine=Noise(flow*float2(2.7,1.3)+11.2);
                float strand=pow(saturate(.5+.5*sin(input.uv.x*31+input.uv.y*3.8-t*29+input.phase.y)),9);
                float edge=smoothstep(1,.69,side);
                float core=pow(saturate(1-side),3);
                foam=saturate(strand*.40+pow(fine,6)*.62);
                if (_Mode>1.5)
                {
                    foam=saturate(foam+.38*pow(side,4));
                    alpha=edge*(.37+.29*broad)*input.color.b;
                }
                else alpha=edge*(.34+core*.24+broad*.15)*input.color.b;
                float3 view=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                float3 normal=SafeNormalize(input.normalWS);
                normal*=dot(normal,view)<0 ? -1:1;
                normal=SafeNormalize(normal+float3(sin(input.uv.y*14-t*25),cos(input.uv.y*17-t*21),0)*.11);
                Light mainLight=GetMainLight();
                float3 halfDirection=SafeNormalize(view+mainLight.direction);
                spec=pow(saturate(dot(normal,halfDirection)),88)*1.12;
                fresnel=pow(1-saturate(dot(normal,view)),3);
                water=lerp(float3(.014,.20,.43),float3(.045,.62,.84),core*.62+broad*.21);
                water=lerp(water,float3(.66,.94,1),saturate(foam+fresnel*.63));
                water+=float3(.58,.87,1)*spec*mainLight.color;
                alpha=saturate(alpha+fresnel*input.color.b*.13);
                return half4(water*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
