// ──────────────────────────────────────────────────────────────────────────
// SystemMessageUI.cs
// 시스템 메시지 팝업 UI 컨트롤러.
// 슬라이드 인 → 액션 감지까지 화면 유지 → 위로 페이드아웃.
// 시간 기반 자동 닫힘 없음. TutorialManager가 DismissAsync()로 닫는다.
// ──────────────────────────────────────────────────────────────────────────
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// Inspector에서 RectTransform, CanvasGroup, TextMeshProUGUI를 연결해 사용.
/// Canvas의 Render Mode는 Screen Space - Overlay 권장.
/// </summary>
public class SystemMessageUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _messageText;

    [Header("Layout")]
    [Tooltip("화면 밖 시작 X 오프셋 (패널 너비보다 크게)")]
    [SerializeField] private float _offscreenOffsetX = 600f;
    [Tooltip("표시 중 패널의 앵커 기준 X 위치 (0 = 앵커 원점)")]
    [SerializeField] private float _onscreenX = 0f;
    [Tooltip("페이드아웃 시 위로 이동할 거리 (px)")]
    [SerializeField] private float _fadeOutRiseY = 80f;

    [Header("Duration (unscaled seconds)")]
    [SerializeField] private float _slideInDuration = 0.35f;
    [SerializeField] private float _fadeOutDuration = 0.4f;

    // ── 런타임 ────────────────────────────────────────────────────────────
    private CancellationTokenSource _cts;

    // ── 초기화 ────────────────────────────────────────────────────────────
    private void Awake()
    {
        SetOffscreen();
        _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    #region 외부 API

    /// <summary>
    /// 메시지를 슬라이드 인해서 화면에 띄운다.
    /// 슬라이드 인 연출이 끝나면 await가 풀리고,
    /// 이후 메시지는 DismissAsync()가 호출될 때까지 화면에 유지된다.
    /// </summary>
    public async UniTask ShowAsync(string message, CancellationToken externalCt = default)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _cts.Token;

        _messageText.text = message;
        gameObject.SetActive(true);

        try
        {
            await SlideIn(ct);
        }
        catch
        {
            Cleanup();
            throw;
        }
    }

    /// <summary>
    /// 화면에 떠 있는 메시지를 페이드아웃하며 닫는다.
    /// TutorialManager에서 액션 감지 후 호출.
    /// await 하면 페이드아웃 완료 후 제어가 돌아온다.
    /// </summary>
    public async UniTask DismissAsync(CancellationToken externalCt = default)
    {
        // 기존 _cts와 연결해 외부 취소도 반영
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts?.Token ?? CancellationToken.None, externalCt);
        var ct = linkedCts.Token;

        try
        {
            await FadeOut(ct);
        }
        finally
        {
            Cleanup();
        }
    }

    /// <summary>
    /// 연출 없이 즉시 숨긴다 (씬 전환, 강제 중단 등).
    /// </summary>
    public void Hide()
    {
        _cts?.Cancel();
        Cleanup();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    #region 내부 애니메이션

    private async UniTask SlideIn(CancellationToken ct)
    {
        float startX = _onscreenX + _offscreenOffsetX;
        float targetX = _onscreenX;
        float startY = _panel.anchoredPosition.y;

        _panel.anchoredPosition = new Vector2(startX, startY);
        _canvasGroup.alpha = 1f;

        await TweenAsync(_slideInDuration, ct, t =>
        {
            float x = Mathf.Lerp(startX, targetX, EaseOutCubic(t));
            _panel.anchoredPosition = new Vector2(x, startY);
        });
    }

    private async UniTask FadeOut(CancellationToken ct)
    {
        float startX = _panel.anchoredPosition.x;
        float startY = _panel.anchoredPosition.y;
        float targetY = startY + _fadeOutRiseY;

        await TweenAsync(_fadeOutDuration, ct, t =>
        {
            _panel.anchoredPosition = new Vector2(startX, Mathf.Lerp(startY, targetY, EaseInCubic(t)));
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
        });
    }

    // ── 범용 트윈 헬퍼 ───────────────────────────────────────────────────
    private static async UniTask TweenAsync(
        float duration, CancellationToken ct, System.Action<float> onUpdate)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            onUpdate(Mathf.Clamp01(elapsed / duration));

            bool canceled = await UniTask
                .WaitForFixedUpdate(cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return;

            elapsed += Time.fixedUnscaledDeltaTime;
        }
        onUpdate(1f);
    }

    // ── 이징 ─────────────────────────────────────────────────────────────
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInCubic(float t) => t * t * t;

    // ── 유틸 ─────────────────────────────────────────────────────────────
    private void SetOffscreen()
        => _panel.anchoredPosition = new Vector2(_onscreenX + _offscreenOffsetX,
                                                 _panel.anchoredPosition.y);

    private void Cleanup()
    {
        gameObject.SetActive(false);
        SetOffscreen();
        _canvasGroup.alpha = 0f;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────────────
    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
