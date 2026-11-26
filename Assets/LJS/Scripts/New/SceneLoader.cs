using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 요청을 받아 Loading 씬을 경유해 다음 씬으로 이동시키는 정적 로더.
/// </summary>
public static class SceneLoader
{
    public const string DefaultLoadingSceneName = "Loading";

    public static bool HasPendingTarget { get; private set; }
    public static bool UseBuildIndex { get; private set; }
    public static string PendingSceneName { get; private set; }
    public static int PendingBuildIndex { get; private set; } = -1;

    /// <summary>
    /// 씬 이름을 지정해 Loading 씬을 거쳐 목표 씬으로 이동합니다.
    /// </summary>
    public static void LoadScene(string sceneName, string loadingSceneName = DefaultLoadingSceneName, bool fallbackToDirectLoad = true)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneLoader] 이동할 sceneName이 비어 있습니다.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneLoader] '{sceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다.");
            return;
        }

        Time.timeScale = 1f;

        if (!CanUseLoadingScene(loadingSceneName))
        {
            if (fallbackToDirectLoad)
            {
                Debug.LogWarning($"[SceneLoader] '{loadingSceneName}' 씬이 Build Settings에 없어 '{sceneName}' 씬으로 직접 이동합니다.");
                ClearPendingTarget();
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogError($"[SceneLoader] '{loadingSceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다.");
            }
            return;
        }

        UseBuildIndex = false;
        PendingSceneName = sceneName;
        PendingBuildIndex = -1;
        HasPendingTarget = true;

        SceneManager.LoadScene(loadingSceneName);
    }

    /// <summary>
    /// 빌드 인덱스를 지정해 Loading 씬을 거쳐 목표 씬으로 이동합니다.
    /// </summary>
    public static void LoadScene(int buildIndex, string loadingSceneName = DefaultLoadingSceneName, bool fallbackToDirectLoad = true)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[SceneLoader] 빌드 인덱스 {buildIndex}가 유효 범위를 벗어났습니다. (등록된 씬 수: {SceneManager.sceneCountInBuildSettings})");
            return;
        }

        Time.timeScale = 1f;

        if (!CanUseLoadingScene(loadingSceneName))
        {
            if (fallbackToDirectLoad)
            {
                Debug.LogWarning($"[SceneLoader] '{loadingSceneName}' 씬이 Build Settings에 없어 인덱스 {buildIndex} 씬으로 직접 이동합니다.");
                ClearPendingTarget();
                SceneManager.LoadScene(buildIndex);
            }
            else
            {
                Debug.LogError($"[SceneLoader] '{loadingSceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다.");
            }
            return;
        }

        UseBuildIndex = true;
        PendingSceneName = null;
        PendingBuildIndex = buildIndex;
        HasPendingTarget = true;

        SceneManager.LoadScene(loadingSceneName);
    }

    /// <summary>
    /// 대기 중인 목표 씬을 비동기 로드합니다. Loading 씬 내부 컨트롤러에서 호출합니다.
    /// </summary>
    public static AsyncOperation LoadPendingSceneAsync()
    {
        if (!HasPendingTarget) return null;

        if (UseBuildIndex)
        {
            int index = PendingBuildIndex;
            ClearPendingTarget();
            return SceneManager.LoadSceneAsync(index);
        }

        string targetName = PendingSceneName;
        ClearPendingTarget();
        return SceneManager.LoadSceneAsync(targetName);
    }

    /// <summary>
    /// 대기 중인 씬 이동 정보를 초기화합니다.
    /// </summary>
    public static void ClearPendingTarget()
    {
        HasPendingTarget = false;
        UseBuildIndex = false;
        PendingSceneName = null;
        PendingBuildIndex = -1;
    }

    private static bool CanUseLoadingScene(string loadingSceneName)
    {
        return !string.IsNullOrWhiteSpace(loadingSceneName) &&
               Application.CanStreamedLevelBeLoaded(loadingSceneName);
    }
}
