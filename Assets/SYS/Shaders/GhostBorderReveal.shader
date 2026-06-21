Shader "Custom/URP2D/GhostBorderReveal"
{
    // ============================================================
    //  공격모드 전환용 "기계적 등장" 셰이더 (URP 2D / Unity 6000.x)
    //  - 코너부터 노이즈 디졸브로 등장
    //  - 디졸브 경계선에 글로우 라인
    //  - 진행 초반엔 글리치 UV 지터 (전원 들어오는 느낌)
    //  사용법: SpriteRenderer 또는 UI Raw Image 머티리얼에 적용,
    //          C# 스크립트에서 _Progress 0→1로 구동
    // ============================================================

    Properties
    {
        [MainTexture] _MainTex ("Border Texture (RGBA)", 2D) = "white" {}
        _NoiseTex ("Reveal Noise (R, tileable)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Reveal Progress (0~1)", Range(0,1)) = 0

        [Header(Edge Glow)]
        _EdgeWidth ("Edge Glow Width", Range(0.001, 0.3)) = 0.06
        _EdgeColor ("Edge Glow Color", Color) = (1, 0.2, 0.25, 1)
        _EdgeIntensity ("Edge Glow Intensity", Range(0,5)) = 2.0

        [Header(Dissolve Shape)]
        _CornerBias ("Corner-First Bias (0=균등,1=코너먼저)", Range(0,1)) = 0.5
        _NoiseScale ("Noise Tiling", Float) = 3.0

        [Header(Mechanical Glitch)]
        _GlitchAmount ("Glitch Jitter Amount", Range(0,0.2)) = 0.05
        _GlitchBlocks ("Glitch Block Count", Float) = 24
        _FlickerSpeed ("Power-on Flicker Speed", Float) = 40
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "GhostBorderReveal"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Progress;
                float  _EdgeWidth;
                float4 _EdgeColor;
                float  _EdgeIntensity;
                float  _CornerBias;
                float  _NoiseScale;
                float  _GlitchAmount;
                float  _GlitchBlocks;
                float  _FlickerSpeed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float rand(float2 co)
            {
                return frac(sin(dot(co, float2(12.9898, 78.233))) * 43758.5453);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // ---- 1) 글리치 지터: Progress가 낮을수록(등장 초반) 강하게, 끝나면 0 ----
                float glitchPower = (1.0 - saturate(_Progress * 1.4)) * _GlitchAmount;
                float blockRow = floor(uv.y * _GlitchBlocks) / _GlitchBlocks;
                float blockRand = rand(float2(blockRow, floor(_Time.y * _FlickerSpeed)));
                uv.x += (blockRand - 0.5) * glitchPower;

                // ---- 2) 코너 우선 마스크: 중심(0) ~ 모서리(1) ----
                float2 centered = uv - 0.5;
                float distFromCenter = saturate(length(centered) * 1.41421356);
                float cornerBoost = distFromCenter * _CornerBias;

                // ---- 3) 노이즈 디졸브 임계값 (코너일수록 더 빨리 통과되도록 낮춤) ----
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * _NoiseScale).r;
                float threshold = saturate(noise - cornerBoost * 0.5);

                float reveal = step(threshold, _Progress);
                float edge = smoothstep(_Progress - _EdgeWidth, _Progress, threshold) * reveal;

                // ---- 4) 메인 텍스처 ----
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float4 baseColor = tex * IN.color;

                // ---- 5) 전원 들어오는 플리커 (등장 도중에만, 끝나면 안정) ----
                float poweringOn = step(_Progress, 0.98);
                float flicker = 1.0;
                if (poweringOn > 0.5)
                {
                    flicker = lerp(0.6, 1.0, frac(sin(_Time.y * _FlickerSpeed) * 43758.5453));
                }

                float3 finalRGB = baseColor.rgb * flicker + _EdgeColor.rgb * edge * _EdgeIntensity;
                float finalAlpha = saturate(baseColor.a * reveal * flicker + edge);

                return float4(finalRGB, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
