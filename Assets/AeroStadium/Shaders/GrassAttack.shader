Shader "AeroStadium/Original Living Grass"
{
    Properties
    {
        _Mode("Vine, leaf, cutting glint", Float) = 0
        _ClipShield("Clip shield interior", Float) = 0
        _ShieldCenter("Shield world centre", Vector) = (0,0,0,0)
        _ShieldRadii("Shield world radii", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+16" }
        Pass
        {
            Name "LivingGrass"
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
                float _Mode, _ClipShield;
                float4 _ShieldCenter, _ShieldRadii;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
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
                clip(input.color.a-.001);
                if (_ClipShield>.5)
                {
                    float3 shell=(input.positionWS-_ShieldCenter.xyz)/max(_ShieldRadii.xyz,float3(.02,.02,.02));
                    clip(dot(shell,shell)-1.0001);
                }
                float3 tint=input.color.rgb;
                float alpha=input.color.a;
                if (_Mode>1.5)
                {
                    float edge=1-abs(input.uv.y*2-1);
                    float tip=sin(input.uv.x*3.14159265);
                    alpha*=smoothstep(0,.8,edge)*sqrt(saturate(tip));
                    return half4(tint*alpha*1.55,alpha);
                }
                float3 view=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                float3 normal=SafeNormalize(input.normalWS);
                normal*=dot(normal,view)<0?-1:1;
                Light light=GetMainLight();
                float diffuse=.53+saturate(dot(normal,light.direction))*.47;
                float spec=pow(saturate(dot(normal,SafeNormalize(view+light.direction))),40);
                if (_Mode>.5)
                {
                    float across=abs(input.uv.y*2-1), length=input.uv.x;
                    float midrib=1-smoothstep(.025,.065,across);
                    float veins=1-smoothstep(.025,.065,abs(frac(length*6-across*1.45+.5)-.5));
                    veins*=smoothstep(.05,.16,across)*(1-smoothstep(.7,.97,across));
                    float margin=smoothstep(.72,1,across);
                    tint*=1-margin*.26;
                    tint=lerp(tint,float3(.57,.79,.16),midrib*.70+veins*.24);
                    tint*=diffuse;
                    tint+=float3(.25,.35,.075)*spec*.40;
                }
                else
                {
                    float ridge=.5+.5*sin(input.uv.x*56.55+input.uv.y*15);
                    float fiber=pow(ridge,10)*.15;
                    tint*=diffuse*(.88+fiber);
                    tint+=float3(.25,.31,.065)*spec*.5;
                }
                return half4(tint*alpha,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
