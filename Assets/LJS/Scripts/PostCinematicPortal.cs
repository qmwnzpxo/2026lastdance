using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// '?' 오브젝트에 직접 붙이는 스크립트.
/// 
/// 보스 사망 감지 전: F키 상호작용 비활성
/// 보스 사망 후: 상호작용 활성 → 플레이어가 F키 누름 → nextSceneName 씬으로 이동
/// </summary>
public class PostCinematicPortal : MonoBehaviour
{
    [Header("보스 참조")]
    [Tooltip("BossController 컴포넌트를 연결하세요.")]
    [SerializeField] private BossController bossController;

    [Header("씬 이동 설정")]
    [Tooltip("F키를 눌렀을 때 이동할 씬 이름.")]
    [SerializeField] private string nextSceneName = "Main";

    [Tooltip("화면 페이드아웃에 걸리는 시간 (초).")]
    [SerializeField] private float fadeDuration = 1.0f;

    [Header("UI 설정")]
    [Tooltip("화면 전체를 덮는 검정 Image 컴포넌트.")]
    [SerializeField] private Image fadeImage;

    [Header("상호작용 설정")]
    public KeyCode interactionKey = KeyCode.F;

    [Tooltip("플레이어 범위 내 안내 UI. 없으면 비워두세요.")]
    [SerializeField] private GameObject promptUI;

    private bool _isPlayerInRange = false;
    private bool _isTransitioning = false;
    private bool _isRevealed      = false;

    private void Awake()
    {
        if (promptUI != null) promptUI.SetActive(false);

        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
            fadeImage.raycastTarget = false;
        }

        if (bossController == null)
            Debug.LogWarning("[PostCinematicPortal] BossController가 연결되지 않았습니다!");
    }

    private void Update()
    {
        // ── 보스 사망 감지 → '?' 오브젝트 표시 ───────────────────────────
        if (!_isRevealed && bossController != null)
        {
            if (bossController.GetCurrentState() == BossState.Dead)
            {
                _isRevealed = true;
                RevealPortal();
            }
        }

        // ── F키 입력 → 씬 전환 ────────────────────────────────────────────
        if (_isPlayerInRange && !_isTransitioning && Input.GetKeyDown(interactionKey))
        {
            _isTransitioning = true;
            TransitionAsync().Forget();
        }
    }

    private void RevealPortal()
    {
        Debug.Log("[PostCinematicPortal] 보스 사망 → F키 상호작용 활성화.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            if (promptUI != null) promptUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            if (promptUI != null) promptUI.SetActive(false);
        }
    }

    private async UniTaskVoid TransitionAsync()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[PostCinematicPortal] '{nextSceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다!");
            _isTransitioning = false;
            return;
        }

        Debug.Log($"[PostCinematicPortal] F키 → '{nextSceneName}' 씬 전환.");
        var ct = this.GetCancellationTokenOnDestroy();

        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true;
            if (fadeDuration > 0f)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    if (ct.IsCancellationRequested || fadeImage == null) break;
                    timer += Time.deltaTime;
                    fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(timer / fadeDuration));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            if (fadeImage != null)
            {
                fadeImage.color = new Color(0, 0, 0, 1f);
            }
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
        if (asyncLoad == null)
        {
            Debug.LogError($"[PostCinematicPortal] '{nextSceneName}' 씬을 찾을 수 없습니다!");
            if (fadeImage != null) fadeImage.raycastTarget = false;
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
}
