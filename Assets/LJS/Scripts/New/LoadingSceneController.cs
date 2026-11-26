using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Loading 씬에 배치하여 대기 중인 목표 씬을 비동기 로드하고 진행률 UI를 갱신하는 컨트롤러.
/// UI 참조가 비어 있어도 오류 없이 동작합니다.
/// </summary>
public class LoadingSceneController : MonoBehaviour
{
    [Header("진행률 UI (선택)")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image progressFillImage;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private string progressFormat = "{0:0}%";

    [Header("안내 문구 / 팁 UI (선택)")]
    [SerializeField] private TMP_Text tipText;
    [TextArea]
    [SerializeField] private string[] loadingTips;
    [SerializeField] private GameObject pressAnyKeyPrompt;

    [Header("로딩 동작 설정")]
    [Tooltip("로딩 화면이 너무 순식간에 지나가지 않도록 유지할 최소 시간 (초)")]
    [SerializeField] private float minimumLoadTime = 1.0f;
    [Tooltip("표시 진행률이 실제 진행률을 따라가는 보간 속도")]
    [SerializeField] private float progressSmoothSpeed = 5f;
    [Tooltip("체크하면 100% 도달 후 아무 키/클릭 입력 시 다음 씬으로 넘어갑니다.")]
    [SerializeField] private bool waitForInputOnComplete = false;

    [Header("페이드 연출 (선택)")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("에디터 단독 테스트용 대체 씬")]
    [Tooltip("Loading 씬을 에디터에서 직접 실행했을 때 대신 로드할 씬 이름 (비워두면 대기)")]
    [SerializeField] private string fallbackSceneName = "Main";

    private float _displayedProgress = 0f;

    private void Awake()
    {
        Time.timeScale = 1f;
        UpdateProgressUI(0f);

        if (pressAnyKeyPrompt != null)
        {
            pressAnyKeyPrompt.SetActive(false);
        }

        if (tipText != null && loadingTips != null && loadingTips.Length > 0)
        {
            tipText.text = loadingTips[UnityEngine.Random.Range(0, loadingTips.Length)];
        }
    }

    private void Start()
    {
        LoadTargetSceneAsync().Forget();
    }

    private async UniTaskVoid LoadTargetSceneAsync()
    {
        CancellationToken ct = this.GetCancellationTokenOnDestroy();

        try
        {
            if (fadeImage != null)
            {
                await FadeAsync(1f, 0f, fadeDuration, ct);
            }

            AsyncOperation asyncLoad = StartTargetAsyncOperation();
            if (asyncLoad == null)
            {
                Debug.LogWarning("[LoadingSceneController] 로드할 목표 씬이 없습니다. SceneLoader.LoadScene()으로 진입했는지 또는 fallbackSceneName을 확인하세요.", this);
                return;
            }

            asyncLoad.allowSceneActivation = false;

            float elapsed = 0f;
            while (!ct.IsCancellationRequested)
            {
                elapsed += Time.unscaledDeltaTime;

                float rawProgress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                float minTimeProgress = minimumLoadTime > 0f ? Mathf.Clamp01(elapsed / minimumLoadTime) : 1f;
                float targetProgress = Mathf.Min(rawProgress, minTimeProgress);

                if (progressSmoothSpeed > 0f)
                {
                    _displayedProgress = Mathf.MoveTowards(_displayedProgress, targetProgress, progressSmoothSpeed * Time.unscaledDeltaTime);
                }
                else
                {
                    _displayedProgress = targetProgress;
                }

                UpdateProgressUI(_displayedProgress);

                bool isLoadReady = asyncLoad.progress >= 0.9f;
                bool isMinTimeDone = elapsed >= minimumLoadTime;
                bool isVisualDone = _displayedProgress >= 0.999f;

                if (isLoadReady && isMinTimeDone && isVisualDone)
                {
                    UpdateProgressUI(1f);
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (waitForInputOnComplete)
            {
                if (pressAnyKeyPrompt != null)
                {
                    pressAnyKeyPrompt.SetActive(true);
                }

                await UniTask.WaitUntil(() => Input.anyKeyDown || Input.GetMouseButtonDown(0), cancellationToken: ct);
            }

            if (fadeImage != null)
            {
                await FadeAsync(0f, 1f, fadeDuration, ct);
            }

            asyncLoad.allowSceneActivation = true;
        }
        catch (OperationCanceledException)
        {
            // 씬 종료/오브젝트 파괴 시 정상 취소
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }

    private AsyncOperation StartTargetAsyncOperation()
    {
        if (SceneLoader.HasPendingTarget)
        {
            return SceneLoader.LoadPendingSceneAsync();
        }

        if (!string.IsNullOrWhiteSpace(fallbackSceneName) && Application.CanStreamedLevelBeLoaded(fallbackSceneName))
        {
            return SceneManager.LoadSceneAsync(fallbackSceneName);
        }

        return null;
    }

    private void UpdateProgressUI(float progress)
    {
        float clamped = Mathf.Clamp01(progress);

        if (progressBar != null)
        {
            progressBar.value = clamped;
        }

        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = clamped;
        }

        if (progressText != null)
        {
            progressText.text = string.Format(progressFormat, clamped * 100f);
        }
    }

    private async UniTask FadeAsync(float fromAlpha, float toAlpha, float duration, CancellationToken ct)
    {
        if (fadeImage == null) return;

        fadeImage.raycastTarget = toAlpha > 0f;
        Color color = fadeImage.color;

        if (duration > 0f)
        {
            float timer = 0f;
            while (timer < duration)
            {
                ct.ThrowIfCancellationRequested();
                if (fadeImage == null) return;

                timer += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(timer / duration));
                fadeImage.color = color;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        if (fadeImage != null)
        {
            color.a = toAlpha;
            fadeImage.color = color;
            if (Mathf.Approximately(toAlpha, 0f))
            {
                fadeImage.raycastTarget = false;
            }
        }
    }
}
