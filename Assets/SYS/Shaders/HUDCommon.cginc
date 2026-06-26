// HUDCommon.cginc — shared feature core for the four HUD attack-mode shaders.
// Features: separated colors (core/glow/accent/accent2), double frame, corner brackets,
// scanline sweep, inner halo, live energy-noise flicker, user decal/emblem texture,
// intro assemble (_Anim) + outro fade (_Fade).
// Each mode shader includes this, then DEFINES:  float3 OrnamentField(...)
#ifndef HUD_COMMON_INCLUDED
#define HUD_COMMON_INCLUDED
#include "UnityCG.cginc"

struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };

float4 _CoreColor,_GlowColor,_AccentColor,_Accent2Color;
float  _Aspect,_Border,_Glow,_Intensity,_CornerLen,_Inset,_Inset2;
float  _Pulse,_PulseSpeed,_Anim,_Fade;
float  _ScanStrength,_ScanSpeed,_NoiseStrength,_NoiseScroll,_HaloStrength;
float  _DecalStrength;
sampler2D _NoiseTex;
sampler2D _DecalTex; float4 _DecalTex_ST;
float4 _DecalColor;

float boxd(float2 p,float2 b){ float2 d=abs(p)-b; return length(max(d,0.0))+min(max(d.x,d.y),0.0); }
float hash(float n){ return frac(sin(n)*43758.5453); }

v2f vert(appdata v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }

// each mode shader defines this (prototype):
float3 OrnamentField(float2 p, float2 ext, float t, float anm, float pulse);

fixed4 HUDFrag(v2f i)
{
    float2 p=i.uv-0.5; p.x*=_Aspect;
    float2 ext=float2(0.5*_Aspect,0.5)-_Inset;
    float t=_Time.y;
    float anm=smoothstep(0.0,1.0,_Anim);
    float pulse=1.0+_Pulse*sin(t*_PulseSpeed);

    // intro assemble: frame wipes open horizontally
    float wipe=step(abs(p.x), ext.x*(0.10+0.90*anm));

    // outer frame
    float d0=abs(boxd(p,ext));
    float core=smoothstep(_Border,0.0,d0)*wipe;
    float glow=smoothstep(_Glow,0.0,d0)*wipe;

    // inner secondary frame (double-line look)
    float2 ext2=ext-_Inset2;
    float d1=abs(boxd(p,ext2));
    core += smoothstep(_Border*0.7,0.0,d1)*wipe*0.9;
    glow += smoothstep(_Glow*0.6,0.0,d1)*0.5*wipe;

    // corner brackets + ticks (accent)
    float2 cn=ext-abs(p);
    float onF=smoothstep(_Border*2.5,0.0,d0);
    float bracket=onF*step(cn.x,_CornerLen)*step(cn.y,_CornerLen)*step(0.45,_Anim);

    // scanline sweep
    float scanY=frac(t*_ScanSpeed);
    float scan=smoothstep(0.012,0.0,abs(i.uv.y-scanY))*_ScanStrength*onF;

    // inner halo near the edges
    float halo=smoothstep(_Glow*3.0,0.0,d0)*_HaloStrength*0.4*wipe;

    // live energy flicker from a noise texture (default white = no flicker)
    float nz=tex2D(_NoiseTex, float2(i.uv.x*3.0 + t*_NoiseScroll, i.uv.y*3.0 - t*_NoiseScroll*0.3)).r;
    float energy=1.0 + _NoiseStrength*(nz-0.5)*2.0;

    // mode ornaments (already colored per-part)
    float3 orn=OrnamentField(p,ext,t,anm,pulse);

    // user decal / emblem texture (additive), default black = none
    float4 decal=tex2D(_DecalTex, TRANSFORM_TEX(i.uv,_DecalTex)) * _DecalColor;
    float decalA=decal.a*_DecalStrength;

    // ---- composite with separated colors ----
    core *= pulse*energy;
    glow  = glow*pulse*energy + halo;

    float3 rgb=0;
    rgb += core * lerp(_CoreColor.rgb, float3(1,1,1), 0.45);  // hot core washes to white
    rgb += glow * _GlowColor.rgb * 0.7;
    rgb += (bracket*0.9 + scan) * _AccentColor.rgb;
    rgb += orn;                                               // ornaments carry their own color
    rgb += decal.rgb * decalA;

    float ornA=max(orn.r,max(orn.g,orn.b));
    rgb *= _Intensity * _Fade;
    float a=saturate(core + glow*0.8 + bracket + scan + ornA + decalA) * _Fade;

    rgb*=i.color.rgb; a*=i.color.a;
    return fixed4(rgb,a);
}
#endif
