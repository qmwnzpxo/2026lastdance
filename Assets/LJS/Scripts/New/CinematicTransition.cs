using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 보스 사망 자동 전환과 포탈 상호작용 전환을 통합한 시네마틱 씬 전환 컴포넌트.
/// </summary>
public class CinematicTransition : MonoBehaviour
{
    public enum TransitionMode
    {
        AutoOnBossDead,
        PortalAfterBossDead,
        ImmediatePortal
    }

    [Header("전환 모드")]
    [SerializeField] private TransitionMode transitionMode = TransitionMode.AutoOnBossDead;

    [Header("보스 참조")]
    [Tooltip("AutoOnBossDead 또는 PortalAfterBossDead 모드에서 상태를 감시할 BossController")]
    [SerializeField] private BossController bossController;

    [Header("씬 이동 설정")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool useLoadingScene = true;
    [SerializeField] private string loadingSceneName = SceneLoader.DefaultLoadingSceneName;
    [Tooltip("AutoOnBossDead 모드에서 페이드 시작 전 대기 시간 (초)")]
    [SerializeField] private float delayBeforeFade = 2.5f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("UI 연결")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private GameObject promptUI;

    [Header("포탈 상호작용 설정")]
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private string playerTag = "Player";

    private bool _isPlayerInRange = false;
    private bool _isPortalUnlocked = false;
    private bool _isTransitioning = false;

    private void Awake()
    {
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = false;
        }

        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }

        _isPortalUnlocked = transitionMode == TransitionMode.ImmediatePortal;

        if (transitionMode != TransitionMode.ImmediatePortal && bossController == null)
        {
            Debug.LogWarning($"[CinematicTransition] {name}: BossController가 연결되지 않았습니다.", this);
        }
    }

    private void Update()
    {
        if (_isTransitioning) return;

        if (transitionMode == TransitionMode.AutoOnBossDead)
        {
            if (bossController != null && bossController.GetCurrentState() == BossState.Dead)
            {
                _isTransitioning = true;
                TransitionAsync(delayBeforeFade).Forget();
            }
            return;
        }

        if (transitionMode == TransitionMode.PortalAfterBossDead && !_isPortalUnlocked)
        {
            if (bossController != null && bossController.GetCurrentState() == BossState.Dead)
            {
                _isPortalUnlocked = true;
                if (promptUI != null && _isPlayerInRange)
                {
                    promptUI.SetActive(true);
                }
            }
        }

        if (_isPortalUnlocked && _isPlayerInRange && Input.GetKeyDown(interactionKey))
        {
            _isTransitioning = true;
            if (promptUI != null) promptUI.SetActive(false);
            TransitionAsync(0f).Forget();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        _isPlayerInRange = true;
        if (promptUI != null && _isPortalUnlocked && !_isTransitioning)
        {
            promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        _isPlayerInRange = false;
        if (promptUI != null)
        {
            promptUI.SetActive(false);
        }
    }

    private async UniTaskVoid TransitionAsync(float delay)
    {
        if (string.IsNullOrWhiteSpace(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[CinematicTransition] {name}: '{nextSceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다.", this);
            _isTransitioning = false;
            return;
        }

        CancellationToken ct = this.GetCancellationTokenOnDestroy();

        try
        {
            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            await FadeOutAsync(ct);

            if (useLoadingScene)
            {
                SceneLoader.LoadScene(nextSceneName, loadingSceneName);
                return;
            }

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
            if (asyncLoad == null)
            {
                Debug.LogError($"[CinematicTransition] {name}: '{nextSceneName}' 비동기 씬 로드에 실패했습니다.", this);
                RestoreFadeState();
                _isTransitioning = false;
                return;
            }

            asyncLoad.allowSceneActivation = false;
            while (asyncLoad.progress < 0.9f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            asyncLoad.allowSceneActivation = true;
        }
        catch (OperationCanceledException)
        {
            RestoreFadeState();
            _isTransitioning = false;
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            RestoreFadeState();
            _isTransitioning = false;
        }
    }

    private async UniTask FadeOutAsync(CancellationToken ct)
    {
        if (fadeImage == null) return;

        fadeImage.raycastTarget = true;

        if (fadeDuration > 0f)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                ct.ThrowIfCancellationRequested();
                if (fadeImage == null) return;

                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / fadeDuration);
                fadeImage.color = new Color(0f, 0f, 0f, alpha);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        if (fadeImage != null)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }
    }

    private void RestoreFadeState()
    {
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = false;
        }
    }
}
