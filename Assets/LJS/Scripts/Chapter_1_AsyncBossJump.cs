using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class Chapter_1_AsyncBossJump : MonoBehaviour
{
    [Header("이동할 씬의 정확한 이름을 적어주세요")]
    public string targetSceneName;

    // 플레이어가 콜라이더 안에서 비비적거릴 때
    // 로딩이 수십 번 겹쳐서 실행되는 것을 막는 안전장치입니다.
    private bool isLoading = false;

    // 유니티 2D 물리엔진: 트리거에 무언가 들어왔을 때 실행
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. 들어온 물체가 플레이어이고
        // 2. 현재 로딩 중이 아닐 때만 실행
        if (other.CompareTag("Player") && !isLoading)
        {
            StartCoroutine(LoadSceneAsyncCoroutine());
        }
    }

    private IEnumerator LoadSceneAsyncCoroutine()
    {
        // 로딩 시작 상태로 잠금 (중복 실행 방지)
        isLoading = true;
        Debug.Log($"'{targetSceneName}' 씬 비동기 로딩을 시작합니다...");

        // 백그라운드에서 씬 데이터를 불러오기 시작합니다.
        AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);

        // 로딩이 완료되면 다른 스크립트의 허락을 기다리지 않고 즉시 씬을 전환합니다.
        // (독립적인 작동을 위해 true로 둡니다)
        operation.allowSceneActivation = true;

        // 진행 상황을 유니티 콘솔창에 찍어줍니다. (게임 렉을 유발하지 않는 백그라운드 반복문)
        while (!operation.isDone)
        {
            // 진행도(0.0 ~ 1.0)를 백분율로 계산
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log($"로딩 중... {progress * 100}%");

            // 다음 프레임까지 대기하여 메인 게임이 멈추지 않게 합니다.
            yield return null;
        }
    }
}
