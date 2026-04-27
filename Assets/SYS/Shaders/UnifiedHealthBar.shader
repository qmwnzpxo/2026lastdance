// =============================================================================
//  UnifiedHealthBar.shader — 고스트 HP 단순화 버전
//
//  [ 고스트 HP 동작 방식 ]
//  셰이더는 _GhostFill 값만 읽어서 고스트 구간을 렌더링합니다.
//  _GhostFill 값을 C# Update() 가 매 프레임 _FillAmount 쪽으로 lerp 합니다.
//
//  사용자 입장에서:
//    1. Image 오브젝트에 UnifiedHealthBarVisual 컴포넌트 추가
//    2. Inspector 에서 _GhostFill 체크박스 ON
//    3. _FillAmount 를 바꾸면 고스트가 자동으로 따라옴 (호출 불필요)
//
//  Inspector 에서 조절:
//    Ghost Delay    → 데미지 후 고스트가 유지되는 시간 (C# 필드)
//    Ghost Speed    → 고스트가 따라오는 속도 (C# 필드)
//    _ColorGhost    → 고스트 색상
// =============================================================================

Shader "Custom/UI/UnifiedHealthBar"
{
    Properties
    {
        [HideInInspector] _MainTex ("텍스처 (시스템용)", 2D) = "white" {}

        // ── 기본 ──────────────────────────────────────────────────────────────
        _FillAmount   ("● 채움 비율 │ 0=완전히 빔  1=완전히 참",      Range(0,1)) = 0.7
        _SegmentCount ("● 칸 수",                                       Int)        = 10

        // ── 색상 ──────────────────────────────────────────────────────────────
        _ColorFill        ("■ 채워진 칸 색상",                          Color) = (0.18, 0.88, 0.38, 1)
        _ColorFillDim     ("■ 부분 채움 칸 색상 │ 부분채움 ON 시 사용", Color) = (0.08, 0.45, 0.18, 0.6)
        _ColorEmpty       ("■ 빈 칸 색상",                              Color) = (0.07, 0.07, 0.07, 1)
        _ColorEmptyBorder ("■ 빈 칸 테두리 색상",                       Color) = (0.85, 0.85, 0.85, 1)
        _ColorFillBorder  ("■ 채워진 칸 테두리 색상",                   Color) = (0.0,  0.0,  0.0,  1)
        _ColorOutline     ("■ 전체 외곽선 색상",                        Color) = (0.0,  0.0,  0.0,  1)
        _ColorGap         ("■ 갭 색상 │ 투명갭 OFF 시 보임",            Color) = (0.0,  0.0,  0.0,  1)
        _ColorDanger      ("■ 위험 색상",                               Color) = (1.0,  0.18, 0.18, 1)
        _DangerThreshold  ("● 위험 임계값 │ 0.3 = 30% 이하",           Range(0,1)) = 0.3
        _DangerPulseSpeed ("● 위험 깜빡임 속도",                        Range(1,20)) = 8.0

        // ── 고스트 HP ─────────────────────────────────────────────────────────
        // 셰이더는 _GhostFill 값을 읽어서 현재HP ~ 고스트HP 구간을 색칠합니다.
        // _GhostFill 은 C# Update() 가 매 프레임 자동으로 갱신합니다.
        // (호출 불필요 — 컴포넌트만 있으면 자동 동작)
        [Header(Ghost HP Fading Bar)]
        [Toggle(_GHOST_FILL_ON)] _UseGhostFill
            ("◆ 고스트 HP ON/OFF", Float) = 0

        // C# Update() 가 자동으로 갱신합니다. 직접 수정 금지.
        _GhostFill  ("● 고스트 HP 비율 │ C# 자동 갱신",
                      Range(0,1)) = 1.0

        // ★ 고스트 색상은 여기서 수정하세요.
        _ColorGhost ("■ 고스트 HP 색상",
                      Color) = (1.0, 0.85, 0.15, 1)

        // ── 외부 글로우 ───────────────────────────────────────────────────────
        [Header(Outer Glow)]
        [Toggle(_GLOW_ON)]       _UseGlow        ("◆ 외부 글로우 ON/OFF", Float) = 0
        _ColorGlow               ("■ 글로우 색상",  Color) = (0.3, 1.0, 0.5, 1)
        _GlowOuterPixels         ("● 글로우 번짐 범위 (픽셀)", Range(0,30)) = 8.0
        _GlowIntensity           ("● 글로우 강도",              Range(0,1))  = 0.85

        // ── 내부 글로우 ───────────────────────────────────────────────────────
        [Header(Inner Glow)]
        [Toggle(_INNER_GLOW_ON)] _UseInnerGlow   ("◆ 내부 글로우 ON/OFF │ 채움 경계 발광", Float) = 0
        _InnerGlowIntensity      ("● 내부 글로우 강도",           Range(0,1))  = 0.4
        _InnerGlowWidth          ("● 내부 글로우 너비 (픽셀)",    Range(0,16)) = 4.0

        // ── 시머 광택선 ───────────────────────────────────────────────────────
        [Header(Shimmer)]
        [Toggle(_SHIMMER_ON)]    _UseShimmer     ("◆ 시머 ON/OFF │ 채워진 구간 광택선", Float) = 0
        _ShimmerSpeed            ("● 시머 이동 속도",             Range(0,2))  = 0.35
        _ShimmerWidth            ("● 시머 너비 (픽셀)",           Range(0,30)) = 5.0
        _ShimmerIntensity        ("● 시머 강도",                  Range(0,1))  = 0.28

        // ── 모드 ─────────────────────────────────────────────────────────────
        [Header(Mode)]
        [Toggle(_TRANSPARENT_GAP_ON)] _UseTransparentGap
            ("◆ 투명 갭 ON/OFF │ ON=칸사이투명  OFF=갭색으로채움", Float) = 0
        [Toggle(_PARTIAL_FILL_ON)]    _UsePartialFill
            ("◆ 부분 채움 ON/OFF │ 세그먼트 모드 전용",            Float) = 0
        [Toggle(_SLIDER_MODE_ON)]     _UseSliderMode
            ("◆ 슬라이더 모드 ON/OFF │ ON=연속채움  OFF=칸단위",   Float) = 0

        // ── 픽셀 설정 (C# 자동 주입) ─────────────────────────────────────────
        [Header(Pixel Settings Auto Injected by Script)]
        _BarPixelSize    ("● 바 픽셀 크기 XY", Vector) = (400,20,0,0)
        _GapPixels       ("● 칸 사이 간격 (픽셀)", Float) = 3
        _BorderPixels    ("● 외곽선 두께 (픽셀)",  Float) = 2
        _SegBorderPixels ("● 칸 내부 좌우 테두리 두께 (픽셀)", Float) = 1

        [Header(System Do Not Modify)]
        _StencilComp     ("Stencil Comparison", Float) = 8
        _Stencil         ("Stencil ID",         Float) = 0
        _StencilOp       ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask",  Float) = 255
        _ColorMask       ("Color Mask",         Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off  Lighting Off  ZWrite Off
        ZTest    [unity_GUIZTestMode]
        Blend    SrcAlpha OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            Name "UnifiedHealthBar"
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // _GHOST_FILL_ON: 런타임에 EnableKeyword로 켜므로 multi_compile_local 필수
            // shader_feature_local 로 하면 빌드 시 Variant가 스트리핑되어 작동 안 함
            #pragma multi_compile_local _ _GHOST_FILL_ON
            // 나머지는 Material에 미리 구워지므로 shader_feature_local 유지
            #pragma shader_feature_local _GLOW_ON
            #pragma shader_feature_local _INNER_GLOW_ON
            #pragma shader_feature_local _SHIMMER_ON
            #pragma shader_feature_local _TRANSPARENT_GAP_ON
            #pragma shader_feature_local _PARTIAL_FILL_ON
            #pragma shader_feature_local _SLIDER_MODE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _FillAmount;
                float  _SegmentCount;
                float4 _ColorFill;
                float4 _ColorFillDim;
                float4 _ColorEmpty;
                float4 _ColorEmptyBorder;
                float4 _ColorFillBorder;
                float4 _ColorOutline;
                float4 _ColorGap;
                float4 _ColorDanger;
                float  _DangerThreshold;
                float  _DangerPulseSpeed;
                // 고스트: C# Update() 가 매 프레임 갱신하는 단순 float 값
                float  _GhostFill;
                float4 _ColorGhost;
                // 외부 글로우
                float4 _ColorGlow;
                float  _GlowOuterPixels;
                float  _GlowIntensity;
                // 내부 글로우
                float  _InnerGlowIntensity;
                float  _InnerGlowWidth;
                // 시머
                float  _ShimmerSpeed;
                float  _ShimmerWidth;
                float  _ShimmerIntensity;
                // 픽셀
                float4 _BarPixelSize;
                float  _GapPixels;
                float  _BorderPixels;
                float  _SegBorderPixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.color       = IN.color;
                return OUT;
            }

            bool IsGapPosition(float innerPxX, float unitPx, float segPx, float segCount)
            {
                float idx    = floor(innerPxX / unitPx);
                float localX = fmod(innerPxX, unitPx);
                return localX >= segPx && idx < segCount - 1.0;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // ── STEP 1. 픽셀 좌표 ─────────────────────────────────────────
                float barW = _BarPixelSize.x;
                float barH = _BarPixelSize.y;

            #if defined(_GLOW_ON)
                float gop    = _GlowOuterPixels;
                float totalW = barW + gop * 2.0;
                float totalH = barH + gop * 2.0;
            #else
                float gop    = 0.0;
                float totalW = barW;
                float totalH = barH;
            #endif

                float pixelX = floor(uv.x * totalW);
                float pixelY = floor(uv.y * totalH);
                float barX   = pixelX - gop;
                float barY   = pixelY - gop;

                // ── STEP 2. 공통 사전 계산 ────────────────────────────────────
                float bp       = _BorderPixels;
                float innerPxW = barW - bp * 2.0;
                float segCount  = max(floor(_SegmentCount), 1.0);
                float gapCount  = max(segCount - 1.0, 0.0);
                float segPx     = max(floor((innerPxW - gapCount * _GapPixels) / segCount), 1.0);
                float unitPx    = segPx + _GapPixels;
                float lastContentPx = (segCount - 1.0) * unitPx + segPx - 1.0;
                float fillEdgePx    = min(_FillAmount * innerPxW, lastContentPx + 1.0);

                bool  isDanger = _FillAmount <= _DangerThreshold;
                float pulse    = isDanger ? abs(sin(_Time.y * _DangerPulseSpeed)) : 0.0;
                float4 fillColor  = lerp(_ColorFill, _ColorDanger, pulse);
                float4 emptyColor = _ColorEmpty;

            #if defined(_GLOW_ON)
                float4 glowColor = lerp(_ColorGlow, _ColorDanger, pulse * 0.6);
            #endif

            #if defined(_GHOST_FILL_ON)
                // 고스트 비율 (현재HP 이상을 보장)
                // _GhostFill 은 C# Update() 가 매 프레임 lerp 로 갱신합니다.
                float ghostFill = max(_GhostFill, _FillAmount);
            #endif

                // ── STEP 3. 바 외부 → 글로우 ─────────────────────────────────
            #if defined(_GLOW_ON)
                bool insideBar = barX >= 0.0 && barX < barW &&
                                 barY >= 0.0 && barY < barH;
                if (!insideBar)
                {
                    if (_FillAmount < 0.001) return float4(0,0,0,0);
                    float dx    = max(0.0, max(-barX, barX - barW + 1.0));
                    float dy    = max(0.0, max(-barY, barY - barH + 1.0));
                    float glowT = saturate(1.0 - sqrt(dx*dx + dy*dy) / max(gop, 0.001));
                    glowT = glowT * glowT * glowT;

                    float nearInnerX = clamp(barX - bp, 0.0, lastContentPx);
                    float fillFactor = 0.0;
                    if (!IsGapPosition(nearInnerX, unitPx, segPx, segCount))
                    {
                    #if defined(_SLIDER_MODE_ON)
                        fillFactor = saturate((fillEdgePx - nearInnerX + 1.0) / 2.0);
                    #elif defined(_PARTIAL_FILL_ON)
                        float fg = _FillAmount * segCount;
                        float eg = min(floor(fg)*unitPx + frac(fg)*segPx, lastContentPx);
                        fillFactor = saturate((eg - nearInnerX + 1.0) / 2.0);
                    #else
                        fillFactor = saturate(_FillAmount * segCount - floor(nearInnerX / unitPx));
                    #endif
                    }
                    float alpha = glowT * _GlowIntensity * fillFactor * glowColor.a;
                    return float4(glowColor.rgb, alpha) * IN.color;
                }
            #endif

                // ── STEP 4. 외곽선 ───────────────────────────────────────────
                bool isOutline = barX < bp || barX >= barW - bp ||
                                 barY < bp || barY >= barH - bp;
                if (isOutline) return _ColorOutline * IN.color;

                // ── STEP 5. 내부 좌표 + 잔여 픽셀 ────────────────────────────
                float innerPxX = barX - bp;
                if (innerPxX > lastContentPx) return _ColorOutline * IN.color;
                float segIdx = floor(innerPxX / unitPx);
                float localX = fmod(innerPxX, unitPx);
                if (segIdx >= segCount) return _ColorOutline * IN.color;

                // ── STEP 6. 채움 판정 ─────────────────────────────────────────
                bool isFilled;

            #if defined(_SLIDER_MODE_ON)
                isFilled = innerPxX < fillEdgePx;

            #if defined(_GHOST_FILL_ON)
                // 고스트 구간: 현재HP ~ 고스트HP 사이
                // 셰이더는 그냥 ghostFill 값을 읽어서 구간을 색칠합니다.
                if (!isFilled)
                {
                    float ghostEdge = min(ghostFill * innerPxW, lastContentPx + 1.0);
                    if (innerPxX < ghostEdge)
                        return _ColorGhost * IN.color;
                }
            #endif

                bool isDivider = localX >= segPx && segIdx < segCount - 1.0;
                if (isDivider)
                {
                #if defined(_TRANSPARENT_GAP_ON)
                    return float4(0,0,0,0);
                #else
                    return _ColorGap * IN.color;
                #endif
                }

            #else
                // 세그먼트 모드
                bool isGap = localX >= segPx && segIdx < segCount - 1.0;
                if (isGap)
                {
                #if defined(_TRANSPARENT_GAP_ON)
                    return float4(0,0,0,0);
                #else
                    return _ColorGap * IN.color;
                #endif
                }

                float sb          = _SegBorderPixels;
                bool  isSegBorder = localX < sb || localX >= segPx - sb;
                float filledF  = _FillAmount * segCount;
                float fullSegs = floor(filledF);
                float partial  = frac(filledF);

                if (segIdx < fullSegs)
                {
                    isFilled = true;
                    if (isSegBorder) return _ColorFillBorder * IN.color;
                }
            #if defined(_PARTIAL_FILL_ON)
                else if (segIdx < fullSegs + 1.0 && partial > 0.001)
                {
                    if (isSegBorder) return _ColorEmptyBorder * IN.color;
                    float fillInSeg    = partial * (segPx - sb * 2.0);
                    float contentLocalX = localX - sb;
                    return (contentLocalX < fillInSeg ? _ColorFillDim : emptyColor)
                           * IN.color;
                }
            #endif
            #if defined(_GHOST_FILL_ON)
                // 고스트 구간: 현재HP 칸 ~ 고스트HP 칸 사이
                else if (segIdx < ghostFill * segCount)
                {
                    return _ColorGhost * IN.color;
                }
            #endif
                else
                {
                    isFilled = false;
                    if (isSegBorder) return _ColorEmptyBorder * IN.color;
                }
            #endif

                // ── STEP 7. 내부 글로우 ───────────────────────────────────────
            #if defined(_INNER_GLOW_ON)
                if (_FillAmount > 0.001 && _FillAmount < 0.999)
                {
                    float fg2  = _FillAmount * segCount;
                    float igE  = min(floor(fg2)*unitPx + frac(fg2)*segPx, lastContentPx);
                    float igT  = saturate(1.0 - abs(innerPxX - igE) / max(_InnerGlowWidth, 0.001));
                    igT = igT * igT;
                #if defined(_GLOW_ON)
                    if (isFilled) fillColor  = lerp(fillColor,  glowColor, igT * _InnerGlowIntensity);
                    else          emptyColor = lerp(emptyColor, glowColor, igT * 0.5);
                #else
                    float4 ec = isDanger ? _ColorDanger : _ColorFill;
                    if (isFilled) fillColor  = lerp(fillColor,  ec, igT * _InnerGlowIntensity);
                    else          emptyColor = lerp(emptyColor, ec, igT * 0.3);
                #endif
                }
            #endif

                // ── STEP 8. 시머 ──────────────────────────────────────────────
            #if defined(_SHIMMER_ON)
                if (isFilled && _ShimmerIntensity > 0.001)
                {
                    float sp  = frac(_Time.y * _ShimmerSpeed) * (lastContentPx + 1.0);
                    float st  = saturate(1.0 - abs(innerPxX - sp) / max(_ShimmerWidth, 0.001));
                    st = st * st;
                    fillColor = lerp(fillColor, float4(1,1,1,fillColor.a), st * _ShimmerIntensity);
                }
            #endif

                return (isFilled ? fillColor : emptyColor) * IN.color;
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}
