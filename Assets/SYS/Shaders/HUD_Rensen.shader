// HUD_Rensen.shader — 연섬 (초록). Rich version: per-part colors + textures.
// Uses HUDCommon.cginc. Full-screen UI RawImage. For glow-only look: Blend One One.
Shader "UI/HUD_Rensen"
{
    Properties
    {
        [Header(Colors)]
        _CoreColor("Core (hot edge)",Color)=(0.7,1,0.85,1)
        _GlowColor("Glow (neon)",Color)=(0.15,1,0.62,1)
        _AccentColor("Accent (nodes/edges)",Color)=(0.15,1,0.62,1)
        _Accent2Color("Accent 2 (segments/chain)",Color)=(0.6,1,0.8,1)
        [Header(Frame)]
        _Intensity("Intensity",Range(0,5))=1.6
        _Border("Border Thickness",Range(0,0.06))=0.010
        _Glow("Glow Width",Range(0,0.5))=0.16
        _Inset("Frame Inset",Range(0,0.2))=0.035
        _Inset2("Inner Frame Gap",Range(0,0.1))=0.020
        _CornerLen("Corner Bracket Size",Range(0,0.4))=0.14
        _Pulse("Pulse Amount",Range(0,1))=0.20
        _PulseSpeed("Pulse Speed",Float)=2.2
        _HaloStrength("Inner Halo",Range(0,2))=0.6
        [Header(Scanline)]
        _ScanStrength("Scanline Strength",Range(0,1))=0.5
        _ScanSpeed("Scanline Speed",Float)=0.55
        [Header(Energy Noise)]
        _NoiseTex("Energy Noise (R)",2D)="white"{}
        _NoiseStrength("Energy Flicker",Range(0,1))=0.0
        _NoiseScroll("Energy Scroll",Float)=0.5
        [Header(Decal Emblem)]
        _DecalTex("Decal / Emblem (RGBA)",2D)="black"{}
        _DecalColor("Decal Tint",Color)=(1,1,1,1)
        _DecalStrength("Decal Strength",Range(0,3))=0.0
        [Header(State auto driven)]
        _Aspect("Aspect (auto)",Float)=1.7777
        _Anim("Intro 0..1",Range(0,1))=1
        _Fade("Visible 0..1",Range(0,1))=1
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane"}
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha   // additive look: change to  Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragMain
            #include "HUDCommon.cginc"

            float3 OrnamentField(float2 p,float2 ext,float t,float anm,float pulse)
            {
                float3 c=0;
                // 3 charge segments top-center (skewed bars), lit sequentially
                float segY=ext.y-0.03;
                for(int k=0;k<3;k++){
                    float cx=(k-1)*0.30;
                    float b=boxd(float2((p.x-cx)+(p.y-segY)*0.5, p.y-segY), float2(0.12,0.011));
                    float vis=step((float)k*0.16,_Anim);
                    c += step(b,0.0)*vis*pulse*0.9*_Accent2Color.rgb;          // fill
                    c += smoothstep(0.018,0.0,abs(b))*vis*0.4*_AccentColor.rgb; // edge glow
                }
                // corner circuit node clusters
                float2 q=ext-abs(p);
                float nd=min(min(length(q-float2(0.020,0.020)), length(q-float2(0.070,0.038))), length(q-float2(0.040,0.072)));
                float nearC=step(q.x,0.12)*step(q.y,0.12);
                c += smoothstep(0.009,0.0,abs(nd-0.006))*nearC*step(0.3,_Anim)*0.95*_AccentColor.rgb;
                c += smoothstep(0.004,0.0,abs((q.y-0.020)-(q.x-0.020)*0.75))*step(q.x,0.085)*step(q.y,0.085)*nearC*0.5*anm*_Accent2Color.rgb;
                // chain links along bottom (gentle bob)
                float chY=-(ext.y-0.02)+0.004*sin(t*3.0+p.x*20.0);
                float cxk=(frac((p.x+0.4)/0.10)-0.5)*0.10;
                c += smoothstep(0.012,0.0,abs(cxk)+abs(p.y-chY))*step(abs(p.x),0.4)*0.7*anm*pulse*_Accent2Color.rgb;
                return c;
            }
            fixed4 fragMain(v2f i):SV_Target { return HUDFrag(i); }
            ENDCG
        }
    }
}
