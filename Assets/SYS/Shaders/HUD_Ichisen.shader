// HUD_Ichisen.shader — 일섬 (기본/분홍). Rich version: per-part colors + textures.
// Uses HUDCommon.cginc. Full-screen UI RawImage. For glow-only look: Blend One One.
Shader "UI/HUD_Ichisen"
{
    Properties
    {
        [Header(Colors)]
        _CoreColor("Core (hot edge)",Color)=(1,0.7,0.9,1)
        _GlowColor("Glow (neon)",Color)=(1,0.30,0.77,1)
        _AccentColor("Accent (ornaments)",Color)=(1,0.30,0.77,1)
        _Accent2Color("Accent 2 (diamonds)",Color)=(1,0.65,0.88,1)
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
                // top & bottom diamonds (accent2)
                float dia=min(abs(p.x)+abs(p.y-ext.y), abs(p.x)+abs(p.y+ext.y));
                float ds=0.020*(1.0+0.25*sin(t*3.0));
                c += smoothstep(ds,0.0,dia)*step(0.35,_Anim)*pulse*1.2*_Accent2Color.rgb;
                // tick rulers (accent)
                float band=smoothstep(0.016,0.0,abs(abs(p.y)-(ext.y-0.045)));
                c += step(0.5,frac(p.x*40.0))*band*step(abs(p.x),ext.x*0.5)*0.5*anm*_AccentColor.rgb;
                // single diagonal slash sweep (hot white core)
                float ang=-0.35, rotX=p.x*cos(ang)-p.y*sin(ang);
                float sweep=lerp(-0.95,0.95,_Anim)*ext.x;
                c += smoothstep(0.012,0.0,abs(rotX-sweep))*sin(_Anim*3.14159)*1.6*lerp(_CoreColor.rgb,float3(1,1,1),0.5);
                // corner quarter arcs (accent)
                float2 cn=ext-abs(p); float cr=length(cn);
                c += smoothstep(0.010,0.0,abs(cr-_CornerLen*0.55))*step(cn.x,_CornerLen)*step(cn.y,_CornerLen)*0.7*step(0.4,_Anim)*_AccentColor.rgb;
                return c;
            }
            fixed4 fragMain(v2f i):SV_Target { return HUDFrag(i); }
            ENDCG
        }
    }
}
