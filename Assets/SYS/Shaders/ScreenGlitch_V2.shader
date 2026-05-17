Shader "URP2D/FX/ScreenGlitch_V2"
{
    Properties
    {
        // ══════════════════════════════════════════════════════════
        // 전체 컨트롤
        // ══════════════════════════════════════════════════════════
        [Header(GLOBAL CONTROL)]
        _Intensity      ("전체 강도",                   Range(0, 1))    = 0
        _Alpha          ("전체 투명도",                  Range(0, 1))    = 1
        _GlitchColor    ("글리치 기본 색상",              Color)          = (0.2, 1, 0.9, 1)

        // ══════════════════════════════════════════════════════════
        // 공통 노이즈 형태 설정
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(NOISE SHAPE)]
        [Toggle(_USE_NOISE_TEX)]
        _UseNoiseTex    ("텍스처 노이즈 사용 꺼짐시 프로시저럴", Float) = 0
        [NoScaleOffset]
        _NoiseTex       ("노이즈 텍스처 흑백권장",          2D)             = "white" {}

        _NoiseFreqX     ("노이즈 주파수 X",               Range(0.1, 50)) = 4.0
        _NoiseFreqY     ("노이즈 주파수 Y",               Range(0.1, 200))= 80.0
        _NoiseContrast  ("노이즈 대비",                   Range(0.1, 10)) = 1.0
        _NoiseBias      ("노이즈 밝기 오프셋",             Range(-1, 1))   = 0.0
        _NoiseSharpness ("노이즈 날카로움 파워커브",        Range(0.1, 8))  = 1.0
        _NoiseTiling    ("노이즈 타일링 스케일",           Range(0.1, 10)) = 1.0

        // ══════════════════════════════════════════════════════════
        // 블록 글리치
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(BLOCK GLITCH)]
        [Toggle(_BLOCK_ON)]
        _BlockOn        ("블록 글리치 활성화",             Float)          = 1
        _BlockSize      ("블록 크기",                    Range(0.005, 0.5)) = 0.05
        _BlockSpeed     ("블록 속도",                    Range(0, 50))   = 12
        _BlockStrength  ("블록 강도 UV오프셋",             Range(0, 0.5))  = 0.15
        _BlockFrequency ("발생 빈도",                    Range(0, 1))    = 0.5
        _BlockNoiseMix  ("노이즈 형태 반영 비율",          Range(0, 1))    = 0.5

        // ══════════════════════════════════════════════════════════
        // RGB 색수차
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(RGB CHROMA)]
        [Toggle(_CHROMA_ON)]
        _ChromaOn       ("RGB 색수차 활성화",              Float)          = 1
        _ChromaStrength ("색수차 강도",                   Range(0, 0.1))  = 0.02
        _ChromaAngle    ("분리 각도 도 단위",              Range(0, 360))  = 0
        _ChromaSpeed    ("각도 애니메이션 속도",            Range(0, 10))   = 0

        // ══════════════════════════════════════════════════════════
        // 스캔라인
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(SCANLINE)]
        [Toggle(_SCAN_ON)]
        _ScanOn         ("스캔라인 활성화",                Float)          = 1
        _ScanDensity    ("밀도",                         Range(10, 800)) = 120
        _ScanStrength   ("강도",                         Range(0, 1))    = 0.25
        _ScanSpeed      ("스크롤 속도",                   Range(-10, 10)) = 1
        _ScanThickness  ("선 두께",                      Range(0.01, 1)) = 0.5
        _ScanColor      ("스캔라인 색상",                  Color)          = (0, 1, 0.8, 1)
        _ScanNoise      ("스캔라인 노이즈 양",              Range(0, 1))    = 0.2

        // ══════════════════════════════════════════════════════════
        // 수직 점프 (화면 찢김)
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(VERTICAL JUMP)]
        [Toggle(_VJUMP_ON)]
        _VJumpOn        ("수직 점프 활성화",                Float)          = 1
        _VJumpStrength  ("점프 강도",                    Range(0, 1))    = 0.1
        _VJumpSpeed     ("점프 속도",                    Range(0, 20))   = 4
        _VJumpFrequency ("발생 빈도",                    Range(0, 1))    = 0.3

        // ══════════════════════════════════════════════════════════
        // 수평 드리프트
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(HORIZONTAL DRIFT)]
        [Toggle(_DRIFT_ON)]
        _DriftOn        ("수평 드리프트 활성화",            Float)          = 1
        _DriftSpeed     ("드리프트 속도",                 Range(0, 10))   = 2
        _DriftStrength  ("드리프트 강도",                 Range(0, 0.15)) = 0.02
        _DriftDensity   ("라인 밀도",                    Range(5, 200))  = 40

        // ══════════════════════════════════════════════════════════
        // 필름 그레인
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(FILM GRAIN)]
        [Toggle(_GRAIN_ON)]
        _GrainOn        ("필름 그레인 활성화",              Float)          = 0
        _GrainIntensity ("그레인 강도",                   Range(0, 1))    = 0.3
        _GrainSize      ("그레인 크기",                   Range(0.5, 10)) = 1
        _GrainSpeed     ("그레인 속도",                   Range(0, 60))   = 20
        _GrainColored   ("컬러 그레인 0흑백 1컬러",         Range(0, 1))    = 0

        // ══════════════════════════════════════════════════════════
        // 픽셀화
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(PIXELATE)]
        [Toggle(_PIXEL_ON)]
        _PixelOn        ("픽셀화 활성화",                  Float)          = 0
        _PixelSize      ("픽셀 크기",                    Range(1, 128))  = 4

        // ══════════════════════════════════════════════════════════
        // 배럴 왜곡
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(BARREL DISTORTION)]
        [Toggle(_BARREL_ON)]
        _BarrelOn       ("배럴 왜곡 활성화",                Float)          = 0
        _BarrelStrength ("왜곡 강도 음수오목 양수볼록",       Range(-1, 1))   = 0.2
        _BarrelPulse    ("왜곡 진동 속도 0이면 고정",        Range(0, 10))   = 0

        // ══════════════════════════════════════════════════════════
        // 노이즈 레이어 (다중 옥타브 fBm)
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(NOISE LAYER fBm)]
        [Toggle(_NOISELAYER_ON)]
        _NoiseLayerOn   ("노이즈 레이어 활성화",             Float)          = 0
        _NoiseScale     ("레이어 스케일",                  Range(1, 100))  = 10
        _NoiseOctaves   ("옥타브 수 1에서4",               Range(1, 4))    = 2
        _NoiseLacunarity("라쿠나리티 주파수 배율",           Range(1, 4))    = 2
        _NoisePersist   ("퍼시스턴스 옥타브 감쇠",          Range(0.1, 1))  = 0.5
        _NoiseLayerSpeed("흐름 속도",                    Range(0, 10))   = 1
        _NoiseStrength  ("레이어 강도",                   Range(0, 1))    = 0.3
        _NoiseDirX      ("흐름 방향 X",                   Range(-1, 1))   = 0.3
        _NoiseDirY      ("흐름 방향 Y",                   Range(-1, 1))   = 0.1

        // ══════════════════════════════════════════════════════════
        // 비네트
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(VIGNETTE)]
        [Toggle(_VIGNETTE_ON)]
        _VignetteOn     ("비네트 활성화",                  Float)          = 0
        _VignetteRadius ("반경",                         Range(0.1, 2))  = 0.8
        _VignetteSoft   ("부드러움",                     Range(0.01, 1)) = 0.4
        _VignetteColor  ("비네트 색상",                   Color)          = (0, 0, 0, 1)
        _VignettePulse  ("펄스 속도 0이면 고정",           Range(0, 5))    = 0

        // ══════════════════════════════════════════════════════════
        // 색상 플래시
        // ══════════════════════════════════════════════════════════
        [Space(8)]
        [Header(COLOR FLASH)]
        [Toggle(_FLASH_ON)]
        _FlashOn        ("색상 플래시 활성화",              Float)          = 1
        _FlashColor     ("플래시 색상",                   Color)          = (0.2, 1, 0.9, 1)
        _FlashRate      ("발생 빈도",                    Range(0, 30))   = 6
        _FlashStrength  ("플래시 강도",                   Range(0, 1))    = 0.4
        _FlashDuration  ("지속 시간 비율",                Range(0.01, 0.5)) = 0.05

        [Space(8)]
        [Header(NOISE SCATTER)]
        [Toggle(_SCATTER_ON)]
        _ScatterOn         ("노이즈 산포 활성화",            Float)          = 0
        _ScatterCellDensity("셀 밀도 화면 분할 수",          Range(2, 30))   = 8
        _ScatterPatchScale ("패치 크기 배율",               Range(0.1, 3))  = 1.0
        _ScatterFrequency  ("활성 셀 비율",                 Range(0, 1))    = 0.4
        _ScatterSpeed      ("패치 변화 속도",               Range(0, 20))   = 5
        _ScatterStrength   ("산포 강도",                   Range(0, 2))    = 1.0
        _ScatterColorized  ("채널 컬러 분리 0모노 1컬러",    Range(0, 1))    = 1.0
        _ScatterRotation   ("패치 회전 랜덤 강도",          Range(0, 1))    = 0.3
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Overlay"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ScreenGlitch"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma shader_feature_local _USE_NOISE_TEX
            #pragma shader_feature_local _BLOCK_ON
            #pragma shader_feature_local _CHROMA_ON
            #pragma shader_feature_local _SCAN_ON
            #pragma shader_feature_local _VJUMP_ON
            #pragma shader_feature_local _DRIFT_ON
            #pragma shader_feature_local _GRAIN_ON
            #pragma shader_feature_local _PIXEL_ON
            #pragma shader_feature_local _BARREL_ON
            #pragma shader_feature_local _NOISELAYER_ON
            #pragma shader_feature_local _VIGNETTE_ON
            #pragma shader_feature_local _FLASH_ON
            #pragma shader_feature_local _SCATTER_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PI_VAL 3.14159265359

            // ── 텍스처 (CBUFFER 밖) ──────────────────────────────────
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            // ── CBUFFER ─────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                // 전체
                float  _Intensity;
                float  _Alpha;
                float4 _GlitchColor;
                // 공통 노이즈 형태
                float  _NoiseFreqX;
                float  _NoiseFreqY;
                float  _NoiseContrast;
                float  _NoiseBias;
                float  _NoiseSharpness;
                float  _NoiseTiling;
                // 블록
                float  _BlockSize;
                float  _BlockSpeed;
                float  _BlockStrength;
                float  _BlockFrequency;
                float  _BlockNoiseMix;
                // 색수차
                float  _ChromaStrength;
                float  _ChromaAngle;
                float  _ChromaSpeed;
                // 스캔라인
                float  _ScanDensity;
                float  _ScanStrength;
                float  _ScanSpeed;
                float  _ScanThickness;
                float4 _ScanColor;
                float  _ScanNoise;
                // 수직 점프
                float  _VJumpStrength;
                float  _VJumpSpeed;
                float  _VJumpFrequency;
                // 드리프트
                float  _DriftSpeed;
                float  _DriftStrength;
                float  _DriftDensity;
                // 필름 그레인
                float  _GrainIntensity;
                float  _GrainSize;
                float  _GrainSpeed;
                float  _GrainColored;
                // 픽셀화
                float  _PixelSize;
                // 배럴
                float  _BarrelStrength;
                float  _BarrelPulse;
                // 노이즈 레이어
                float  _NoiseScale;
                float  _NoiseOctaves;
                float  _NoiseLacunarity;
                float  _NoisePersist;
                float  _NoiseLayerSpeed;
                float  _NoiseStrength;
                float  _NoiseDirX;
                float  _NoiseDirY;
                // 비네트
                float  _VignetteRadius;
                float  _VignetteSoft;
                float4 _VignetteColor;
                float  _VignettePulse;
                // 플래시
                float4 _FlashColor;
                float  _FlashRate;
                float  _FlashStrength;
                float  _FlashDuration;
                // 노이즈 산포
                float  _ScatterCellDensity;
                float  _ScatterPatchScale;
                float  _ScatterFrequency;
                float  _ScatterSpeed;
                float  _ScatterStrength;
                float  _ScatterColorized;
                float  _ScatterRotation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            // ════════════════════════════════════════════════════════
            // 해시 & 프로시저럴 노이즈
            // ════════════════════════════════════════════════════════
            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }
            float hash11(float p)
            {
                p = frac(p * 0.1031); p *= p + 33.33; p *= p + p;
                return frac(p);
            }
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }
            float valueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash21(i),               hash21(i + float2(1,0)), u.x),
                    lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x), u.y);
            }
            float fbm(float2 p, int oct, float lac, float per)
            {
                float v = 0, a = 0.5, fr = 1.0;
                for (int i = 0; i < 4; i++)
                {
                    if (i >= oct) break;
                    v += valueNoise(p * fr) * a;
                    fr *= lac; a *= per;
                }
                return v;
            }

            // ════════════════════════════════════════════════════════
            // 공통 노이즈 샘플 함수
            // — 텍스처 / 프로시저럴 분기 + 형태 파라미터 적용
            // ════════════════════════════════════════════════════════
            float sampleNoise(float2 uv)
            {
                float2 freq = float2(_NoiseFreqX, _NoiseFreqY) * _NoiseTiling;
                float  n;

                #ifdef _USE_NOISE_TEX
                    // 텍스처 노이즈: R채널 사용, 타일 반복
                    n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex,
                                         frac(uv * freq)).r;
                #else
                    // 프로시저럴 Value Noise
                    n = valueNoise(uv * freq);
                #endif

                // 형태 파라미터 적용
                n = saturate((n - (0.5 + _NoiseBias * 0.5)) * _NoiseContrast + 0.5);
                n = pow(max(n, 0.0001), _NoiseSharpness);
                return n;
            }

            // 채널별 오프셋 노이즈 (색수차용)
            float3 sampleNoiseRGB(float2 uv, float t)
            {
                return float3(
                    sampleNoise(uv + float2(t * 0.4,   0)),
                    sampleNoise(uv + float2(0,         t * 0.25)),
                    sampleNoise(uv + float2(t * 0.6,   t * 0.15))
                );
            }

            // ════════════════════════════════════════════════════════
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float I = _Intensity;
                if (I <= 0.001) return half4(0, 0, 0, 0);

                float  t  = _Time.y;
                float2 uv = IN.uv;

                // ── 배럴 왜곡 ───────────────────────────────────────
                #ifdef _BARREL_ON
                {
                    float2 c = uv - 0.5;
                    float bAmt = _BarrelStrength * I;
                    if (_BarrelPulse > 0.001) bAmt *= sin(t * _BarrelPulse) * 0.5 + 0.5;
                    uv = 0.5 + c * (1.0 + bAmt * dot(c, c));
                }
                #endif

                // ── 수직 점프 ───────────────────────────────────────
                #ifdef _VJUMP_ON
                {
                    float jT   = floor(t * _VJumpSpeed);
                    float jOn  = step(1.0 - _VJumpFrequency * I, hash11(jT * 0.37));
                    float jAmt = (hash11(jT * 1.73) - 0.5) * _VJumpStrength * I * jOn;
                    uv.y = frac(uv.y + jAmt);
                }
                #endif

                // ── 블록 글리치 ─────────────────────────────────────
                float blockActive = 0.0;
                #ifdef _BLOCK_ON
                {
                    float bRow = floor(uv.y / max(_BlockSize, 0.001));
                    float bT   = floor(t * _BlockSpeed);

                    // 블록 트리거: 순수 해시 vs 노이즈 형태 반영 믹스
                    float pureRand  = hash21(float2(bRow, bT));
                    float noiseRand = sampleNoise(float2(bRow * _BlockSize, bT * 0.01));
                    float blended   = lerp(pureRand, noiseRand, _BlockNoiseMix);
                    blockActive = step(1.0 - _BlockFrequency * I, blended);

                    float bOff = (hash21(float2(bRow * 1.7, bT + 1.3)) - 0.5)
                                 * _BlockStrength * I * blockActive;
                    uv.x = frac(uv.x + bOff);
                }
                #endif

                // ── 수평 드리프트 ───────────────────────────────────
                #ifdef _DRIFT_ON
                {
                    float dRow = floor(uv.y * _DriftDensity);
                    float dOff = (valueNoise(float2(dRow * 0.3, t * _DriftSpeed)) - 0.5)
                                 * _DriftStrength * I;
                    uv.x = frac(uv.x + dOff);
                }
                #endif

                // ── 픽셀화 ──────────────────────────────────────────
                #ifdef _PIXEL_ON
                {
                    float res = 256.0 / max(_PixelSize, 1.0);
                    uv = floor(uv * res) / res;
                }
                #endif

                // ── 색상 생성 ───────────────────────────────────────
                float3 col = float3(0, 0, 0);

                #ifdef _CHROMA_ON
                {
                    float  angle = (_ChromaAngle + t * _ChromaSpeed * 60.0)
                                   * (PI_VAL / 180.0);
                    float2 dir   = float2(cos(angle), sin(angle)) * _ChromaStrength * I;

                    // 색수차: 각 채널 UV를 조금씩 다르게 오프셋 후 공통 노이즈 샘플
                    float3 nRGB  = sampleNoiseRGB(frac(uv), t);
                    float  nR    = sampleNoise(frac(uv + dir));
                    float  nG    = sampleNoise(frac(uv));
                    float  nB    = sampleNoise(frac(uv - dir));

                    // 블록 영역 밖은 약하게
                    nR = lerp(nR * 0.08, nR, blockActive);
                    nG = lerp(nG * 0.08, nG, blockActive);
                    nB = lerp(nB * 0.08, nB, blockActive);
                    col = float3(nR, nG, nB) * _GlitchColor.rgb;
                }
                #else
                {
                    float n = sampleNoise(frac(uv));
                    n = lerp(n * 0.08, n, blockActive);
                    col = float3(n, n, n) * _GlitchColor.rgb;
                }
                #endif

                // ── 노이즈 레이어 (fBm) ─────────────────────────────
                #ifdef _NOISELAYER_ON
                {
                    int oct = clamp((int)floor(_NoiseOctaves), 1, 4);
                    float2 fUV = uv * _NoiseScale
                               + float2(_NoiseDirX, _NoiseDirY) * t * _NoiseLayerSpeed;

                    float nv;
                    #ifdef _USE_NOISE_TEX
                        // 텍스처 fBm: 옥타브마다 스케일 올리며 샘플
                        nv = 0;
                        float amp = 0.5, freq = 1.0;
                        for (int i = 0; i < 4; i++)
                        {
                            if (i >= oct) break;
                            float2 tUV = frac(fUV * freq
                                        * float2(_NoiseFreqX, _NoiseFreqY) * 0.05);
                            float raw = SAMPLE_TEXTURE2D(_NoiseTex,
                                                         sampler_NoiseTex, tUV).r;
                            raw = saturate((raw - (0.5 + _NoiseBias * 0.5))
                                           * _NoiseContrast + 0.5);
                            raw = pow(max(raw, 0.0001), _NoiseSharpness);
                            nv += raw * amp;
                            freq *= _NoiseLacunarity;
                            amp  *= _NoisePersist;
                        }
                    #else
                        nv = fbm(fUV, oct, _NoiseLacunarity, _NoisePersist);
                        nv = saturate((nv - (0.5 + _NoiseBias * 0.5))
                                      * _NoiseContrast + 0.5);
                        nv = pow(max(nv, 0.0001), _NoiseSharpness);
                    #endif

                    col += float3(nv, nv * 0.8, nv * 1.1) * _NoiseStrength * I;
                }
                #endif

                // ── 스캔라인 ────────────────────────────────────────
                float scanContrib = 0.0;
                #ifdef _SCAN_ON
                {
                    float scanY = uv.y * _ScanDensity + t * _ScanSpeed;
                    float scanLine  = abs(sin(scanY * PI_VAL));
                    scanLine = pow(scanLine, max(1.0 / max(_ScanThickness, 0.01), 1.0));
                    float scanN = hash21(float2(floor(scanY), floor(t * 12.0)));
                    scanLine = lerp(scanLine, scanLine * scanN, _ScanNoise);
                    scanContrib = scanLine * _ScanStrength * I;
                    col = lerp(col, col * _ScanColor.rgb, scanContrib);
                    col += _ScanColor.rgb * scanContrib * 0.25;
                }
                #endif

                // ── 필름 그레인 ─────────────────────────────────────
                #ifdef _GRAIN_ON
                {
                    float2 gUV = uv * (100.0 / max(_GrainSize, 0.1));
                    float  gT  = floor(t * _GrainSpeed);
                    float3 grain;
                    if (_GrainColored > 0.5)
                        grain = float3(hash21(gUV + gT),
                                       hash21(gUV + gT + float2(7.3, 2.1)),
                                       hash21(gUV + gT + float2(1.7, 9.2)));
                    else
                    { float gn = hash21(gUV + gT); grain = float3(gn, gn, gn); }
                    col = lerp(col, col + grain - 0.5, _GrainIntensity * I);
                }
                #endif

                // ── 비네트 ──────────────────────────────────────────
                #ifdef _VIGNETTE_ON
                {
                    float vR = _VignetteRadius;
                    if (_VignettePulse > 0.001) vR *= 1.0 + sin(t * _VignettePulse) * 0.1;
                    float vMask = 1.0 - smoothstep(vR - _VignetteSoft, vR, length(uv - 0.5));
                    col = lerp(_VignetteColor.rgb * col, col, vMask);
                }
                #endif

                // ── 색상 플래시 ─────────────────────────────────────
                #ifdef _FLASH_ON
                {
                    float fPhase = frac(t * _FlashRate);
                    float flash  = step(1.0 - _FlashDuration, fPhase)
                                 * step(0.65, hash11(floor(t * _FlashRate)))
                                 * _FlashStrength * I;
                    col = lerp(col, _FlashColor.rgb, flash);
                }
                #endif


                // ── 노이즈 산포 ─────────────────────────────────────
                #ifdef _SCATTER_ON
                {
                    float  sT    = floor(t * _ScatterSpeed);
                    float2 cellUV = uv * _ScatterCellDensity;
                    float2 cellID = floor(cellUV);
                    float2 cellF  = frac(cellUV);

                    float3 scatterCol = float3(0, 0, 0);
                    float  scatterA   = 0.0;

                    // 주변 3x3 셀 순회 → 패치가 경계를 넘어도 연속되게
                    for (int cx = -1; cx <= 1; cx++)
                    for (int cy = -1; cy <= 1; cy++)
                    {
                        float2 nCell = cellID + float2(cx, cy);

                        // 이 셀 활성화 여부
                        float cellRand = hash21(nCell + sT * 0.17);
                        float active   = step(1.0 - _ScatterFrequency * I, cellRand);
                        if (active < 0.5) continue;

                        // 셀 내 패치 위치 (랜덤)
                        float2 patchPos  = hash22(nCell + sT * 0.31) * 0.7 + 0.15;
                        float2 localUV   = cellF - float2(cx, cy) - patchPos;

                        // 패치 회전 (랜덤 각도)
                        float rotAngle = hash21(nCell + sT * 0.53 + 5.7)
                                         * _ScatterRotation * PI_VAL;
                        float cosA = cos(rotAngle), sinA = sin(rotAngle);
                        float2 rotUV = float2(
                            cosA * localUV.x - sinA * localUV.y,
                            sinA * localUV.x + cosA * localUV.y
                        );

                        // 패치 크기 랜덤 변형
                        float pSize  = (hash21(nCell * 2.3 + sT * 0.1) * 0.6 + 0.4)
                                       * _ScatterPatchScale;
                        float2 sUV   = rotUV / pSize + 0.5;

                        // 패치 범위 마스크 (부드러운 경계)
                        float2 edgeDist = min(sUV, 1.0 - sUV);
                        float  edgeMask = smoothstep(0.0, 0.1, min(edgeDist.x, edgeDist.y));
                        if (edgeMask < 0.01) continue;

                        // 노이즈 샘플 (공통 함수 사용 → 텍스처/프로시저럴 분기)
                        float  nR = sampleNoise(frac(sUV + float2(t * 0.1, 0)));
                        float  nG = sampleNoise(frac(sUV + float2(0.33, t * 0.07)));
                        float  nB = sampleNoise(frac(sUV + float2(t * 0.15, 0.66)));

                        // 모노 vs 컬러 분리
                        float3 patchCol;
                        if (_ScatterColorized > 0.5)
                            patchCol = float3(nR, nG, nB) * _GlitchColor.rgb;
                        else
                        {
                            float mono = (nR + nG + nB) / 3.0;
                            patchCol = float3(mono, mono, mono) * _GlitchColor.rgb;
                        }

                        scatterCol += patchCol * edgeMask;
                        scatterA    = max(scatterA, edgeMask * ((nR + nG + nB) / 3.0));
                    }

                    col += scatterCol * _ScatterStrength * I;
                    a   = saturate(a + scatterA * _ScatterStrength * I * _Alpha);
                }
                #endif
                // ── 알파 ────────────────────────────────────────────
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                float a   = saturate(lum * I * 2.0
                                   + blockActive * I * 0.4
                                   + scanContrib  * 0.4) * _Alpha;

                return half4(saturate(col), a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
