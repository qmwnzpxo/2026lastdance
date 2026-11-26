using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BossDeathSceneTransition : MonoBehaviour
{
    [Header("보스 참조")]
    [Tooltip("DamageHandler가 아닌 BossController 컴포넌트를 연결해주세요.")]
    [SerializeField] private BossController bossController;

    [Header("씬 이동 설정")]
    [Tooltip("이동할 다음 씬의 이름을 입력하세요.")]
    [SerializeField] private string nextSceneName = "NextSceneName";

    [Tooltip("보스 사망 연출을 기다리는 시간 (DeadState 코루틴이 2초 대기하므로 2~2.5초 권장)")]
    [SerializeField] private float delayBeforeFade = 2.5f;

    [Tooltip("화면이 어두워지는 데 걸리는 시간")]
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("UI 연결")]
    [Tooltip("화면 전체를 덮는 검은색 UI Image를 연결해주세요.")]
    [SerializeField] private Image fadeImage;

    private bool _isTransitionStarted = false; // 중복 실행 방지용 플래그

    private void Start()
    {
        // 화면을 투명하게 초기화하고 터치를 방해하지 않게 설정합니다.
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
            fadeImage.raycastTarget = false;
        }

        if (bossController == null)
        {
            Debug.LogWarning("[BossDeathSceneTransition] BossController가 연결되지 않았습니다!");
        }
    }

    private void Update()
    {
        // 씬 전환이 아직 시작되지 않았고, 보스 컨트롤러가 존재할 때 매 프레임 상태를 확인합니다.
        if (!_isTransitionStarted && bossController != null)
        {
            // 보스의 현재 상태가 '사망(Dead)'으로 변경되었는지 직접 확인합니다.
            if (bossController.GetCurrentState() == BossState.Dead)
            {
                _isTransitionStarted = true; // 코루틴이 여러 번 실행되는 것을 막습니다.
                StartCoroutine(SceneTransitionCoroutine());
            }
        }
    }

    private IEnumerator SceneTransitionCoroutine()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[BossDeathSceneTransition] '{nextSceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다!");
            _isTransitionStarted = false;
            yield break;
        }

        Debug.Log($"[BossDeathSceneTransition] 보스 사망 상태 감지됨. {delayBeforeFade}초 대기 후 페이드 아웃 시작.");

        // 1. 보스의 사망 애니메이션과 이펙트가 끝날 때까지 대기합니다.
        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFade);
        }

        // 2. 화면 페이드 아웃 (서서히 암전)
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true; // 페이드 아웃 중 플레이어의 UI 터치 방지
            if (fadeDuration > 0f)
            {
                float timer = 0f;
                while (timer < fadeDuration)
                {
                    if (fadeImage == null) break;
                    timer += Time.deltaTime;
                    float alpha = Mathf.Clamp01(timer / fadeDuration);
                    fadeImage.color = new Color(0, 0, 0, alpha);
                    yield return null;
                }
            }

            if (fadeImage != null)
            {
                fadeImage.color = new Color(0, 0, 0, 1f);
            }
        }

        Debug.Log($"[BossDeathSceneTransition] '{nextSceneName}' 씬 비동기 로딩 시작.");

        // 3. 비동기 씬 로드 (LoadSceneAsync 활용)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName);
        if (asyncLoad == null)
        {
            Debug.LogError($"[BossDeathSceneTransition] '{nextSceneName}' 씬 비동기 로드에 실패했습니다.");
            if (fadeImage != null) fadeImage.raycastTarget = false;
            _isTransitionStarted = false;
            yield break;
        }

        asyncLoad.allowSceneActivation = false; // 로딩이 끝나도 즉시 넘어가지 않도록 대기

        // 유니티의 비동기 로딩은 90%(0.9)가 되면 완료로 판정합니다.
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // 4. 로딩 완료 및 씬 활성화
        asyncLoad.allowSceneActivation = true;
    }
}