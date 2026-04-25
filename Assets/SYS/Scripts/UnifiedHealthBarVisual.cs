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

[RequireComponent(typeof(Image))]
public class UnifiedHealthBarVisual : MonoBehaviour
{
    // ── 셰이더 키워드 ─────────────────────────────────────────────────────────
    const string KW_GHOST_FILL = "_GHOST_FILL_ON";
    const string KW_GLOW = "_GLOW_ON";
    const string KW_INNER_GLOW = "_INNER_GLOW_ON";
    const string KW_SHIMMER = "_SHIMMER_ON";
    const string KW_TRANSPARENT_GAP = "_TRANSPARENT_GAP_ON";
    const string KW_PARTIAL_FILL = "_PARTIAL_FILL_ON";
    const string KW_SLIDER_MODE = "_SLIDER_MODE_ON";

    // ── Property ID ───────────────────────────────────────────────────────────
    static readonly int ID_Fill = Shader.PropertyToID("_FillAmount");
    static readonly int ID_PixelSize = Shader.PropertyToID("_BarPixelSize");
    static readonly int ID_UseGlow = Shader.PropertyToID("_UseGlow");
    static readonly int ID_GlowOuterPixels = Shader.PropertyToID("_GlowOuterPixels");
    static readonly int ID_GhostFill = Shader.PropertyToID("_GhostFill");

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("필수 연결")]
    [SerializeField] Canvas rootCanvas;
    [SerializeField] Material sourceMaterial;

    [Header("바 크기")]
    [SerializeField] Vector2 barSize = new Vector2(400f, 20f);

    [Header("고스트 HP 타이밍")]
    public float ghostDelay = 0.6f;
    public float ghostSpeed = 0.4f;

    // ── 내부 변수 ─────────────────────────────────────────────────────────────
    Material mat;
    RectTransform rt;

    float ghostFill = 1f;
    float currentFill = 1f;

    // UniTask 취소 토큰 관리
    CancellationTokenSource ghostCts;
    CancellationTokenSource shakeCts;

    // 셰이크 위치 보정을 위한 변수
    Vector2 originalPosition;
    bool isShaking = false;

    // ── Unity 생명주기 ────────────────────────────────────────────────────────
    void Awake()
    {
        var img = GetComponent<Image>();
        rt = GetComponent<RectTransform>();

        if (sourceMaterial == null) return;

        mat = new Material(sourceMaterial);
        mat.name = sourceMaterial.name + " (Instance)";
        img.material = mat;

        mat.SetFloat(ID_GhostFill, 1f);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);

        // 메모리 누수 방지 및 Task 강제 종료
        ghostCts?.Cancel();
        ghostCts?.Dispose();
        shakeCts?.Cancel();
        shakeCts?.Dispose();
    }

    void OnEnable() => UpdatePixelSize();

    void OnRectTransformDimensionsChange()
    {
        if (mat == null) return;
        if (!mat.IsKeywordEnabled(KW_GLOW)) UpdatePixelSize();
    }

    // ── 픽셀 크기 주입 ────────────────────────────────────────────────────────
    void UpdatePixelSize()
    {
        if (mat == null || rootCanvas == null) return;
        float scale = rootCanvas.scaleFactor;
        bool glowOn = mat.IsKeywordEnabled(KW_GLOW);
        float pixelW = glowOn ? Mathf.Round(barSize.x * scale) : Mathf.Round(rt.rect.width * scale);
        float pixelH = glowOn ? Mathf.Round(barSize.y * scale) : Mathf.Round(rt.rect.height * scale);
        mat.SetVector(ID_PixelSize, new Vector4(pixelW, pixelH, 0f, 0f));
    }

    // =========================================================================
    //  공개 API (UniTask 트리거 포인트)
    // =========================================================================

    /// <summary>채움 비율 설정 (0.0 ~ 1.0) - 고스트 HP 자동 작동</summary>
    public void SetFill(float ratio)
    {
        if (mat == null) return;

        currentFill = Mathf.Clamp01(ratio);
        mat.SetFloat(ID_Fill, currentFill);

        // 비동기 고스트 처리 시작
        ProcessGhostAsync().Forget();
    }

    // ── 비동기 고스트 HP 로직 ─────────────────────────────────────────────────
    private async UniTaskVoid ProcessGhostAsync()
    {
        // 이전 진행 중이던 고스트 로직 취소
        ghostCts?.Cancel();
        ghostCts = new CancellationTokenSource();
        var token = ghostCts.Token;

        try
        {
            if (ghostFill > currentFill)
            {
                // 1. 데미지를 입었을 때: 설정된 시간만큼 딜레이
                await UniTask.Delay(System.TimeSpan.FromSeconds(ghostDelay), cancellationToken: token);

                // 2. 고스트가 현재 HP에 도달할 때까지 선형 이동
                while (ghostFill > currentFill)
                {
                    ghostFill = Mathf.MoveTowards(ghostFill, currentFill, ghostSpeed * Time.deltaTime);
                    mat.SetFloat(ID_GhostFill, ghostFill);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            else
            {
                // 3. 체력을 회복했을 때: 고스트를 즉시 현재 위치로 스냅
                ghostFill = currentFill;
            }

            // 루프 종료 후 정확한 값 보정
            mat.SetFloat(ID_GhostFill, currentFill);
        }
        catch (System.OperationCanceledException)
        {
            // 연속 피격으로 작업이 취소된 경우 예외 무시
        }
    }

    // ── 비동기 피격 흔들림 ────────────────────────────────────────────────────
    public void PlayShake(float duration = 0.25f)
        => ProcessShakeAsync(duration, new Vector2(8f, 4f)).Forget(); //메서드 오버로딩 

    public void PlayShake(float duration, Vector2 strength) 
        => ProcessShakeAsync(duration, strength).Forget(); 

    public void PlayShakeByDamage(float damage, float maxHP, float duration = 0.25f)
    {
        float r = Mathf.Clamp01(damage / maxHP);
        ProcessShakeAsync(duration, Vector2.Lerp(new Vector2(3f, 1.5f), new Vector2(16f, 8f), r)).Forget();
    }

    private async UniTaskVoid ProcessShakeAsync(float duration, Vector2 strength)
    {
        // 연타를 맞았을 경우 이전 흔들림 즉시 취소
        shakeCts?.Cancel(); //멈추기!
        shakeCts = new CancellationTokenSource();
        var token = shakeCts.Token;

        try
        {
            // 흔들림이 시작될 때 단 한 번만 원래 위치를 저장
            if (!isShaking)
            {
                originalPosition = rt.anchoredPosition;
                isShaking = true;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = 1f - elapsed / duration;
                rt.anchoredPosition = originalPosition + Vector2.Scale(Random.insideUnitCircle, strength * t);
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (System.OperationCanceledException)
        {
            // 취소될 경우 아무 처리 안 함 
        }
        finally
        {
            // 정상 종료든 취소든 마지막엔 무조건 제자리 복구 
            if (shakeCts.Token.IsCancellationRequested == false)
            {
                rt.anchoredPosition = originalPosition;
                isShaking = false;
            }
        }
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

    // ── 피격 흔들림 ───────────────────────────────────────────────────────────

    ///// <summary>피격 흔들림 (기본)</summary>
    //public void PlayShake(float duration = 0.25f)
    //    => StartCoroutine(ShakeRoutine(duration, new Vector2(8f, 4f)));

    ///// <summary>피격 흔들림 (강도 지정)</summary>
    //public void PlayShake(float duration, Vector2 strength)
    //    => StartCoroutine(ShakeRoutine(duration, strength));

    ///// <summary>데미지 크기에 따라 흔들림 강도 자동 조절</summary>
    //public void PlayShakeByDamage(float damage, float maxHP, float duration = 0.25f)
    //{
    //    float r = Mathf.Clamp01(damage / maxHP);
    //    StartCoroutine(ShakeRoutine(duration,
    //        Vector2.Lerp(new Vector2(3f, 1.5f), new Vector2(16f, 8f), r)));
    //}

    //System.Collections.IEnumerator ShakeRoutine(float duration, Vector2 strength)
    //{
    //    Vector2 origin  = rt.anchoredPosition;
    //    float   elapsed = 0f;
    //    while (elapsed < duration)
    //    {
    //        float t = 1f - elapsed / duration;
    //        rt.anchoredPosition = origin + Vector2.Scale(Random.insideUnitCircle, strength * t);
    //        elapsed += Time.deltaTime;
    //        yield return null;
    //    }
    //    rt.anchoredPosition = origin;
    //}
}
