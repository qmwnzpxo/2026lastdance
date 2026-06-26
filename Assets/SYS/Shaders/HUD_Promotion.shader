// HUD_Promotion.shader — 프로모션 (빨강). Rich version: per-part colors + textures.
// Uses HUDCommon.cginc. Full-screen UI RawImage. For glow-only look: Blend One One.
Shader "UI/HUD_Promotion"
{
    Properties
    {
        [Header(Colors)]
        _CoreColor("Core (hot edge)",Color)=(1,0.6,0.65,1)
        _GlowColor("Glow (neon)",Color)=(1,0.17,0.30,1)
        _AccentColor("Accent (film/timeline)",Color)=(1,0.17,0.30,1)
        _Accent2Color("Accent 2 (frames/nodes)",Color)=(1,0.55,0.6,1)
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
                // film sprocket strips L/R (scrolling holes)
                float fx=abs(abs(p.x)-(ext.x-0.030));
                float filmBand=smoothstep(0.011,0.0,fx)*step(abs(p.y),ext.y-0.06);
                c += filmBand*step(0.5,frac(p.y*34.0 - t*4.0))*0.7*anm*_AccentColor.rgb;
                // storyboard pre-cast frames (stamp sequentially) + connecting path
                float sbY=ext.y-0.20;
                c += smoothstep(0.004,0.0,abs(p.y-sbY))*step(abs(p.x),0.72)*0.35*anm*_AccentColor.rgb;
                for(int k=0;k<6;k++){
                    float cx=-0.66+k*0.264;
                    float vis=step((float)k*0.12,_Anim);
                    float b=boxd(float2(p.x-cx,p.y-sbY),float2(0.052,0.036));
                    c += smoothstep(0.006,0.0,abs(b))*vis*0.85*_Accent2Color.rgb;
                    c += smoothstep(0.022,0.0,abs(b))*vis*0.18*_GlowColor.rgb;
                }
                // bottom timeline + diamond nodes
                float tlY=-(ext.y-0.03);
                c += smoothstep(0.004,0.0,abs(p.y-tlY))*step(abs(p.x),0.55)*0.5*anm*_AccentColor.rgb;
                float nx=(frac((p.x+0.55)/0.22)-0.5)*0.22;
                c += smoothstep(0.013,0.0,abs(nx)+abs(p.y-tlY))*step(abs(p.x),0.55)*0.8*anm*_Accent2Color.rgb;
                // execute sweep flash mid-intro (hot white bar)
                float ex=lerp(-0.9,0.9,smoothstep(0.6,1.0,_Anim))*ext.x;
                c += smoothstep(0.02,0.0,abs(p.x-ex))*step(abs(p.y),ext.y-0.05)*smoothstep(0.6,0.8,_Anim)*(1.0-smoothstep(0.95,1.0,_Anim))*1.4*float3(1,1,1);
                return c;
            }
            fixed4 fragMain(v2f i):SV_Target { return HUDFrag(i); }
            ENDCG
        }
    }
}
