using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 트리거 통합 컴포넌트.
/// 씬 이름 또는 빌드 인덱스, 동기/비동기 로드, 즉시 진입/키 입력 진입을 인스펙터에서 선택할 수 있습니다.
/// </summary>
public class ChapterJump : MonoBehaviour
{
    public enum SceneTargetType
    {
        SceneName,
        BuildIndex
    }

    public enum JumpTriggerMode
    {
        OnTriggerEnter,
        OnInteractKey
    }

    [Header("이동 대상 씬 설정")]
    [SerializeField] private SceneTargetType targetType = SceneTargetType.SceneName;
    [SerializeField] private string targetSceneName;
    [SerializeField] private int targetBuildIndex = 0;

    [Header("실행 방식 설정")]
    [SerializeField] private JumpTriggerMode triggerMode = JumpTriggerMode.OnTriggerEnter;
    [SerializeField] private KeyCode interactionKey = KeyCode.F;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool useLoadingScene = true;
    [SerializeField] private string loadingSceneName = SceneLoader.DefaultLoadingSceneName;
    [SerializeField] private bool useAsyncLoad = true;
    [SerializeField] private bool logProgress = false;

    [Header("저장")]
    [Tooltip("이동 직전에 구역 저장(SectionSave)을 초기화. 거점 → 챕터 시작처럼 챕터를 처음부터 시작하는 이동에만 켠다")]
    [SerializeField] private bool clearSectionSave = false;

    private bool _isPlayerInRange = false;
    private bool _isLoading = false;

    private void Update()
    {
        if (triggerMode == JumpTriggerMode.OnInteractKey &&
            _isPlayerInRange &&
            !_isLoading &&
            Input.GetKeyDown(interactionKey))
        {
            JumpToScene();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        _isPlayerInRange = true;

        if (triggerMode == JumpTriggerMode.OnTriggerEnter && !_isLoading)
        {
            JumpToScene();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        _isPlayerInRange = false;
    }

    /// <summary>
    /// 설정된 대상 씬으로 전환을 시작합니다. UI 버튼이나 외부 이벤트에서도 호출 가능합니다.
    /// </summary>
    public void JumpToScene()
    {
        if (_isLoading) return;

        if (!TryValidateTarget(out string errorMessage))
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                Debug.LogError($"[ChapterJump] {name}: {errorMessage}", this);
            }
            return;
        }

        if (clearSectionSave)
            SectionSave.Clear();

        if (useLoadingScene)
        {
            _isLoading = true;
            if (targetType == SceneTargetType.SceneName)
            {
                SceneLoader.LoadScene(targetSceneName, loadingSceneName);
            }
            else
            {
                SceneLoader.LoadScene(targetBuildIndex, loadingSceneName);
            }
            return;
        }

        if (useAsyncLoad)
        {
            StartCoroutine(LoadSceneAsyncCoroutine());
        }
        else
        {
            _isLoading = true;
            if (targetType == SceneTargetType.SceneName)
            {
                SceneManager.LoadScene(targetSceneName);
            }
            else
            {
                SceneManager.LoadScene(targetBuildIndex);
            }
        }
    }

    private bool TryValidateTarget(out string errorMessage)
    {
        if (targetType == SceneTargetType.SceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                errorMessage = "targetSceneName이 비어 있습니다.";
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                errorMessage = $"'{targetSceneName}' 씬을 찾을 수 없거나 Build Settings에 등록되지 않았습니다.";
                return false;
            }
        }
        else
        {
            if (targetBuildIndex < 0 || targetBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                errorMessage = $"빌드 인덱스 {targetBuildIndex}가 유효 범위를 벗어났습니다. (등록된 씬 수: {SceneManager.sceneCountInBuildSettings})";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private IEnumerator LoadSceneAsyncCoroutine()
    {
        _isLoading = true;

        AsyncOperation operation = targetType == SceneTargetType.SceneName
            ? SceneManager.LoadSceneAsync(targetSceneName)
            : SceneManager.LoadSceneAsync(targetBuildIndex);

        if (operation == null)
        {
            Debug.LogError($"[ChapterJump] {name}: 비동기 씬 로드 시작에 실패했습니다.", this);
            _isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            if (logProgress)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                Debug.Log($"[ChapterJump] 로딩 중... {progress * 100f:F0}%");
            }

            yield return null;
        }
    }
}
