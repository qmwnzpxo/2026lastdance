// =============================================================================
//  UnifiedHealthBarVisual.cs
//
//  [ 고스트 HP 동작 방식 — 완전 자동 ]
//  Update() 가 매 프레임 _GhostFill 을 _FillAmount 쪽으로 자동으로 lerp 합니다.
//  별도로 호출할 필요 없습니다.
//
//  사용 방법:
//    1. Image 에 이 컴포넌트 추가
//    2. Material Inspector 에서 "◆ 고스트 HP ON/OFF" 체크
//    3. _FillAmount 가 바뀌면 고스트가 자동으로 뒤따라옴
//
//  타이밍 조절 (Inspector):
//    Ghost Delay → 데미지 후 고스트가 멈춰있는 시간 (초)
//    Ghost Speed → 고스트가 따라오는 속도 (1초에 몇 % 이동)
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

[RequireComponent(typeof(Image))]
public class UnifiedHealthBarVisual : MonoBehaviour
{
    // ── 셰이더 키워드 ─────────────────────────────────────────────────────────
    const string KW_GHOST_FILL      = "_GHOST_FILL_ON";
    const string KW_GLOW            = "_GLOW_ON";
    const string KW_INNER_GLOW      = "_INNER_GLOW_ON";
    const string KW_SHIMMER         = "_SHIMMER_ON";
    const string KW_TRANSPARENT_GAP = "_TRANSPARENT_GAP_ON";
    const string KW_PARTIAL_FILL    = "_PARTIAL_FILL_ON";
    const string KW_SLIDER_MODE     = "_SLIDER_MODE_ON";

    // ── Property ID ───────────────────────────────────────────────────────────
    static readonly int ID_Fill            = Shader.PropertyToID("_FillAmount");
    static readonly int ID_PixelSize       = Shader.PropertyToID("_BarPixelSize");
    static readonly int ID_UseGlow         = Shader.PropertyToID("_UseGlow");
    static readonly int ID_GlowOuterPixels = Shader.PropertyToID("_GlowOuterPixels");
    static readonly int ID_GhostFill       = Shader.PropertyToID("_GhostFill");

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("필수 연결")]
    [SerializeField] Canvas   rootCanvas;
    [SerializeField] Material sourceMaterial;

    [Header("바 크기")]
    [SerializeField] Vector2 barSize = new Vector2(400f, 20f);

    [Header("고스트 HP 타이밍")]
    [Tooltip("데미지 후 고스트가 멈춰있는 시간 (초)\n" +
             "이 시간 동안 고스트 HP가 눈에 보입니다.")]
    public float ghostDelay = 0.6f;

    [Tooltip("고스트가 현재 HP 를 따라오는 속도\n" +
             "1.0 = 1초에 100% 이동 (빠름)\n" +
             "0.3 = 1초에 30% 이동 (느림)")]
    public float ghostSpeed = 0.4f;

    // ── 내부 변수 ─────────────────────────────────────────────────────────────
    Material      mat;
    RectTransform rt;

    float ghostFill   = 1f;
    float currentFill = 1f;
    float holdTimer   = 0f;

    // 흔들림 원점 고정 + 취소 토큰
    // 연속 데미지 시 이전 흔들림을 취소하고 원점을 유지합니다.
    Vector2           shakeOrigin;
    CancellationTokenSource shakeCts;
    bool              isShaking; // 현재 흔들림 진행 중 여부

    // ── Unity 생명주기 ────────────────────────────────────────────────────────

    void Awake()
    {
        var img = GetComponent<Image>();
        rt = GetComponent<RectTransform>();

        if (sourceMaterial == null)
        {
            Debug.LogError("[UnifiedHealthBarVisual] Source Material 연결 필요!", this);
            return;
        }

        mat = new Material(sourceMaterial);
        mat.name = sourceMaterial.name + " (Instance)";
        img.material = mat;

        mat.SetFloat(ID_GhostFill, 1f);
    }

    void OnDestroy()
    {
        // CancellationTokenSource 해제 (메모리 누수 방지)
        shakeCts?.Cancel();
        shakeCts?.Dispose();
        if (mat != null) Destroy(mat);
    }

    void OnEnable() => UpdatePixelSize();

    void OnRectTransformDimensionsChange()
    {
        if (mat == null) return;
        if (!mat.IsKeywordEnabled(KW_GLOW)) UpdatePixelSize();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Update: 고스트 HP 자동 추적
    //
    //  매 프레임 실행됩니다.
    //  currentFill(현재 HP) 과 ghostFill(고스트 HP) 을 비교해서:
    //    ghostFill > currentFill: 고스트가 위에 있음 → 딜레이 후 현재HP 쪽으로 이동
    //    ghostFill ≤ currentFill: 회복됨 → 고스트를 즉시 현재HP 로 스냅
    //
    //  별도로 호출하지 않아도 됩니다.
    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (mat == null) return;

        // 셰이더의 현재 _FillAmount 를 읽어서 추적
        // (SetFill() 을 통하지 않고 직접 _FillAmount 를 설정해도 동작합니다)
        currentFill = mat.GetFloat(ID_Fill);

        if (ghostFill > currentFill + 0.001f)
        {
            // ── 고스트가 현재HP 보다 높음: 딜레이 후 서서히 감소 ─────────────
            if (holdTimer > 0f)
            {
                // 아직 딜레이 중: 고스트 위치 고정
                holdTimer -= Time.deltaTime;
            }
            else
            {
                // 딜레이 끝: 고스트를 현재HP 쪽으로 이동
                // MoveTowards: 선형 이동 (Lerp 와 달리 끝에서 딱 멈춤)
                ghostFill = Mathf.MoveTowards(ghostFill, currentFill,
                                               ghostSpeed * Time.deltaTime);
            }
        }
        else
        {
            // ── 현재HP가 고스트 이상: 회복됨 → 고스트 즉시 동기화 ────────────
            ghostFill = currentFill;
            // 다음 데미지를 위해 딜레이 타이머 리셋
            holdTimer = ghostDelay;
        }

        // 매 프레임 셰이더에 고스트 값 전달
        mat.SetFloat(ID_GhostFill, ghostFill);
    }

    // ── 픽셀 크기 주입 ────────────────────────────────────────────────────────
    void UpdatePixelSize()
    {
        if (mat == null || rootCanvas == null) return;
        float scale  = rootCanvas.scaleFactor;
        bool  glowOn = mat.IsKeywordEnabled(KW_GLOW);
        float pixelW = glowOn ? Mathf.Round(barSize.x * scale)
                               : Mathf.Round(rt.rect.width  * scale);
        float pixelH = glowOn ? Mathf.Round(barSize.y * scale)
                               : Mathf.Round(rt.rect.height * scale);
        mat.SetVector(ID_PixelSize, new Vector4(pixelW, pixelH, 0f, 0f));
    }

    // =========================================================================
    //  공개 API
    // =========================================================================

    /// <summary>채움 비율 설정 (0.0 ~ 1.0)</summary>
    public void SetFill(float ratio)
    {
        if (mat == null) return;
        mat.SetFloat(ID_Fill, Mathf.Clamp01(ratio));
        // Update() 가 다음 프레임부터 자동으로 고스트를 처리합니다.
    }

    // ── Toggle API ────────────────────────────────────────────────────────────

    /// <summary>고스트 HP ON/OFF</summary>
    public void SetGhostFill(bool enabled)
    {
        if (enabled) mat?.EnableKeyword(KW_GHOST_FILL);
        else         mat?.DisableKeyword(KW_GHOST_FILL);
    }

    /// <summary>외부 글로우 ON/OFF</summary>
    public void SetGlow(bool enabled)
    {
        if (mat == null) return;
        if (enabled)
        {
            mat.EnableKeyword(KW_GLOW);
            mat.SetFloat(ID_UseGlow, 1f);
            rt.sizeDelta = barSize + Vector2.one * mat.GetFloat(ID_GlowOuterPixels) * 2f;
        }
        else
        {
            mat.DisableKeyword(KW_GLOW);
            mat.SetFloat(ID_UseGlow, 0f);
            rt.sizeDelta = barSize;
        }
        UpdatePixelSize();
    }

    /// <summary>내부 글로우 ON/OFF</summary>
    public void SetInnerGlow(bool enabled)
    {
        if (enabled) mat?.EnableKeyword(KW_INNER_GLOW);
        else         mat?.DisableKeyword(KW_INNER_GLOW);
    }

    /// <summary>시머 ON/OFF</summary>
    public void SetShimmer(bool enabled)
    {
        if (enabled) mat?.EnableKeyword(KW_SHIMMER);
        else         mat?.DisableKeyword(KW_SHIMMER);
    }

    /// <summary>투명 갭 ON/OFF</summary>
    public void SetTransparentGap(bool enabled)
    {
        if (enabled) mat?.EnableKeyword(KW_TRANSPARENT_GAP);
        else         mat?.DisableKeyword(KW_TRANSPARENT_GAP);
    }

    /// <summary>부분 채움 ON/OFF</summary>
    public void SetPartialFill(bool enabled)
    {
        if (enabled) mat?.EnableKeyword(KW_PARTIAL_FILL);
        else         mat?.DisableKeyword(KW_PARTIAL_FILL);
    }

    /// <summary>슬라이더 모드 ON/OFF</summary>
    public void SetSliderMode(bool enabled)
    {
        if (enabled) mat?.EnableKeyword(KW_SLIDER_MODE);
        else         mat?.DisableKeyword(KW_SLIDER_MODE);
    }

    // ── 피격 흔들림 (UniTask) ─────────────────────────────────────────────────

    /// <summary>피격 흔들림 (기본 강도)</summary>
    public void PlayShake(float duration = 0.25f)
        => StartShake(duration, new Vector2(8f, 4f));

    /// <summary>피격 흔들림 (강도 직접 지정, 픽셀 단위)</summary>
    public void PlayShake(float duration, Vector2 strength)
        => StartShake(duration, strength);

    /// <summary>데미지 크기에 따라 흔들림 강도 자동 조절</summary>
    public void PlayShakeByDamage(float damage, float maxHP, float duration = 0.25f)
    {
        float r = Mathf.Clamp01(damage / maxHP);
        StartShake(duration,
            Vector2.Lerp(new Vector2(3f, 1.5f), new Vector2(16f, 8f), r));
    }

    void StartShake(float duration, Vector2 strength)
    {
        // 이전 흔들림 취소
        shakeCts?.Cancel();
        shakeCts?.Dispose();
        shakeCts = new CancellationTokenSource();

        // 흔들림 중이 아닐 때만 원점 갱신
        // 연속 흔들림 중에는 shakeOrigin 유지 → 위치 어긋남 방지
        if (!isShaking)
            shakeOrigin = rt.anchoredPosition;

        ShakeAsync(duration, strength, shakeCts.Token).Forget();
    }

    async UniTaskVoid ShakeAsync(float duration, Vector2 strength, CancellationToken ct)
    {
        Vector2 origin  = shakeOrigin;
        float   elapsed = 0f;
        isShaking = true;

        try
        {
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();

                float t = 1f - elapsed / duration;
                rt.anchoredPosition = origin +
                    Vector2.Scale(UnityEngine.Random.insideUnitCircle, strength * t);

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 새 흔들림이 이어서 시작됨 → 원점/isShaking 은 새 태스크가 관리
            return;
        }

        // 정상 완료 → 원점 복귀 후 플래그 해제
        rt.anchoredPosition = origin;
        isShaking = false;
    }
}
