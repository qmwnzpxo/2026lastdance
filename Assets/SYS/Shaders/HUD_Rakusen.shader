// HUD_Rakusen.shader — 낙섬 (하늘/번개). Rich version: per-part colors + textures.
// Uses HUDCommon.cginc. Full-screen UI RawImage. For glow-only look: Blend One One.
Shader "UI/HUD_Rakusen"
{
    Properties
    {
        [Header(Colors)]
        _CoreColor("Core (hot edge)",Color)=(0.8,0.97,1,1)
        _GlowColor("Glow (neon)",Color)=(0.25,0.85,1,1)
        _AccentColor("Accent (rail)",Color)=(0.25,0.85,1,1)
        _Accent2Color("Accent 2",Color)=(0.6,0.92,1,1)
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
        [Header(Lightning)]
        _BoltDepth("Bolt Length",Range(0,0.4))=0.20
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

            float _BoltDepth;

            float3 OrnamentField(float2 p,float2 ext,float t,float anm,float pulse)
            {
                float3 c=0;
                // charged top rail
                float railY=ext.y-0.006;
                c += smoothstep(0.004,0.0,abs(p.y-railY))*step(abs(p.x),ext.x*0.92)*0.55*pulse*anm*_AccentColor.rgb;
                // descending jagged lightning bolts (staggered strikes)
                float yTop=ext.y;
                for(int k=0;k<4;k++){
                    float seed=(float)k*1.7+0.5;
                    float cx=-0.62+k*0.41;
                    float depth=_BoltDepth*(0.7+0.5*hash(seed));
                    float inSeg=step(yTop-depth,p.y)*step(p.y,yTop);
                    float bx=cx + 0.028*sin(p.y*55.0+seed*6.0) + 0.018*sin(p.y*21.0+seed*2.0);
                    float bd=abs(p.x-bx);
                    float ph=frac(t*0.32 + seed);
                    float strike=smoothstep(0.06,0.0,ph)+smoothstep(1.0,0.94,ph);
                    float coreB=smoothstep(0.009,0.0,bd)*inSeg;
                    float glowB=smoothstep(0.03,0.0,bd)*inSeg*0.4;
                    c += coreB*saturate(strike)*1.3*anm*lerp(_CoreColor.rgb,float3(1,1,1),0.6);
                    c += glowB*saturate(strike)*1.0*anm*_GlowColor.rgb;
                    float2 tip=float2(bx, yTop-depth);
                    c += smoothstep(0.012,0.0,length(p-tip))*saturate(strike)*0.9*anm*float3(1,1,1);
                }
                return c;
            }
            fixed4 fragMain(v2f i):SV_Target { return HUDFrag(i); }
            ENDCG
        }
    }
}
