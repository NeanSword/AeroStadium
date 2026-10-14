Shader "AeroStadium/Original Protect Shield"
{
    Properties
    {
        _Mode("Shell or ground halo",Float)=0
        _Age("Activation time",Float)=0
        _Fade("Release opacity",Float)=1
        _ImpactAge("Contact ripple time",Float)=10
        _ImpactDirection("Contact direction on shell",Vector)=(0,1,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20"}
        Pass
        {
            Name "OriginalProtectEnergy"
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
                float _Mode,_Age,_Fade,_ImpactAge;
                float4 _ImpactDirection;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 sphere:TEXCOORD2;float2 uv:TEXCOORD3;};
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.sphere=input.positionOS.xyz;output.uv=input.uv;return output;
            }
            // Local hex coordinates remain fixed on the shell; only their energy moves.
            float2 HexCell(float2 p)
            {
                const float2 lattice=float2(1,1.7320508);
                float2 a=frac(p/lattice)*lattice-lattice*.5;
                float2 b=frac((p-lattice*.5)/lattice)*lattice-lattice*.5;
                return dot(a,a)<dot(b,b)?a:b;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float formed=smoothstep(0,.65,_Age),alpha,energy;
                float impact=saturate(1-_ImpactAge/.70);
                float3 tint=float3(.16,1.25,.76);
                if(_Mode>.5)
                {
                    float r=length(input.uv),angle=atan2(input.uv.y,input.uv.x);
                    float ring=exp(-pow((r-(.83+.15*formed))/.031,2));
                    float outer=exp(-pow((r-1.075)/.016,2));
                    float pulse=exp(-pow((r-(.74+frac(_Age*.55)*.36))/.035,2))*.30;
                    float arcs=smoothstep(.32,.72,sin(angle*9-_Age*3));
                    alpha=(ring*.40+outer*arcs*.23+pulse)*formed*_Fade;
                    energy=1.10+impact*.8;
                }
                else
                {
                    float3 sphere=normalize(input.sphere);
                    float3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                    float facing=abs(dot(normalize(input.normalWS),view));
                    float rim=pow(saturate(1-facing),2.5);
                    float height=sphere.y*.5+.5;
                    float frontier=saturate(_Age/.65);
                    float reveal=1-smoothstep(frontier-.04,frontier+.035,height);
                    float formation=exp(-pow((height-frontier)/.045,2))*(1-formed);
                    float2 hex=abs(HexCell(input.uv*float2(28,17.320508)));
                    float edge=max(hex.x,dot(hex,float2(.5,.8660254)));
                    float lattice=smoothstep(.44,.495,edge);
                    float distance=acos(clamp(dot(sphere,normalize(_ImpactDirection.xyz)),-.999,.999));
                    float ripple=exp(-pow((distance-_ImpactAge*3.2)/.105,2))*impact;
                    float innerRipple=exp(-pow((distance-_ImpactAge*2.1)/.055,2))*impact*.40;
                    float contact=exp(-distance*distance*32)*exp(-_ImpactAge*8);
                    float movingArc=pow(saturate(sin(input.uv.x*18.8496+input.uv.y*11-_Age*3)),18);
                    float breathing=.92+.08*sin(_Age*2.4);
                    alpha=(.024+rim*.24+lattice*.036+movingArc*rim*.07)*breathing;
                    alpha+=formation*.46+(ripple+innerRipple)*(.22+lattice*.34)+contact*.62;
                    alpha=saturate(alpha)*reveal*_Fade;
                    energy=1.03+formation*.50+ripple*.66+contact*.90;
                    tint=lerp(tint,float3(.48,1.38,1.18),saturate(ripple+contact));
                }
                return half4(tint*energy*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
